using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Quest performance pass for the active scene (see CLAUDE.md, "Quest performance"). Each menu item is one
// step; scene edits support Undo, and every step logs a "[QuestPerf]" summary to the console.
public static class QuestPerformanceTools
{
    private const string MenuRoot = "Tools/Quest Performance/";
    private const string LogPrefix = "[QuestPerf] ";
    private const string MergedObjectName = "__Merged";
    private const string MergedMeshFolder = "Assets/Generated/MergedMeshes";

    // Static decoration made of many small box/tape/sticker/pallet renderers per instance.
    private static readonly string[] MergePrefabPaths =
    {
        "Assets/Prefabs/env/pallet_boxes_prefab.prefab",
        "Assets/Prefabs/env/pallet_box_pack_prefab.prefab",
    };

    // Anything under one of these can move, animate or is UI, so it is never marked occlusion static.
    private static readonly Type[] MovableComponentTypes =
    {
        typeof(Rigidbody), typeof(Animator), typeof(Animation), typeof(ParticleSystem), typeof(Canvas),
        typeof(Camera), typeof(CharacterController), typeof(Cloth), typeof(Joint), typeof(UnityEngine.AI.NavMeshAgent),
    };

    // Meshes whose coarser Mesh LODs visibly break (checked with screenshots), capped at this level.
    // The racks' thin perforated uprights twist and lose their shading from LOD 2 on.
    private static readonly Dictionary<string, int> MaxForcedLod = new Dictionary<string, int>
    {
        { "main_structural_01", 1 },
    };

    private const int HeavyMeshTriangles = 20000;
    private const float SmallPropSize = 1f;          // bounds diagonal in metres
    private const int SmallPropTriangleTarget = 8000;
    private const int LargeMeshTriangleTarget = 20000;
    private const int AndroidTextureMaxSize = 2048;

    // Where the trainee stands during Module 1 (see CLAUDE.md §4), eye height 1.6 m.
    private static readonly (string name, Vector3 position, float yaw)[] Viewpoints =
    {
        ("Spawn", new Vector3(0f, 1.6f, 26.2f), 180f),
        ("Corridor", new Vector3(0f, 1.6f, 17f), 180f),
        ("A-aisle / tote", new Vector3(1f, 1.6f, 5f), 180f),
        ("B/C aisle", new Vector3(0.1f, 1.6f, 10.5f), 0f),
    };

    // ---------------------------------------------------------------- Report

    [MenuItem(MenuRoot + "Report", priority = 0)]
    public static void Report()
    {
        List<Renderer> renderers = ActiveRenderers(SceneManager.GetActiveScene());
        var sb = new StringBuilder(LogPrefix + "Report for " + SceneManager.GetActiveScene().name + "\n");

        int draws = 0, occluders = 0, occludees = 0, forcedLod = 0;
        long triangles = 0;
        foreach (Renderer r in renderers)
        {
            Mesh mesh = SharedMesh(r);
            if (mesh == null) continue;
            draws += DrawCount(r, mesh);
            triangles += Triangles(r, mesh);
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
            if ((flags & StaticEditorFlags.OccluderStatic) != 0) occluders++;
            if ((flags & StaticEditorFlags.OccludeeStatic) != 0) occludees++;
            if (r.forceMeshLod >= 0) forcedLod++;
        }

        sb.AppendLine($"Active renderers: {renderers.Count}, draws (submesh x material): {draws}, triangles: {triangles:N0}");
        sb.AppendLine($"Occluder static: {occluders}, occludee static: {occludees}, forced Mesh LOD: {forcedLod}");
        sb.AppendLine($"Occlusion data baked: {StaticOcclusionCulling.umbraDataSize > 0} ({StaticOcclusionCulling.umbraDataSize:N0} bytes)");
        sb.AppendLine($"Merged pallet stacks: {Object.FindObjectsByType<MergedMeshGroup>(FindObjectsSortMode.None).Length}");

        int raycasters = Object.FindObjectsByType<GraphicRaycaster>(FindObjectsSortMode.None).Length;
        sb.AppendLine($"Active GraphicRaycasters: {raycasters}");

        sb.AppendLine("In view (frustum only, no occlusion; forced Mesh LOD counted, otherwise LOD0):");
        foreach (var viewpoint in Viewpoints)
        {
            (int viewDraws, long viewTriangles) = CountInView(renderers, viewpoint.position, viewpoint.yaw);
            sb.AppendLine($"  {viewpoint.name,-15} draws {viewDraws,6}  triangles {viewTriangles,12:N0}");
        }

        Debug.Log(sb.ToString());
    }

