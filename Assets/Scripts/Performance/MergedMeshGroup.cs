using System.Collections.Generic;
using UnityEngine;

// Parent of the meshes that Tools > Quest Performance > Merge Pallet Stacks built from one pallet stack.
// It keeps the source renderers (disabled, not deleted) so the merge can be undone, and it reapplies the
// baked lightmap to each merged renderer. The merged mesh's UV2 already holds every part's lightmap
// scale/offset, so only the lightmap index is needed. The scene's lighting data has no entry for the new
// renderers, so without this they would render unlit.
[ExecuteAlways]
public class MergedMeshGroup : MonoBehaviour
{
    [SerializeField] private List<Renderer> _sourceRenderers = new List<Renderer>();
    [SerializeField] private List<MeshRenderer> _mergedRenderers = new List<MeshRenderer>();
    [SerializeField] private List<int> _lightmapIndices = new List<int>();

    public IReadOnlyList<Renderer> SourceRenderers => _sourceRenderers;
    public IReadOnlyList<MeshRenderer> MergedRenderers => _mergedRenderers;

    public void SetSources(List<Renderer> sources) => _sourceRenderers = sources;

    public void AddMerged(MeshRenderer merged, int lightmapIndex)
    {
        _mergedRenderers.Add(merged);
        _lightmapIndices.Add(lightmapIndex);
    }

    private void OnEnable() => ApplyLightmaps();

    private void Start() => ApplyLightmaps();

    public void ApplyLightmaps()
    {
        int lightmapCount = LightmapSettings.lightmaps.Length;
        for (int i = 0; i < _mergedRenderers.Count; i++)
        {
            MeshRenderer merged = _mergedRenderers[i];
            int index = _lightmapIndices[i];
            if (merged == null || index < 0 || index >= lightmapCount) continue;

            // A lighting rebake that includes this renderer gives it a lightmap of its own; keep that one.
            if (IsLightmapped(merged)) continue;

            merged.lightmapIndex = index;
            merged.lightmapScaleOffset = new Vector4(1f, 1f, 0f, 0f);
        }
    }

    // 0xFFFE and 0xFFFF mean "no lightmap assigned".
    public static bool IsLightmapped(Renderer renderer) =>
        renderer.lightmapIndex >= 0 && renderer.lightmapIndex < 0xFFFE;
}