    private static (int draws, long triangles) CountInView(List<Renderer> renderers, Vector3 position, float yaw)
    {
        var cameraObject = new GameObject("QuestPerfViewpoint") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.fieldOfView = 100f; // roughly Quest's combined field of view
            camera.aspect = 1f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 1000f;
            cameraObject.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
            int draws = 0;
            long triangles = 0;
            foreach (Renderer r in renderers)
            {
                Mesh mesh = SharedMesh(r);
                if (mesh == null || !GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
                draws += DrawCount(r, mesh);
                triangles += Triangles(r, mesh);
            }
            return (draws, triangles);
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
        }
    }

    // ---------------------------------------------------------------- 1. Merge pallet stacks

    [MenuItem(MenuRoot + "1. Merge Pallet Stacks", priority = 20)]
    public static void MergePalletStacks()
    {
        Scene scene = SceneManager.GetActiveScene();
        List<GameObject> roots = PrefabInstanceRoots(scene, MergePrefabPaths);
        string folder = EnsureFolder($"{MergedMeshFolder}/{scene.name}");

        Undo.SetCurrentGroupName("Merge pallet stacks");
        int undoGroup = Undo.GetCurrentGroup();
        int stacks = 0, sourceCount = 0, mergedCount = 0, skipped = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (GameObject root in roots)
            {
                if (root.transform.Find(MergedObjectName) != null) { skipped++; continue; }
                (int sources, int merged) = MergeStack(root, folder);
                if (merged == 0) continue;
                stacks++;
                sourceCount += sources;
                mergedCount += merged;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(LogPrefix + $"Merged {stacks} pallet stacks: {sourceCount} renderers -> {mergedCount} " +
                  $"(already merged: {skipped}). Meshes in {folder}. Save the scene to keep it.");
    }

    private struct MergeKey : IEquatable<MergeKey>
    {
        public Material Material;
        public int LightmapIndex;
        public ShadowCastingMode Shadows;
        public bool ReceiveShadows;
        public int Layer;

        public bool Equals(MergeKey o) => Material == o.Material && LightmapIndex == o.LightmapIndex &&
                                          Shadows == o.Shadows && ReceiveShadows == o.ReceiveShadows && Layer == o.Layer;
        public override bool Equals(object obj) => obj is MergeKey o && Equals(o);
        public override int GetHashCode() => HashCode.Combine(Material, LightmapIndex, Shadows, ReceiveShadows, Layer);
    }

    private struct MergePart
    {
        public Mesh Mesh;
        public int SubMesh;
        public Matrix4x4 ToMerged;
        public Vector4 LightmapScaleOffset;
    }

    private static (int sources, int merged) MergeStack(GameObject root, string folder)
    {
        var sources = new List<Renderer>();
        var buckets = new Dictionary<MergeKey, List<MergePart>>();
        var templates = new Dictionary<MergeKey, MeshRenderer>();
        Matrix4x4 rootWorldToLocal = root.transform.worldToLocalMatrix;

        foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(false))
        {
            if (!r.enabled || r.GetComponent<MergedMeshGroup>() != null) continue;
            var filter = r.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null) continue;

            int lightmapIndex = MergedMeshGroup.IsLightmapped(r) ? r.lightmapIndex : -1;
            Material[] materials = r.sharedMaterials;
            for (int s = 0; s < Mathf.Min(mesh.subMeshCount, materials.Length); s++)
            {
                if (materials[s] == null) continue;
                var key = new MergeKey
                {
                    Material = materials[s], LightmapIndex = lightmapIndex, Shadows = r.shadowCastingMode,
                    ReceiveShadows = r.receiveShadows, Layer = r.gameObject.layer,
                };
                if (!buckets.TryGetValue(key, out List<MergePart> parts))
                {
                    buckets[key] = parts = new List<MergePart>();
                    templates[key] = r;
                }
                parts.Add(new MergePart
                {
                    Mesh = mesh, SubMesh = s, ToMerged = rootWorldToLocal * r.transform.localToWorldMatrix,
                    LightmapScaleOffset = r.lightmapScaleOffset,
                });
            }
            sources.Add(r);
        }

        if (sources.Count < 2) return (0, 0);

        var mergedRoot = new GameObject(MergedObjectName) { layer = root.layer };
        Undo.RegisterCreatedObjectUndo(mergedRoot, "Merge pallet stacks");
        Undo.SetTransformParent(mergedRoot.transform, root.transform, "Merge pallet stacks");
        mergedRoot.transform.localPosition = Vector3.zero;
        mergedRoot.transform.localRotation = Quaternion.identity;
        mergedRoot.transform.localScale = Vector3.one;
        var group = mergedRoot.AddComponent<MergedMeshGroup>();

        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{Sanitize(root.name)}.asset");
        bool assetCreated = false;
        int index = 0;
        foreach (KeyValuePair<MergeKey, List<MergePart>> bucket in buckets)
        {
            MergeKey key = bucket.Key;
            bool lightmapped = key.LightmapIndex >= 0;
            Mesh mesh = BuildMergedMesh(bucket.Value, lightmapped, $"{root.name}_{index}_{key.Material.name}");

            if (!assetCreated) { AssetDatabase.CreateAsset(mesh, assetPath); assetCreated = true; }
            else AssetDatabase.AddObjectToAsset(mesh, assetPath);
            // Like the source FBX meshes, keep no CPU copy in the player.
            var serializedMesh = new SerializedObject(mesh);
            serializedMesh.FindProperty("m_IsReadable").boolValue = false;
            serializedMesh.ApplyModifiedPropertiesWithoutUndo();

            var child = new GameObject($"Merged_{index}_{key.Material.name}") { layer = key.Layer };
            child.transform.SetParent(mergedRoot.transform, false);
            Undo.RegisterCreatedObjectUndo(child, "Merge pallet stacks");
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var merged = child.AddComponent<MeshRenderer>();
            MeshRenderer template = templates[key];
            merged.sharedMaterial = key.Material;
            merged.shadowCastingMode = key.Shadows;
            merged.receiveShadows = key.ReceiveShadows;
            merged.lightProbeUsage = template.lightProbeUsage;
            merged.reflectionProbeUsage = template.reflectionProbeUsage;
            merged.motionVectorGenerationMode = template.motionVectorGenerationMode;
            merged.renderingLayerMask = template.renderingLayerMask;
            merged.allowOcclusionWhenDynamic = template.allowOcclusionWhenDynamic;
            merged.receiveGI = lightmapped ? ReceiveGI.Lightmaps : template.receiveGI;

            // ContributeGI keeps a future lighting rebake baking the merged mesh itself.
            StaticEditorFlags flags = StaticEditorFlags.OccludeeStatic;
            if (lightmapped) flags |= StaticEditorFlags.ContributeGI;
            if (IsOpaque(key.Material)) flags |= StaticEditorFlags.OccluderStatic;
            GameObjectUtility.SetStaticEditorFlags(child, flags);

            group.AddMerged(merged, key.LightmapIndex);
            index++;
        }
        group.SetSources(sources);
        // OnEnable already ran when the component was added, before it knew its renderers.
        group.ApplyLightmaps();

        foreach (Renderer source in sources)
        {
            Undo.RecordObject(source, "Merge pallet stacks");
            source.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(source);
        }

        return (sources.Count, index);
    }

    // Copies the parts into one mesh in the stack's local space. Lightmapped parts get their lightmap
    // scale/offset baked into UV channel 1 (or UV0 when a mesh has no channel 1), so the merged renderer
    // samples the same lightmap texels with a (1,1,0,0) scale/offset. shdr_basic_01 reads its textures
    // from UV channel 0 or 2 and never from channel 1, so rewriting channel 1 only affects the lightmap;
    // every other channel is copied unchanged.
    private const int FirstExtraUvChannel = 2, LastUvChannel = 7;

    private static Mesh BuildMergedMesh(List<MergePart> parts, bool lightmapped, string name)
    {
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var tangents = new List<Vector4>();
        var uv0 = new List<Vector2>();
        var uv1 = new List<Vector2>();
        var colors = new List<Color32>();
        var triangles = new List<int>();
        bool hasNormals = parts.All(p => p.Mesh.HasVertexAttribute(VertexAttribute.Normal));
        bool hasTangents = parts.All(p => p.Mesh.HasVertexAttribute(VertexAttribute.Tangent));
        bool hasColors = parts.Any(p => p.Mesh.HasVertexAttribute(VertexAttribute.Color));
        bool hasUv1 = lightmapped || parts.Any(p => p.Mesh.HasVertexAttribute(VertexAttribute.TexCoord1));
        var extraUvs = new Dictionary<int, List<Vector4>>();
        var extraUvDimensions = new Dictionary<int, int>();
        for (int channel = FirstExtraUvChannel; channel <= LastUvChannel; channel++)
        {
            var attribute = (VertexAttribute)((int)VertexAttribute.TexCoord0 + channel);
            int dimension = parts.Max(p => p.Mesh.HasVertexAttribute(attribute) ? p.Mesh.GetVertexAttributeDimension(attribute) : 0);
            if (dimension == 0) continue;
            extraUvs[channel] = new List<Vector4>();
            extraUvDimensions[channel] = dimension;
        }

        var cache = new Dictionary<Mesh, MeshArrays>();
        foreach (MergePart part in parts)
        {
            if (!cache.TryGetValue(part.Mesh, out MeshArrays source))
                cache[part.Mesh] = source = new MeshArrays(part.Mesh);

            Matrix4x4 m = part.ToMerged;
            Matrix4x4 normalMatrix = m.inverse.transpose;
            bool mirrored = m.determinant < 0f;
            Vector4 st = part.LightmapScaleOffset;
            var remap = new Dictionary<int, int>();
            int[] indices = part.Mesh.GetTriangles(part.SubMesh);

            int Vertex(int i)
            {
                if (remap.TryGetValue(i, out int mapped)) return mapped;
                mapped = positions.Count;
                remap[i] = mapped;
                positions.Add(m.MultiplyPoint3x4(source.Positions[i]));
                if (hasNormals) normals.Add(normalMatrix.MultiplyVector(source.Normals[i]).normalized);
                if (hasTangents)
                {
                    Vector4 t = source.Tangents[i];
                    Vector3 dir = m.MultiplyVector(new Vector3(t.x, t.y, t.z)).normalized;
                    tangents.Add(new Vector4(dir.x, dir.y, dir.z, mirrored ? -t.w : t.w));
                }
                uv0.Add(source.Uv0.Length > 0 ? source.Uv0[i] : Vector2.zero);
                if (hasUv1)
                {
                    Vector2 uv = source.Uv1.Length > 0 ? source.Uv1[i] : (source.Uv0.Length > 0 ? source.Uv0[i] : Vector2.zero);
                    uv1.Add(lightmapped ? new Vector2(uv.x * st.x + st.z, uv.y * st.y + st.w) : uv);
                }
                if (hasColors) colors.Add(source.Colors.Length > 0 ? source.Colors[i] : new Color32(255, 255, 255, 255));
                foreach (KeyValuePair<int, List<Vector4>> extra in extraUvs)
                {
                    List<Vector4> values = source.ExtraUv(extra.Key);
                    extra.Value.Add(values.Count > 0 ? values[i] : Vector4.zero);
                }
                return mapped;
            }

            for (int t = 0; t < indices.Length; t += 3)
            {
                int a = Vertex(indices[t]), b = Vertex(indices[t + 1]), c = Vertex(indices[t + 2]);
                // A mirrored transform flips the winding; swap to keep front faces facing out.
                if (mirrored) { triangles.Add(a); triangles.Add(c); triangles.Add(b); }
                else { triangles.Add(a); triangles.Add(b); triangles.Add(c); }
            }
        }

        var mesh = new Mesh { name = name };
        mesh.indexFormat = positions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(positions);
        if (hasNormals) mesh.SetNormals(normals);
        if (hasTangents) mesh.SetTangents(tangents);
        mesh.SetUVs(0, uv0);
        if (hasUv1) mesh.SetUVs(1, uv1);
        if (hasColors) mesh.SetColors(colors);
        foreach (KeyValuePair<int, List<Vector4>> extra in extraUvs)
        {
            if (extraUvDimensions[extra.Key] <= 2) mesh.SetUVs(extra.Key, extra.Value.Select(v => (Vector2)v).ToList());
            else if (extraUvDimensions[extra.Key] == 3) mesh.SetUVs(extra.Key, extra.Value.Select(v => (Vector3)v).ToList());
            else mesh.SetUVs(extra.Key, extra.Value);
        }
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.Optimize();
        return mesh;
    }

    private sealed class MeshArrays
    {
        public readonly Vector3[] Positions, Normals;
        public readonly Vector4[] Tangents;
        public readonly Vector2[] Uv0, Uv1;
        public readonly Color32[] Colors;
        private readonly Mesh _mesh;
        private readonly Dictionary<int, List<Vector4>> _extraUvs = new Dictionary<int, List<Vector4>>();

        public MeshArrays(Mesh mesh)
        {
            _mesh = mesh;
            Positions = mesh.vertices;
            Normals = mesh.normals;
            Tangents = mesh.tangents;
            Uv0 = mesh.uv;
            Uv1 = mesh.uv2;
            Colors = mesh.colors32;
        }

        // UV channel 2 and up, as Vector4 whatever their dimension; empty if the mesh lacks the channel.
        public List<Vector4> ExtraUv(int channel)
        {
            if (_extraUvs.TryGetValue(channel, out List<Vector4> values)) return values;
            values = new List<Vector4>();
            if (_mesh.HasVertexAttribute((VertexAttribute)((int)VertexAttribute.TexCoord0 + channel)))
                _mesh.GetUVs(channel, values);
            _extraUvs[channel] = values;
            return values;
        }
    }

    [MenuItem(MenuRoot + "1a. Reapply Merged Lightmaps", priority = 21)]
    public static void ReapplyMergedLightmaps()
    {
        MergedMeshGroup[] groups = Object.FindObjectsByType<MergedMeshGroup>(FindObjectsSortMode.None);
        foreach (MergedMeshGroup group in groups) group.ApplyLightmaps();
        SceneView.RepaintAll();
        Debug.Log(LogPrefix + $"Reapplied lightmaps on {groups.Length} merged pallet stacks.");
    }

    [MenuItem(MenuRoot + "1b. Unmerge Pallet Stacks", priority = 22)]
    public static void UnmergePalletStacks()
    {
        Undo.SetCurrentGroupName("Unmerge pallet stacks");
        int undoGroup = Undo.GetCurrentGroup();
        var assetPaths = new HashSet<string>();
        int stacks = 0;

        foreach (MergedMeshGroup group in Object.FindObjectsByType<MergedMeshGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            foreach (Renderer source in group.SourceRenderers)
            {
                if (source == null) continue;
                Undo.RecordObject(source, "Unmerge pallet stacks");
                source.enabled = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(source);
            }
            foreach (MeshRenderer merged in group.MergedRenderers)
            {
                Mesh mesh = merged != null ? merged.GetComponent<MeshFilter>().sharedMesh : null;
                if (mesh != null) assetPaths.Add(AssetDatabase.GetAssetPath(mesh));
            }
            Undo.DestroyObjectImmediate(group.gameObject);
            stacks++;
        }

        // Asset deletion is not undoable, so only do it once nothing references the meshes any more.
        foreach (string path in assetPaths.Where(p => !string.IsNullOrEmpty(p)))
            AssetDatabase.DeleteAsset(path);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log(LogPrefix + $"Unmerged {stacks} pallet stacks and deleted {assetPaths.Count} merged mesh assets.");
    }

    // ---------------------------------------------------------------- 2. Occlusion culling

    [MenuItem(MenuRoot + "2. Mark Occlusion Static", priority = 40)]
    public static void MarkOcclusionStatic()
    {
        Scene scene = SceneManager.GetActiveScene();
        Undo.SetCurrentGroupName("Mark occlusion static");
        int undoGroup = Undo.GetCurrentGroup();
        var movableCache = new Dictionary<Transform, bool>();
        int occluders = 0, occludees = 0, movable = 0;

        foreach (Renderer r in ActiveRenderers(scene))
        {
            if (r is SkinnedMeshRenderer || IsMovable(r.transform, movableCache)) { movable++; continue; }

            GameObject go = r.gameObject;
            StaticEditorFlags before = GameObjectUtility.GetStaticEditorFlags(go);
            StaticEditorFlags after = before | StaticEditorFlags.OccludeeStatic;
            bool occluder = r.sharedMaterials.All(IsOpaque) && r.bounds.size.magnitude >= 0.3f;
            if (occluder) after |= StaticEditorFlags.OccluderStatic;

            if (occluder) occluders++;
            occludees++;
            if (after == before) continue;
            Undo.RecordObject(go, "Mark occlusion static");
            GameObjectUtility.SetStaticEditorFlags(go, after);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go);
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(LogPrefix + $"Occlusion static: {occludees} occludees, {occluders} of them occluders; " +
                  $"skipped {movable} movable/animated/UI renderers.");
    }

    [MenuItem(MenuRoot + "3. Bake Occlusion Culling", priority = 41)]
    public static void BakeOcclusion()
    {
        // Indoor scene with shelving: a 1 m smallest occluder keeps racks and stacks effective,
        // a 0.15 m smallest hole keeps the gaps between boxes see-through.
        StaticOcclusionCulling.smallestOccluder = 1f;
        StaticOcclusionCulling.smallestHole = 0.15f;
        StaticOcclusionCulling.backfaceThreshold = 100f;
        if (!StaticOcclusionCulling.GenerateInBackground())
        {
            Debug.LogError(LogPrefix + "Occlusion bake did not start.");
            return;
        }

        double started = EditorApplication.timeSinceStartup;
        Debug.Log(LogPrefix + "Occlusion bake started.");
        void Poll()
        {
            if (StaticOcclusionCulling.isRunning) return;
            EditorApplication.update -= Poll;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log(LogPrefix + $"Occlusion bake finished in {EditorApplication.timeSinceStartup - started:F0} s, " +
                      $"{StaticOcclusionCulling.umbraDataSize:N0} bytes. Save the scene to keep it.");
        }
        EditorApplication.update += Poll;
    }

    [MenuItem(MenuRoot + "3b. Clear Occlusion Data", priority = 42)]
    public static void ClearOcclusion()
    {
        StaticOcclusionCulling.Clear();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log(LogPrefix + "Occlusion data cleared.");
    }

    private static bool IsMovable(Transform t, Dictionary<Transform, bool> cache)
    {
        if (t == null) return false;
        if (cache.TryGetValue(t, out bool movable)) return movable;

        movable = MovableComponentTypes.Any(type => t.GetComponent(type) != null) ||
                  t.GetComponents<MonoBehaviour>().Any(c => c != null && IsInteractive(c.GetType())) ||
                  IsMovable(t.parent, cache);
        cache[t] = movable;
        return movable;
    }

    // AutoHand components (grabbables, hands, player) and the tutorial triggers attached to props.
    private static bool IsInteractive(Type type) =>
        (type.Namespace != null && type.Namespace.StartsWith("Autohand")) || type.Name == "ChallengeTutorialTrigger";

    // ---------------------------------------------------------------- 4. UI raycasters

    [MenuItem(MenuRoot + "4. Strip Unused UI Raycasters", priority = 60)]
    public static void StripUnusedRaycasters()
    {
        Undo.SetCurrentGroupName("Strip unused UI raycasters");
        int undoGroup = Undo.GetCurrentGroup();
        var stripped = new List<string>();
        int graphicsChanged = 0;

        foreach (GraphicRaycaster raycaster in Object.FindObjectsByType<GraphicRaycaster>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (raycaster.gameObject.scene != SceneManager.GetActiveScene()) continue;
            // Anything clickable, scrollable or draggable (Selectables, ScrollRect, EventTrigger, custom
            // pointer handlers) implements IEventSystemHandler; keep the raycaster for those canvases.
            bool interactive = raycaster.GetComponentsInChildren<MonoBehaviour>(true)
                .Any(c => c is IEventSystemHandler && !(c is BaseRaycaster));
            if (interactive) continue;

            foreach (Graphic graphic in raycaster.GetComponentsInChildren<Graphic>(true))
            {
                if (!graphic.raycastTarget) continue;
                Undo.RecordObject(graphic, "Strip unused UI raycasters");
                graphic.raycastTarget = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(graphic);
                graphicsChanged++;
            }
            stripped.Add(HierarchyPath(raycaster.transform));
            Undo.DestroyObjectImmediate(raycaster);
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log(LogPrefix + $"Removed {stripped.Count} GraphicRaycasters from canvases with nothing to click " +
                  $"({graphicsChanged} graphics no longer raycast targets):\n  " + string.Join("\n  ", stripped));
    }

    // ---------------------------------------------------------------- 5. Mesh LOD

    [MenuItem(MenuRoot + "5. Mesh LOD On Heavy Models", priority = 80)]
    public static void MeshLodOnHeavyModels()
    {
        Scene scene = SceneManager.GetActiveScene();
        List<Renderer> heavy = ActiveRenderers(scene)
            .Where(r => r is MeshRenderer && SharedMesh(r) != null && Triangles(r, SharedMesh(r), 0) >= HeavyMeshTriangles)
            .ToList();

        var importers = new HashSet<ModelImporter>();
        foreach (Renderer r in heavy)
            if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(SharedMesh(r))) is ModelImporter importer && !importer.generateMeshLods)
                importers.Add(importer);

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (ModelImporter importer in importers)
            {
                importer.generateMeshLods = true;
                importer.SaveAndReimport();
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        // "Lower detail even up close": force each heavy renderer to the most detailed Mesh LOD that
        // fits its triangle budget, so it stays cheap at any distance.
        Undo.SetCurrentGroupName("Force Mesh LOD");
        int undoGroup = Undo.GetCurrentGroup();
        var sb = new StringBuilder(LogPrefix + $"Mesh LOD enabled on {importers.Count} models; forced levels:\n");
        foreach (Renderer r in heavy)
        {
            Mesh mesh = SharedMesh(r);
            if (mesh == null || mesh.lodCount <= 1)
            {
                sb.AppendLine($"  {HierarchyPath(r.transform)}: no LODs generated ({mesh?.name})");
                continue;
            }

            long full = Triangles(r, mesh, 0);
            int budget = r.bounds.size.magnitude < SmallPropSize ? SmallPropTriangleTarget : LargeMeshTriangleTarget;
            int level = mesh.lodCount - 1;
            for (int l = 1; l < mesh.lodCount; l++)
                if (Triangles(r, mesh, l) <= budget) { level = l; break; }
            if (MaxForcedLod.TryGetValue(mesh.name, out int maxLevel)) level = Mathf.Min(level, maxLevel);

            Undo.RecordObject(r, "Force Mesh LOD");
            r.forceMeshLod = (short)level;
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            sb.AppendLine($"  {HierarchyPath(r.transform)}: {full:N0} -> {Triangles(r, mesh, level):N0} tris (LOD {level}/{mesh.lodCount - 1})");
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(sb.ToString());
    }

    [MenuItem(MenuRoot + "5b. Clear Forced Mesh LOD", priority = 81)]
    public static void ClearForcedMeshLod()
    {
        Undo.SetCurrentGroupName("Clear forced Mesh LOD");
        int undoGroup = Undo.GetCurrentGroup();
        int cleared = 0;
        foreach (Renderer r in ActiveRenderers(SceneManager.GetActiveScene()).Where(r => r.forceMeshLod >= 0))
        {
            Undo.RecordObject(r, "Clear forced Mesh LOD");
            r.forceMeshLod = -1;
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            cleared++;
        }
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log(LogPrefix + $"Cleared forced Mesh LOD on {cleared} renderers (automatic selection again).");
    }

    // ---------------------------------------------------------------- 6. Textures

    [MenuItem(MenuRoot + "6. Cap Android Textures At 2048", priority = 100)]
    public static void CapAndroidTextures()
    {
        string scenePath = SceneManager.GetActiveScene().path;
        var capped = new List<string>();

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string path in AssetDatabase.GetDependencies(scenePath, true))
            {
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
                int effectiveMax = android.overridden ? android.maxTextureSize : importer.maxTextureSize;
                if (Mathf.Max(width, height) <= AndroidTextureMaxSize || effectiveMax <= AndroidTextureMaxSize) continue;

                if (!android.overridden)
                {
                    // First Android override: start from the default settings so only the size changes.
                    TextureImporterPlatformSettings defaults = importer.GetDefaultPlatformTextureSettings();
                    android.format = TextureImporterFormat.Automatic;
                    android.textureCompression = defaults.textureCompression;
                    android.compressionQuality = defaults.compressionQuality;
                    android.crunchedCompression = defaults.crunchedCompression;
                    android.resizeAlgorithm = defaults.resizeAlgorithm;
                    android.overridden = true;
                }
                android.maxTextureSize = AndroidTextureMaxSize;
                importer.SetPlatformTextureSettings(android);
                importer.SaveAndReimport();
                capped.Add($"{path} ({width}x{height}, was {effectiveMax})");
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        Debug.Log(LogPrefix + $"Capped {capped.Count} textures at {AndroidTextureMaxSize} for Android:\n  " + string.Join("\n  ", capped));
    }

    // ---------------------------------------------------------------- Helpers

    private static List<Renderer> ActiveRenderers(Scene scene)
    {
        var renderers = new List<Renderer>();
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(false))
                if (r.enabled && (r is MeshRenderer || r is SkinnedMeshRenderer))
                    renderers.Add(r);
        return renderers;
    }

    private static Mesh SharedMesh(Renderer r)
    {
        if (r is SkinnedMeshRenderer skinned) return skinned.sharedMesh;
        var filter = r.GetComponent<MeshFilter>();
        return filter != null ? filter.sharedMesh : null;
    }

    // Unity draws submesh i with material i; extra materials redraw the last submesh.
    private static int DrawCount(Renderer r, Mesh mesh) => Mathf.Max(1, r.sharedMaterials.Length);

    private static long Triangles(Renderer r, Mesh mesh, int lodOverride = -1)
    {
        if (mesh.subMeshCount == 0) return 0;
        int materials = Mathf.Max(1, r.sharedMaterials.Length);
        int level = lodOverride >= 0 ? lodOverride : Mathf.Max(0, (int)r.forceMeshLod);
        long total = 0;
        for (int m = 0; m < materials; m++)
        {
            int subMesh = Mathf.Min(m, mesh.subMeshCount - 1);
            total += (mesh.lodCount > 1
                ? mesh.GetLod(subMesh, Mathf.Min(level, mesh.lodCount - 1)).indexCount
                : mesh.GetIndexCount(subMesh)) / 3;
        }
        return total;
    }

    private static bool IsOpaque(Material material) =>
        material != null && material.renderQueue < (int)RenderQueue.AlphaTest;

    private static List<GameObject> PrefabInstanceRoots(Scene scene, string[] prefabPaths)
    {
        var roots = new List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) &&
                    prefabPaths.Contains(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)))
                    roots.Add(t.gameObject);
        return roots;
    }

    private static string EnsureFolder(string folder)
    {
        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
        return folder;
    }

    private static string Sanitize(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    private static string HierarchyPath(Transform t) =>
        t.parent == null ? t.name : HierarchyPath(t.parent) + "/" + t.name;
}
