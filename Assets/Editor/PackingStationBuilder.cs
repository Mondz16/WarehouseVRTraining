using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autohand;
using ImmersiveTraining.TrainingInteractions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Valari.Data;
using Valari.Managers;
using Valari.UI;

/// <summary>
/// Builds Module 2 (Packing and Dispatch): prefabs, the packing station in BasicScene and its wiring to the M2
/// tutorial panels; also restores the M1_09 assessment panel. Re-runnable: "Build Station" replaces the previous
/// M2_PackingStation and QC panel (manual tweaks to them are lost) and re-creates the M2 trigger objects.
/// Each step logs a [PackingBuilder] summary. Spec: docs/superpowers/specs/2026-10-06-packing-dispatch-module-design.md
/// </summary>
public static class PackingStationBuilder
{
    private const string Folder = "Assets/Prefabs/PackingDispatch";
    private const string MaterialFolder = Folder + "/Materials";
    private const string StationName = "M2_PackingStation";
    private const string QcPanelName = "M2_QCPanel";
    private const string PanelGroupName = "PackingAndDispatch";
    private const string LabelSku = "LBL-ORD-2001";
    private const float Wall = PackingCarton.WallThickness;

    private static readonly Color Ink = new Color(0.08f, 0.08f, 0.1f);

    private const string Order1Label =
        "SHIP TO: R. OKAFOR\n18 WHARF RD, PORT KEMBLA NSW 2505\nORD-2001   WT CLASS B (1-2 kg)\nCARRIER: AXE - AXL EXPRESS";

    private const string QcLabel =
        "SHIP TO: T. NGUYEN\n9 BAYVIEW TCE, HOBART TAS 7000\nORD-1874   WT CLASS B (1-2 kg)\nCARRIER: AXE - AXL EXPRESS";

    private const string Order1Panel =
        "ORDER ORD-2001  (1 item)\n" +
        "Wireless Speaker   24 x 14 x 10 cm   1.2 kg\n" +
        "Ship to: R. Okafor, 18 Wharf Rd, Port Kembla NSW 2505\n" +
        "Weight class B (1-2 kg)    Carrier: AXE (AXL Express)\n" +
        "Cartons: S 30x20x15   M 45x35x25   L 60x50x40 cm";

    private const string Order2Panel =
        "ORDER ORD-2002  (3 items)\n" +
        "Desk Lamp 30x12x12 | Book Set 24x17x8 | Cable Kit 15x10x6 cm\n" +
        "Packed together: 32 x 30 x 14 cm   2.9 kg\n" +
        "Ship to: M. Delacroix, 4/77 Ocean St, Narrabeen NSW 2101\n" +
        "Weight class C (2-5 kg)    Carrier: AXE (AXL Express)\n" +
        "Cartons: S 30x20x15   M 45x35x25   L 60x50x40 cm";

    private const string QcOrderText =
        "Ceramic Mug Set 20 x 20 x 12 cm, 1.6 kg - Medium carton\n" +
        "Ship to: T. Nguyen, 9 Bayview Tce, Hobart TAS 7000\n" +
        "Weight class B (1-2 kg)    Carrier: AXE (AXL Express)";

    // Panel -> challenge trigger names, as in Assets/ReferenceData/training_flow_module2.json.
    private static readonly (string Panel, string[] Triggers)[] M2Steps =
    {
        ("M2_02_Station_Layout", new[] { "TRIGGER_M2_StationLayoutConfirmed" }),
        ("M2_03_Box_Selection", new[] { "TRIGGER_M2_CorrectBoxSelected" }),
        ("M2_04_Void_Fill_Application", new[] { "TRIGGER_M2_VoidFillBase", "TRIGGER_M2_VoidFillTop" }),
        ("M2_05_Sealing_the_Box", new[] { "TRIGGER_M2_BoxSealed" }),
        ("M2_06_Label_Verification", new[] { "TRIGGER_M2_LabelVerified", "TRIGGER_M2_LabelApplied" }),
        ("M2_07_Conveyor_Loading", new[] { "TRIGGER_M2_BoxOnConveyor" }),
        ("M2_08_Multi_Item_Consolidation", new[] { "TRIGGER_M2_MultiItemPacked" }),
        ("M2_09_Error_Identification", new[] { "TRIGGER_M2_ErrorIdentified", "TRIGGER_M2_ErrorCorrected" }),
        ("M2_10_Assessment", new[] { "TRIGGER_M2_AssessmentComplete" }),
    };

    private static readonly string[] M2PanelNames =
    {
        "M2_01_Welcome_Packing", "M2_02_Station_Layout", "M2_03_Box_Selection", "M2_04_Void_Fill_Application",
        "M2_05_Sealing_the_Box", "M2_06_Label_Verification", "M2_07_Conveyor_Loading", "M2_08_Multi_Item_Consolidation",
        "M2_09_Error_Identification", "M2_10_Assessment", "M2_11_Module_Complete",
    };

    // Modal poses (world). Trainee faces +x at the bench, so a panel's rotation is the direction the trainee looks.
    private static readonly Vector3 StationModalPosition = new Vector3(16.35f, 1.45f, 14.75f);
    private static readonly Quaternion StationModalRotation = Quaternion.Euler(0f, 35f, 0f);
    private static readonly Vector3 QcModalPosition = new Vector3(16.3f, 1.45f, 8.95f);
    private static readonly Quaternion QcModalRotation = Quaternion.Euler(0f, 134f, 0f);

    private static Dictionary<string, Material> _materials;

    // =====================================================================================================
    // 1. Prefabs
    // =====================================================================================================

    [MenuItem("Tools/Packing Dispatch/1. Build Prefabs")]
    public static void BuildPrefabs()
    {
        LoadMaterials();
        EnsureFolder(Folder);

        foreach (CartonSize size in new[] { CartonSize.Small, CartonSize.Medium, CartonSize.Large })
            BuildCarton(size);

        BuildProduct("Speaker", "Wireless Speaker", new Vector3(24f, 10f, 14f), 1.2f, _materials["ProductBlue"]);
        BuildProduct("DeskLamp", "Desk Lamp", new Vector3(30f, 12f, 12f), 1.4f, _materials["ProductOrange"]);
        BuildProduct("BookSet", "Book Set", new Vector3(24f, 8f, 17f), 1.2f, _materials["ProductPurple"]);
        BuildProduct("CableKit", "Cable Kit", new Vector3(15f, 6f, 10f), 0.3f, _materials["ProductGreen"]);
        BuildPillow();
        BuildLabel();
        BuildTapeGun();
        BuildBoxCutter();

        AssetDatabase.SaveAssets();
        Debug.Log($"[PackingBuilder] Prefabs built in {Folder}: 3 cartons, 4 products, air pillow, shipping label, tape gun, box cutter.");
    }

    private static void BuildCarton(CartonSize size)
    {
        Size3 cm = CartonFit.InnerSize(size);
        float l = cm.X * 0.01f, h = cm.Y * 0.01f, w = cm.Z * 0.01f, t = Wall, top = h + 2f * t;
        var root = new GameObject($"Carton_{size}");
        try
        {
            Material board = _materials["Cardboard"], dark = _materials["CardboardDark"];
            Transform walls = Empty("Walls", root.transform, Vector3.zero).transform;
            Box("Bottom", walls, new Vector3(0f, t / 2f, 0f), new Vector3(l + 2f * t, t, w + 2f * t), board, true);
            Box("Side_-Z", walls, new Vector3(0f, t + h / 2f, -(w / 2f + t / 2f)), new Vector3(l + 2f * t, h, t), board, true);
            Box("Side_+Z", walls, new Vector3(0f, t + h / 2f, w / 2f + t / 2f), new Vector3(l + 2f * t, h, t), board, true);
            Box("End_-X", walls, new Vector3(-(l / 2f + t / 2f), t + h / 2f, 0f), new Vector3(t, h, w), board, true);
            Box("End_+X", walls, new Vector3(l / 2f + t / 2f, t + h / 2f, 0f), new Vector3(t, h, w), board, true);

            GameObject flaps = Empty("OpenFlaps", root.transform, Vector3.zero);
            Flap(flaps.transform, "Flap_-Z", new Vector3(0f, h + t, -(w / 2f + t)), new Vector3(0f, 0.866f, -0.5f), l + 2f * t, w / 2f, dark);
            Flap(flaps.transform, "Flap_+Z", new Vector3(0f, h + t, w / 2f + t), new Vector3(0f, 0.866f, 0.5f), l + 2f * t, w / 2f, dark);
            Flap(flaps.transform, "Flap_-X", new Vector3(-(l / 2f + t), h + t, 0f), new Vector3(-0.5f, 0.866f, 0f), w, Mathf.Min(l, w) * 0.4f, dark);
            Flap(flaps.transform, "Flap_+X", new Vector3(l / 2f + t, h + t, 0f), new Vector3(0.5f, 0.866f, 0f), w, Mathf.Min(l, w) * 0.4f, dark);

            GameObject lid = Empty("Lid", root.transform, Vector3.zero);
            Box("LidPanel", lid.transform, new Vector3(0f, h + 1.5f * t, 0f), new Vector3(l + 2f * t, t, w + 2f * t), board, true);
            Box("FlapJoint", lid.transform, new Vector3(0f, top + 0.0004f, 0f), new Vector3(l, 0.0008f, 0.003f), _materials["CardboardSeam"]);
            lid.SetActive(false);

            GameObject fill = Box("Fill", root.transform, new Vector3(0f, t, 0f), new Vector3(l - 0.01f, 0.001f, w - 0.01f), _materials["Pillow"]);
            fill.SetActive(false);

            Transform tape = Empty("Tape", root.transform, Vector3.zero).transform;
            Quaternion across = Quaternion.Euler(0f, 90f, 0f);
            var strips = new Object[]
            {
                Box("Tape_Centre", tape, new Vector3(0f, top + 0.0015f, 0f), new Vector3(l, 0.002f, 0.048f), _materials["Tape"]).transform,
                Box("Tape_EndA", tape, new Vector3(-l / 2f, top + 0.0015f, 0f), new Vector3(w, 0.002f, 0.048f), _materials["Tape"], false, across).transform,
                Box("Tape_EndB", tape, new Vector3(l / 2f, top + 0.0015f, 0f), new Vector3(w, 0.002f, 0.048f), _materials["Tape"], false, across).transform,
            };
            var guides = new Object[]
            {
                Box("Guide_Centre", tape, new Vector3(0f, top + 0.0008f, 0f), new Vector3(l, 0.001f, 0.06f), _materials["Guide"]),
                Box("Guide_EndA", tape, new Vector3(-l / 2f, top + 0.0008f, 0f), new Vector3(w, 0.001f, 0.06f), _materials["Guide"], false, across),
                Box("Guide_EndB", tape, new Vector3(l / 2f, top + 0.0008f, 0f), new Vector3(w, 0.001f, 0.06f), _materials["Guide"], false, across),
            };
            foreach (Object o in strips) ((Transform)o).gameObject.SetActive(false);
            foreach (Object o in guides) ((GameObject)o).SetActive(false);

            GameObject label = Empty("LabelVisual", root.transform, new Vector3(l * 0.22f, top + 0.002f, 0f));
            LabelFace(label.transform, Order1Label);
            label.SetActive(false);

            Transform contents = Empty("Contents", root.transform, Vector3.zero).transform;

            string mark = $"{size.ToString().ToUpperInvariant()}\n{cm.X:0}x{cm.Z:0}x{cm.Y:0}";
            float markSize = Mathf.Clamp(h * 2.2f, 0.3f, 0.6f);
            Text3D("SizeMark_-X", root.transform, new Vector3(-(l / 2f + t + 0.002f), t + h / 2f, 0f), Quaternion.Euler(0f, 90f, 0f), mark, markSize, new Vector2(w * 0.9f, h * 0.8f), Ink);
            Text3D("SizeMark_+X", root.transform, new Vector3(l / 2f + t + 0.002f, t + h / 2f, 0f), Quaternion.Euler(0f, -90f, 0f), mark, markSize, new Vector2(w * 0.9f, h * 0.8f), Ink);

            float mass = size == CartonSize.Small ? 0.4f : size == CartonSize.Medium ? 0.7f : 1f;
            Grabbable grab = AddGrabbable(root, mass, false);
            grab.heldNoFriction = false;
            PackingCarton carton = root.AddComponent<PackingCarton>();
            Set(carton, "_size", (int)size);
            Set(carton, "_innerSize", new Vector3(l, h, w));
            Set(carton, "_grabbable", grab);
            Set(carton, "_body", root.GetComponent<Rigidbody>());
            Set(carton, "_openFlaps", flaps);
            Set(carton, "_lid", lid);
            Set(carton, "_fillVisual", fill.transform);
            SetArray(carton, "_tapeStrips", strips);
            SetArray(carton, "_seamGuides", guides);
            Set(carton, "_labelVisual", label);
            Set(carton, "_contentsRoot", contents);

            PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/Carton_{size}.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void Flap(Transform parent, string name, Vector3 hinge, Vector3 direction, float across, float depth, Material material)
    {
        Vector3 dir = direction.normalized;
        Box(name, parent, hinge + dir * depth / 2f, new Vector3(across, Wall, depth), material, false, Quaternion.LookRotation(dir, Vector3.up));
    }

    private static void BuildProduct(string id, string display, Vector3 sizeCm, float mass, Material material)
    {
        var root = new GameObject($"Product_{id}");
        try
        {
            Vector3 s = sizeCm * 0.01f;
            Box("Box", root.transform, Vector3.zero, s, material);
            Text3D("Name_Top", root.transform, new Vector3(0f, s.y / 2f + 0.001f, 0f), Quaternion.Euler(90f, 0f, 0f), display, 0.3f, new Vector2(s.x * 0.9f, s.z * 0.8f), Color.white);
            Text3D("Name_-Z", root.transform, new Vector3(0f, 0f, -(s.z / 2f + 0.001f)), Quaternion.identity, display, 0.3f, new Vector2(s.x * 0.9f, s.y * 0.8f), Color.white);
            Text3D("Name_+Z", root.transform, new Vector3(0f, 0f, s.z / 2f + 0.001f), Quaternion.Euler(0f, 180f, 0f), display, 0.3f, new Vector2(s.x * 0.9f, s.y * 0.8f), Color.white);

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = s;
            Grabbable grab = AddGrabbable(root, mass, true);
            PackableItem item = root.AddComponent<PackableItem>();
            Set(item, "_displayName", display);
            Set(item, "_sizeCm", sizeCm);
            Set(item, "_grabbable", grab);
            Set(item, "_body", root.GetComponent<Rigidbody>());

            PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/Product_{id}.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void BuildPillow()
    {
        var root = new GameObject("AirPillow");
        try
        {
            Prim(PrimitiveType.Sphere, "Pillow", root.transform, Vector3.zero, new Vector3(0.11f, 0.045f, 0.085f), _materials["Pillow"]);
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.1f, 0.04f, 0.08f);
            Grabbable grab = AddGrabbable(root, 0.03f, true);
            VoidFillPiece piece = root.AddComponent<VoidFillPiece>();
            Set(piece, "_grabbable", grab);
            Set(piece, "_body", root.GetComponent<Rigidbody>());
            PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/AirPillow.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void BuildLabel()
    {
        var root = new GameObject("ShippingLabel_ORD-2001");
        try
        {
            LabelFace(root.transform, Order1Label);
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.15f, 0.012f, 0.1f);
            Grabbable grab = AddGrabbable(root, 0.02f, true);
            SkuItem sku = root.AddComponent<SkuItem>();
            Set(sku, "_sku", LabelSku);
            Set(sku, "_description", "Shipping label ORD-2001");
            ShippingLabel label = root.AddComponent<ShippingLabel>();
            Set(label, "_grabbable", grab);
            Set(label, "_body", root.GetComponent<Rigidbody>());
            PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/ShippingLabel_ORD-2001.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>A 15 x 10 cm white label lying flat (text up, reading along +X), barcode along the -Z edge.</summary>
    private static void LabelFace(Transform parent, string text)
    {
        Box("Card", parent, Vector3.zero, new Vector3(0.15f, 0.003f, 0.1f), _materials["LabelPaper"]);
        TextMeshPro tmp = Text3D("Text", parent, new Vector3(0f, 0.0018f, 0.012f), Quaternion.Euler(90f, 0f, 0f), text, 0.09f, new Vector2(0.14f, 0.065f), Ink, TextAlignmentOptions.TopLeft);
        tmp.fontSizeMin = 0.03f;
        float[] widths = { 0.002f, 0.001f, 0.003f, 0.001f, 0.002f, 0.004f, 0.001f, 0.002f, 0.001f, 0.003f, 0.002f, 0.001f, 0.003f, 0.001f, 0.002f, 0.004f, 0.001f, 0.002f };
        float x = -0.05f;
        foreach (float width in widths)
        {
            Box("Bar", parent, new Vector3(x + width / 2f, 0.0017f, -0.035f), new Vector3(width, 0.0004f, 0.02f), _materials["Ink"]);
            x += width + 0.0025f;
        }
    }

    private static void BuildTapeGun()
    {
        var root = new GameObject("TapeGun");
        try
        {
            Box("Body", root.transform, new Vector3(0f, 0.05f, 0f), new Vector3(0.035f, 0.035f, 0.17f), _materials["ToolRed"]);
            Box("Handle", root.transform, new Vector3(0f, 0f, -0.055f), new Vector3(0.03f, 0.11f, 0.035f), _materials["ToolRed"], false, Quaternion.Euler(-15f, 0f, 0f));
            Prim(PrimitiveType.Cylinder, "TapeRoll", root.transform, new Vector3(0f, 0.07f, 0.02f), new Vector3(0.1f, 0.025f, 0.1f), _materials["Tape"], false, Quaternion.Euler(0f, 0f, 90f));
            Box("Head", root.transform, new Vector3(0f, -0.005f, 0.08f), new Vector3(0.05f, 0.02f, 0.03f), _materials["ToolGrey"]);
            GameObject head = Empty("HeadPoint", root.transform, new Vector3(0f, -0.02f, 0.09f));
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.03f, 0.01f);
            collider.size = new Vector3(0.06f, 0.14f, 0.2f);
            Grabbable grab = AddGrabbable(root, 0.5f, true);
            SeamTool tool = root.AddComponent<SeamTool>();
            Set(tool, "_kind", (int)SeamToolKind.Tape);
            Set(tool, "_head", head.transform);
            Set(tool, "_grabbable", grab);
            PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/TapeGun.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void BuildBoxCutter()
    {
        var root = new GameObject("BoxCutter");
        try
        {
            Box("Body", root.transform, Vector3.zero, new Vector3(0.028f, 0.018f, 0.13f), _materials["CutterYellow"]);
            Box("Blade", root.transform, new Vector3(0f, -0.002f, 0.078f), new Vector3(0.003f, 0.012f, 0.026f), _materials["Steel"]);
            GameObject head = Empty("HeadPoint", root.transform, new Vector3(0f, -0.006f, 0.09f));
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0f, 0.015f);
            collider.size = new Vector3(0.03f, 0.02f, 0.16f);
            Grabbable grab = AddGrabbable(root, 0.15f, true);
            SeamTool tool = root.AddComponent<SeamTool>();
            Set(tool, "_kind", (int)SeamToolKind.Cutter);
            Set(tool, "_head", head.transform);
            Set(tool, "_grabbable", grab);
            PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/BoxCutter.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // =====================================================================================================
    // 2. Station + M2 wiring
    // =====================================================================================================

    [MenuItem("Tools/Packing Dispatch/2. Build Station + Wire M2")]
    public static void BuildStation()
    {
        LoadMaterials();
        var cartons = new Dictionary<CartonSize, PackingCarton>();
        foreach (CartonSize size in new[] { CartonSize.Small, CartonSize.Medium, CartonSize.Large })
            cartons[size] = LoadPrefab<PackingCarton>($"Carton_{size}");
        PackableItem speaker = LoadPrefab<PackableItem>("Product_Speaker");
        PackableItem lamp = LoadPrefab<PackableItem>("Product_DeskLamp");
        PackableItem books = LoadPrefab<PackableItem>("Product_BookSet");
        PackableItem cables = LoadPrefab<PackableItem>("Product_CableKit");
        VoidFillPiece pillow = LoadPrefab<VoidFillPiece>("AirPillow");
        ShippingLabel labelPrefab = LoadPrefab<ShippingLabel>("ShippingLabel_ORD-2001");
        SeamTool tapeGunPrefab = LoadPrefab<SeamTool>("TapeGun");
        SeamTool cutterPrefab = LoadPrefab<SeamTool>("BoxCutter");
        if (cartons.Values.Any(c => c == null) || new Object[] { speaker, lamp, books, cables, pillow, labelPrefab, tapeGunPrefab, cutterPrefab }.Any(o => o == null))
        {
            Debug.LogError("[PackingBuilder] Prefabs missing - run 'Tools/Packing Dispatch/1. Build Prefabs' first.");
            return;
        }

        GameObject canvas = FindRoot("Canvas");
        TutorialManager tutorialManager = Object.FindFirstObjectByType<TutorialManager>(FindObjectsInactive.Include);
        AssessmentManager assessmentManager = Object.FindFirstObjectByType<AssessmentManager>(FindObjectsInactive.Include);
        AssessmentUI quizUi = Object.FindFirstObjectByType<AssessmentUI>(FindObjectsInactive.Include);
        AssessmentFailPanel failPanel = Object.FindFirstObjectByType<AssessmentFailPanel>(FindObjectsInactive.Include);
        GameObject m1Scanner = FindRoot("M1_Scanner");
        Dictionary<string, TutorialModalUI> panels = ScenePanels();
        string missing = string.Join(", ", new (string, Object)[]
        {
            ("Canvas", canvas), ("TutorialManager", tutorialManager), ("AssessmentManager", assessmentManager),
            ("AssessmentUI", quizUi), ("AssessmentFailPanel", failPanel), ("M1_Scanner", m1Scanner),
        }.Where(p => p.Item2 == null).Select(p => p.Item1).Concat(M2PanelNames.Where(n => !panels.ContainsKey(n))));
        if (missing.Length > 0)
        {
            Debug.LogError($"[PackingBuilder] Scene objects missing: {missing}");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Build packing station");
        int undoGroup = Undo.GetCurrentGroup();

        GameObject oldStation = FindRoot(StationName);
        if (oldStation != null) Undo.DestroyObjectImmediate(oldStation);
        Transform oldQc = canvas.transform.Find(QcPanelName);
        if (oldQc != null) Undo.DestroyObjectImmediate(oldQc.gameObject);

        // ---- panels and triggers first (the station wires to the triggers) ----
        TutorialModalUI m2_10 = EnsureChallengeModal(panels, "M2_10_Assessment", "M2_09_Error_Identification", tutorialManager, TrainingID.PackingAndDispatch);
        panels["M2_10_Assessment"] = m2_10;
        GameObject group = EnsurePanelGroup(canvas.transform);
        Transform m1Welcome = panels.TryGetValue("M1_01_Welcome_Order_Picking", out TutorialModalUI w) ? w.transform : null;
        foreach (string name in M2PanelNames)
        {
            Transform panel = panels[name].transform;
            if (panel.parent != group.transform) Undo.SetTransformParent(panel, group.transform, "Move M2 panel");
            Undo.RecordObject(panel, "Place M2 panel");
            if (name == "M2_01_Welcome_Packing" || name == "M2_02_Station_Layout")
            {
                if (m1Welcome != null) panel.SetPositionAndRotation(m1Welcome.position, m1Welcome.rotation);
            }
            else if (name == "M2_09_Error_Identification") panel.SetPositionAndRotation(QcModalPosition, QcModalRotation);
            else panel.SetPositionAndRotation(StationModalPosition, StationModalRotation);
        }

        var triggers = new Dictionary<string, ChallengeTutorialTrigger>();
        foreach ((string panelName, string[] names) in M2Steps)
        {
            TutorialModalUI modal = panels[panelName];
            RemoveChildren(modal.transform, "Trigger_");
            var list = new List<Object>();
            foreach (string triggerName in names)
            {
                ChallengeTutorialTrigger trigger = CreateTrigger(modal.transform, triggerName);
                triggers[triggerName] = trigger;
                list.Add(trigger);
            }

            Set(modal, "_hasChallenges", true);
            Set(modal, "_enableAutoNext", panelName == "M2_02_Station_Layout");
            SetArray(modal, "_challengeTutorialTriggerList", list);
            if (Get<Transform>(modal, "_challengeHolder") == null)
                Debug.LogError($"[PackingBuilder] {panelName} has no challenge holder; its checklist will not show.");
        }

        UpdateM2AssessmentData();

        // ---- station ----
        var root = new GameObject(StationName);
        Undo.RegisterCreatedObjectUndo(root, "Build packing station");

        GameObject bench = BuildBench(root.transform, out Transform spot, out Transform[] displaySlots, out TextMeshPro orderText,
            out PackingFeedback feedback, out GameObject readout);
        PackingStation station = bench.AddComponent<PackingStation>();

        Transform[] rackSlots = BuildCartonRack(root.transform);
        VoidFillDispenser dispenser = BuildDispenser(root.transform, pillow);
        ShippingLabel label = BuildPrinterAndLabel(root.transform, labelPrefab);

        var tapeGun = (SeamTool)PrefabUtility.InstantiatePrefab(tapeGunPrefab, root.transform);
        tapeGun.transform.SetPositionAndRotation(new Vector3(15.75f, 0.96f, 12.6f), Quaternion.Euler(0f, 90f, 0f));

        GameObject scanner = Object.Instantiate(m1Scanner, root.transform);
        scanner.name = "M2_Scanner";
        scanner.transform.SetPositionAndRotation(new Vector3(15.75f, 0.98f, 13.9f), m1Scanner.transform.rotation);
        ScannerTool scannerTool = scanner.GetComponent<ScannerTool>();
        Set(scannerTool, "_expectedSku", LabelSku);
        Set(scannerTool, "_challengeTrigger", null);

        ConveyorLoadZone conveyor = BuildConveyor(root.transform);
        PackingCarton qcCarton = BuildQcTable(root.transform, cartons[CartonSize.Medium], cutterPrefab);
        (QcReview qc, PackingFeedback qcFeedback) = BuildQcPanel(canvas.transform);
        Set(qcCarton, "_feedback", qcFeedback);

        var layoutGate = Empty("M2_StationLayoutGate", root.transform, Vector3.zero).AddComponent<MultiCounterGate>();
        Set(layoutGate, "_requiredCount", 3);
        Set(layoutGate, "_challengeTrigger", triggers["TRIGGER_M2_StationLayoutConfirmed"]);
        var signs = new[]
        {
            BuildSign(root.transform, "Sign_Cartons", "CARTONS", new Vector3(16.25f, 2.0f, 15.05f), layoutGate),
            BuildSign(root.transform, "Sign_WorkArea", "WORK AREA", new Vector3(16.45f, 2.0f, 13.0f), layoutGate),
            BuildSign(root.transform, "Sign_LabelsConveyor", "LABELS & CONVEYOR", new Vector3(16.25f, 2.0f, 11.7f), layoutGate),
        };

        // ---- wiring ----
        SetOrders(station, new[]
        {
            ("ORD-2001", Order1Panel, new Vector3(24f, 10f, 14f), new Object[] { speaker }),
            ("ORD-2002", Order2Panel, new Vector3(32f, 14f, 30f), new Object[] { lamp, books, cables }),
        });
        SetArray(station, "_displaySlots", displaySlots);
        SetSupplies(station, new[]
        {
            (CartonSize.Small, cartons[CartonSize.Small], rackSlots[0]),
            (CartonSize.Medium, cartons[CartonSize.Medium], rackSlots[1]),
            (CartonSize.Large, cartons[CartonSize.Large], rackSlots[2]),
        });
        Set(station, "_spot", spot);
        Set(station, "_orderText", orderText);
        Set(station, "_feedback", feedback);
        Set(station, "_label", label);
        Set(station, "_conveyor", conveyor);
        Set(station, "_layoutGate", layoutGate);
        SetArray(station, "_signs", signs);
        Set(station, "_qcReview", qc);
        Set(station, "_boxSelectedTrigger", triggers["TRIGGER_M2_CorrectBoxSelected"]);
        Set(station, "_baseFillTrigger", triggers["TRIGGER_M2_VoidFillBase"]);
        Set(station, "_topFillTrigger", triggers["TRIGGER_M2_VoidFillTop"]);
        Set(station, "_sealedTrigger", triggers["TRIGGER_M2_BoxSealed"]);
        Set(station, "_labelAppliedTrigger", triggers["TRIGGER_M2_LabelApplied"]);
        Set(station, "_multiItemTrigger", triggers["TRIGGER_M2_MultiItemPacked"]);

        Set(label, "_feedback", feedback);
        Set(label, "_verifiedReadout", readout);
        Set(label, "_verifiedTrigger", triggers["TRIGGER_M2_LabelVerified"]);

        Set(conveyor, "_station", station);
        Set(conveyor, "_feedback", feedback);
        Set(conveyor, "_challengeTrigger", triggers["TRIGGER_M2_BoxOnConveyor"]);

        Set(qc, "_carton", qcCarton);
        Set(qc, "_identifiedTrigger", triggers["TRIGGER_M2_ErrorIdentified"]);
        Set(qc, "_correctedTrigger", triggers["TRIGGER_M2_ErrorCorrected"]);

        // ---- M2_10 assessment ----
        GameObject container = Get<GameObject>(m2_10, "_container");
        Transform quizContainer = quizUi.transform.Find("Container");
        GameObject quizAnchor = Empty("QuizAnchor_M2", root.transform, Vector3.zero);
        PlaceQuizAnchor(quizAnchor.transform, quizUi.transform, quizContainer, container != null ? container.transform : m2_10.transform);
        AssessmentLauncher launcher = m2_10.GetComponent<AssessmentLauncher>();
        if (launcher == null) launcher = Undo.AddComponent<AssessmentLauncher>(m2_10.gameObject);
        Set(launcher, "_assessmentManager", assessmentManager);
        Set(launcher, "_launchOnce", true);
        Set(launcher, "_hideDuringQuiz", container);
        Set(launcher, "_quizRoot", quizUi.transform);
        Set(launcher, "_quizAnchor", quizAnchor.transform);

        AssessmentChallengeGate gate = Empty("M2_AssessmentGate", root.transform, Vector3.zero).AddComponent<AssessmentChallengeGate>();
        Set(gate, "_assessmentManager", assessmentManager);
        Set(gate, "_trainingId", (int)TrainingID.PackingAndDispatch);
        Set(gate, "_passPercent", 80);
        Set(gate, "_retakeOnFail", true);
        Set(gate, "_challengeTrigger", triggers["TRIGGER_M2_AssessmentComplete"]);
        Set(gate, "_showOnPass", container);
        Set(gate, "_failPanel", failPanel);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[PackingBuilder] Station built: {root.transform.childCount} station parts, {triggers.Count} M2 triggers wired, " +
                  $"M2 panels moved under Canvas/{PanelGroupName}, M2_10 is a challenge modal with launcher + gate. Save the scene.");
    }

    private static GameObject BuildBench(Transform root, out Transform spot, out Transform[] displaySlots, out TextMeshPro orderText,
        out PackingFeedback feedback, out GameObject readout)
    {
        GameObject bench = Empty("Bench", root, new Vector3(16f, 0f, 13f));
        Transform b = bench.transform;
        Box("Top", b, new Vector3(0f, 0.88f, 0f), new Vector3(0.8f, 0.04f, 2.4f), _materials["BenchTop"], true);
        Box("Shelf", b, new Vector3(0f, 0.25f, 0f), new Vector3(0.74f, 0.02f, 2.3f), _materials["Steel"]);
        foreach (float x in new[] { -0.36f, 0.36f })
        foreach (float z in new[] { -1.15f, 1.15f })
            Box("Leg", b, new Vector3(x, 0.43f, z), new Vector3(0.05f, 0.86f, 0.05f), _materials["Steel"]);
        foreach (float z in new[] { -0.5f, 0.5f })
            Box("PanelPost", b, new Vector3(0.43f, 1.33f, z), new Vector3(0.04f, 0.86f, 0.04f), _materials["Steel"]);
        Box("OrderBoard", b, new Vector3(0.43f, 1.43f, 0f), new Vector3(0.02f, 0.56f, 0.95f), _materials["PanelBack"]);

        Quaternion facing = Quaternion.Euler(0f, 90f, 0f); // readable from the trainee's side (looking +x)
        orderText = Text3D("OrderText", b, new Vector3(0.415f, 1.52f, 0f), facing, "PACKING STATION\nWaiting for the next order.", 0.2f,
            new Vector2(0.88f, 0.34f), Color.white, TextAlignmentOptions.TopLeft);
        orderText.fontSizeMin = 0.08f;
        TextMeshPro readoutText = Text3D("LabelReadout", b, new Vector3(0.415f, 1.3f, 0f), facing,
            "LABEL CHECK: address OK - weight class OK - carrier OK", 0.16f, new Vector2(0.88f, 0.06f), new Color(0.3f, 0.9f, 0.4f));
        readoutText.fontSizeMin = 0.06f;
        readout = readoutText.gameObject;
        readout.SetActive(false);
        TextMeshPro feedbackText = Text3D("Feedback", b, new Vector3(0.415f, 1.2f, 0f), facing, "", 0.17f, new Vector2(0.88f, 0.11f), Color.white);
        feedbackText.fontSizeMin = 0.06f;
        feedback = feedbackText.gameObject.AddComponent<PackingFeedback>();
        Set(feedback, "_text", feedbackText);

        spot = Empty("PackingSpot", b, new Vector3(-0.15f, 0.9f, 0.2f), Quaternion.Euler(0f, 90f, 0f)).transform;
        Material guide = _materials["Guide"];
        Box("SpotLine_Front", b, new Vector3(-0.38f, 0.901f, 0.2f), new Vector3(0.012f, 0.002f, 0.66f), guide);
        Box("SpotLine_Back", b, new Vector3(0.12f, 0.901f, 0.2f), new Vector3(0.012f, 0.002f, 0.66f), guide);
        Box("SpotLine_Left", b, new Vector3(-0.13f, 0.901f, 0.53f), new Vector3(0.5f, 0.002f, 0.012f), guide);
        Box("SpotLine_Right", b, new Vector3(-0.13f, 0.901f, -0.13f), new Vector3(0.5f, 0.002f, 0.012f), guide);
        Text3D("PackHere", b, new Vector3(-0.36f, 0.902f, 0.2f), Quaternion.Euler(90f, 90f, 0f), "PACK HERE", 0.3f, new Vector2(0.5f, 0.05f), guide.GetColor("_BaseColor"));

        displaySlots = new[]
        {
            Empty("DisplaySlot_0", b, new Vector3(0.25f, 1f, -0.4f), Quaternion.Euler(0f, 90f, 0f)).transform,
            Empty("DisplaySlot_1", b, new Vector3(0.25f, 1f, 0.2f), Quaternion.Euler(0f, 90f, 0f)).transform,
            Empty("DisplaySlot_2", b, new Vector3(0.25f, 1f, 0.8f), Quaternion.Euler(0f, 90f, 0f)).transform,
        };
        return bench;
    }

    private static Transform[] BuildCartonRack(Transform root)
    {
        GameObject rack = Empty("CartonRack", root, new Vector3(16f, 0f, 15.05f));
        Transform r = rack.transform;
        Box("Top", r, new Vector3(0f, 0.735f, 0f), new Vector3(0.8f, 0.03f, 1.5f), _materials["BenchTop"], true);
        foreach (float x in new[] { -0.36f, 0.36f })
        foreach (float z in new[] { -0.7f, 0.7f })
            Box("Leg", r, new Vector3(x, 0.36f, z), new Vector3(0.05f, 0.72f, 0.05f), _materials["Steel"]);

        // Cartons stand with their length along +x (the trainee sees the size mark on the -x end).
        return new[]
        {
            Empty("Slot_Small", root, new Vector3(15.95f, 0.75f, 14.5f)).transform,
            Empty("Slot_Medium", root, new Vector3(15.95f, 0.75f, 14.87f)).transform,
            Empty("Slot_Large", root, new Vector3(15.95f, 0.75f, 15.42f)).transform,
        };
    }

    private static VoidFillDispenser BuildDispenser(Transform root, VoidFillPiece pillow)
    {
        GameObject go = Empty("VoidFillDispenser", root, new Vector3(15.85f, 0.9f, 12.15f));
        Transform d = go.transform;
        Box("Tray", d, new Vector3(0f, 0.005f, 0f), new Vector3(0.22f, 0.01f, 0.44f), _materials["ToolGrey"]);
        Box("Rim_-X", d, new Vector3(-0.11f, 0.025f, 0f), new Vector3(0.01f, 0.04f, 0.44f), _materials["ToolGrey"]);
        Box("Rim_+X", d, new Vector3(0.11f, 0.025f, 0f), new Vector3(0.01f, 0.04f, 0.44f), _materials["ToolGrey"]);
        Box("SignCard", d, new Vector3(0.12f, 0.1f, 0f), new Vector3(0.005f, 0.1f, 0.3f), _materials["PanelBack"]);
        Text3D("Sign", d, new Vector3(0.116f, 0.1f, 0f), Quaternion.Euler(0f, 90f, 0f), "VOID FILL", 0.3f, new Vector2(0.28f, 0.08f), Color.white);

        var slots = new Object[]
        {
            Empty("Slot_0", d, new Vector3(0f, 0.04f, -0.13f)).transform,
            Empty("Slot_1", d, new Vector3(0f, 0.04f, 0f)).transform,
            Empty("Slot_2", d, new Vector3(0f, 0.04f, 0.13f)).transform,
        };
        VoidFillDispenser dispenser = go.AddComponent<VoidFillDispenser>();
        Set(dispenser, "_piecePrefab", pillow);
        SetArray(dispenser, "_slots", slots);
        return dispenser;
    }

    private static ShippingLabel BuildPrinterAndLabel(Transform root, ShippingLabel labelPrefab)
    {
        GameObject printer = Empty("LabelPrinter", root, new Vector3(16.22f, 0.9f, 12.05f));
        Transform p = printer.transform;
        Box("Body", p, new Vector3(0f, 0.08f, 0f), new Vector3(0.22f, 0.16f, 0.24f), _materials["ToolGrey"], true);
        Box("Screen", p, new Vector3(-0.111f, 0.12f, 0.06f), new Vector3(0.002f, 0.04f, 0.07f), _materials["PanelBack"]);
        Text3D("Name", p, new Vector3(-0.112f, 0.06f, 0f), Quaternion.Euler(0f, 90f, 0f), "LABEL PRINTER", 0.18f, new Vector2(0.2f, 0.04f), Color.white);

        var label = (ShippingLabel)PrefabUtility.InstantiatePrefab(labelPrefab, root);
        label.transform.SetPositionAndRotation(new Vector3(16.2f, 1.07f, 12.05f), Quaternion.Euler(0f, 90f, 0f));
        return label;
    }

    private static ConveyorLoadZone BuildConveyor(Transform root)
    {
        GameObject conveyor = Empty("Conveyor", root, Vector3.zero);
        Transform c = conveyor.transform;
        Box("Belt", c, new Vector3(18.05f, 0.83f, 11.3f), new Vector3(5f, 0.04f, 0.6f), _materials["Belt"], true);
        Box("Rail_-Z", c, new Vector3(18.05f, 0.875f, 10.985f), new Vector3(5f, 0.05f, 0.03f), _materials["Rail"]);
        Box("Rail_+Z", c, new Vector3(18.05f, 0.875f, 11.615f), new Vector3(5f, 0.05f, 0.03f), _materials["Rail"]);
        Box("EndStop", c, new Vector3(20.57f, 0.9f, 11.3f), new Vector3(0.04f, 0.14f, 0.66f), _materials["Rail"], true);
        foreach (float x in new[] { 15.65f, 16.9f, 18.15f, 19.4f, 20.45f })
        foreach (float z in new[] { 11.05f, 11.55f })
            Box("Leg", c, new Vector3(x, 0.405f, z), new Vector3(0.05f, 0.81f, 0.05f), _materials["Steel"]);
        Material guide = _materials["Guide"];
        Box("Zone_Start", c, new Vector3(15.6f, 0.851f, 11.3f), new Vector3(0.012f, 0.002f, 0.6f), guide);
        Box("Zone_End", c, new Vector3(16.6f, 0.851f, 11.3f), new Vector3(0.012f, 0.002f, 0.6f), guide);
        Box("SignPost", c, new Vector3(15.5f, 0.6f, 10.9f), new Vector3(0.04f, 1.2f, 0.04f), _materials["Steel"]);
        Box("SignBoard", c, new Vector3(15.52f, 1.3f, 10.9f), new Vector3(0.02f, 0.2f, 0.42f), _materials["PanelBack"]);
        Text3D("SignText", c, new Vector3(15.508f, 1.3f, 10.9f), Quaternion.Euler(0f, 90f, 0f), "OUTBOUND\nLABEL UP", 0.35f, new Vector2(0.4f, 0.18f), Color.white);

        GameObject zone = Empty("LoadZone", c, new Vector3(16.1f, 1f, 11.3f));
        ConveyorLoadZone loadZone = zone.AddComponent<ConveyorLoadZone>();
        Set(loadZone, "_zone", zone.transform);
        return loadZone;
    }

    private static PackingCarton BuildQcTable(Transform root, PackingCarton mediumPrefab, SeamTool cutterPrefab)
    {
        GameObject table = Empty("QCTable", root, new Vector3(16f, 0f, 10f));
        Transform t = table.transform;
        Box("Top", t, new Vector3(0f, 0.88f, 0f), new Vector3(0.8f, 0.04f, 1.2f), _materials["BenchTop"], true);
        foreach (float x in new[] { -0.36f, 0.36f })
        foreach (float z in new[] { -0.55f, 0.55f })
            Box("Leg", t, new Vector3(x, 0.43f, z), new Vector3(0.05f, 0.86f, 0.05f), _materials["Steel"]);

        var carton = (PackingCarton)PrefabUtility.InstantiatePrefab(mediumPrefab, root);
        carton.name = "QC_Carton_ORD-1874";
        carton.transform.SetPositionAndRotation(new Vector3(15.95f, 0.9f, 10f), Quaternion.Euler(0f, 90f, 0f));
        Set(carton, "_prePacked", true);
        Set(carton, "_cuttable", true);
        Set(carton, "_prePackedOrderId", "ORD-1874");
        Set(carton, "_prePackedFill", 1);
        Set(carton, "_prePackedItems", 1);
        Set(carton.GetComponent<Grabbable>(), "isGrabbable", false);
        Set(carton.GetComponent<Rigidbody>(), "m_IsKinematic", true);

        // The mug set sitting on a too-thin layer of fill.
        Vector3 inner = new Vector3(0.45f, 0.25f, 0.35f);
        Transform contents = carton.transform.Find("Contents");
        float floor = Wall + inner.y * 0.15f * 0.5f;
        Box("MugSet", contents, new Vector3(0f, floor + 0.06f, 0f), new Vector3(0.2f, 0.12f, 0.2f), _materials["ProductRed"]);
        Text3D("MugSetName", contents, new Vector3(0f, floor + 0.121f, 0f), Quaternion.Euler(90f, 0f, 0f), "Ceramic Mug Set - FRAGILE", 0.2f, new Vector2(0.18f, 0.1f), Color.white);
        Transform labelText = carton.transform.Find("LabelVisual/Text");
        if (labelText != null) labelText.GetComponent<TextMeshPro>().text = QcLabel;

        var cutter = (SeamTool)PrefabUtility.InstantiatePrefab(cutterPrefab, root);
        cutter.transform.SetPositionAndRotation(new Vector3(15.72f, 0.93f, 10.45f), Quaternion.Euler(0f, 90f, 0f));
        return carton;
    }

    private static (QcReview, PackingFeedback) BuildQcPanel(Transform canvas)
    {
        var panel = new GameObject(QcPanelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(panel, "Build QC panel");
        panel.layer = 5;
        panel.transform.SetParent(canvas, false);
        var rt = (RectTransform)panel.transform;
        rt.sizeDelta = new Vector2(440f, 300f);
        rt.localScale = Vector3.one * 0.002f;
        rt.SetPositionAndRotation(new Vector3(16.43f, 1.33f, 10f), Quaternion.Euler(0f, 90f, 0f));
        panel.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 0.94f);

        UiText(panel.transform, "Title", new Vector2(0f, 122f), new Vector2(420f, 40f), "QC REVIEW - ORD-1874", 26f);
        UiText(panel.transform, "Order", new Vector2(0f, 68f), new Vector2(420f, 70f), QcOrderText, 15f);
        Button labelButton = UiButton(panel.transform, "Button_Label", new Vector2(-140f, 0f), "LABEL");
        Button sealButton = UiButton(panel.transform, "Button_Seal", new Vector2(0f, 0f), "SEAL");
        Button fillButton = UiButton(panel.transform, "Button_VoidFill", new Vector2(140f, 0f), "VOID FILL");
        TextMeshProUGUI result = UiText(panel.transform, "Result", new Vector2(0f, -72f), new Vector2(420f, 70f), "", 15f);
        TextMeshProUGUI feedbackText = UiText(panel.transform, "Feedback", new Vector2(0f, -125f), new Vector2(420f, 40f), "", 14f);

        PackingFeedback feedback = feedbackText.gameObject.AddComponent<PackingFeedback>();
        Set(feedback, "_text", feedbackText);
        QcReview review = panel.AddComponent<QcReview>();
        Set(review, "_labelButton", labelButton);
        Set(review, "_sealButton", sealButton);
        Set(review, "_voidFillButton", fillButton);
        Set(review, "_resultText", result);
        return (review, feedback);
    }

    private static GazeDwellTarget BuildSign(Transform root, string name, string text, Vector3 position, MultiCounterGate gate)
    {
        GameObject sign = Empty(name, root, position);
        GameObject board = Box("Board", sign.transform, Vector3.zero, new Vector3(0.02f, 0.2f, 0.62f), _materials["SignBoard"]);
        Quaternion facing = Quaternion.Euler(0f, 90f, 0f);
        Text3D("Title", sign.transform, new Vector3(-0.012f, 0.025f, 0f), facing, text, 0.55f, new Vector2(0.6f, 0.11f), Color.white);
        TextMeshPro found = Text3D("Found", sign.transform, new Vector3(-0.012f, -0.065f, 0f), facing, "FOUND", 0.3f, new Vector2(0.6f, 0.06f), new Color(0.75f, 1f, 0.75f));
        found.gameObject.SetActive(false);
        GazeDwellTarget target = sign.AddComponent<GazeDwellTarget>();
        Set(target, "_gate", gate);
        Set(target, "_board", board.GetComponent<Renderer>());
        Set(target, "_foundMark", found.gameObject);
        return target;
    }

    private static void UpdateM2AssessmentData()
    {
        var data = AssetDatabase.LoadAssetAtPath<TutorialData>("Assets/SO/TutorialData/M2_10_Assessment.asset");
        var so = new SerializedObject(data);
        so.FindProperty("<HasChallenges>k__BackingField").boolValue = true;
        so.FindProperty("<IsInOrder>k__BackingField").boolValue = true;
        SerializedProperty list = so.FindProperty("TutorialChallengeLists");
        list.arraySize = 1;
        SerializedProperty entry = list.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("<ChallengeTitle>k__BackingField").stringValue = "Complete the Packing and Dispatch Assessment";
        entry.FindPropertyRelative("<ChallengeTriggerName>k__BackingField").stringValue = "TRIGGER_M2_AssessmentComplete";
        entry.FindPropertyRelative("<IsComplete>k__BackingField").boolValue = false;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// A plain modal (no challenge checklist) is replaced by a copy of a challenge modal pointed at the same data,
    /// and swapped into the training's flow list.
    /// </summary>
    private static TutorialModalUI EnsureChallengeModal(Dictionary<string, TutorialModalUI> panels, string name, string templateName,
        TutorialManager tutorialManager, TrainingID training)
    {
        TutorialModalUI modal = panels[name];
        if (Get<Transform>(modal, "_challengeHolder") != null) return modal;

        TutorialModalUI template = panels[templateName];
        GameObject copy = Object.Instantiate(template.gameObject, modal.transform.parent);
        Undo.RegisterCreatedObjectUndo(copy, $"Replace {name}");
        copy.name = name;
        copy.SetActive(modal.gameObject.activeSelf);
        copy.transform.SetSiblingIndex(modal.transform.GetSiblingIndex());
        copy.transform.SetPositionAndRotation(modal.transform.position, modal.transform.rotation);
        RemoveChildren(copy.transform, "Trigger_");
        TutorialModalUI replacement = copy.GetComponent<TutorialModalUI>();
        Set(replacement, "_tutorialData", Get<TutorialData>(modal, "_tutorialData"));
        ReplaceInFlow(tutorialManager, training, modal, replacement);
        Undo.DestroyObjectImmediate(modal.gameObject);
        Debug.Log($"[PackingBuilder] {name} was a plain modal; replaced with a challenge modal copied from {templateName}.");
        return replacement;
    }

    private static GameObject EnsurePanelGroup(Transform canvas)
    {
        Transform existing = canvas.Find(PanelGroupName);
        if (existing != null) return existing.gameObject;
        var group = new GameObject(PanelGroupName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(group, "Create M2 panel group");
        group.layer = 5;
        group.transform.SetParent(canvas, false);
        return group;
    }

    // =====================================================================================================
    // 3. Restore M1_09
    // =====================================================================================================

    [MenuItem("Tools/Packing Dispatch/3. Restore M1_09 Assessment")]
    public static void RestoreM1Assessment()
    {
        Dictionary<string, TutorialModalUI> panels = ScenePanels();
        if (panels.ContainsKey("M1_09_Assessment"))
        {
            Debug.Log("[PackingBuilder] M1_09_Assessment already exists - nothing to restore.");
            return;
        }

        if (!panels.TryGetValue("M1_08_Path_Efficiency_Review", out TutorialModalUI template) ||
            !panels.TryGetValue("M1_10_Module_Complete", out TutorialModalUI last))
        {
            Debug.LogError("[PackingBuilder] M1_08 or M1_10 panel not found.");
            return;
        }

        TutorialManager tutorialManager = Object.FindFirstObjectByType<TutorialManager>(FindObjectsInactive.Include);
        AssessmentManager assessmentManager = Object.FindFirstObjectByType<AssessmentManager>(FindObjectsInactive.Include);
        AssessmentUI quizUi = Object.FindFirstObjectByType<AssessmentUI>(FindObjectsInactive.Include);
        AssessmentChallengeGate gate = Object.FindObjectsByType<AssessmentChallengeGate>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(g => new SerializedObject(g).FindProperty("_trainingId").intValue == (int)TrainingID.OrderPicking);
        var data = AssetDatabase.LoadAssetAtPath<TutorialData>("Assets/SO/TutorialData/M1_09_Assessment.asset");
        if (tutorialManager == null || assessmentManager == null || quizUi == null || gate == null || data == null)
        {
            Debug.LogError("[PackingBuilder] TutorialManager, AssessmentManager, AssessmentUI, the OrderPicking AssessmentChallengeGate or M1_09 data missing.");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Restore M1_09 assessment");
        int undoGroup = Undo.GetCurrentGroup();

        GameObject copy = Object.Instantiate(template.gameObject, template.transform.parent);
        Undo.RegisterCreatedObjectUndo(copy, "Restore M1_09");
        copy.name = "M1_09_Assessment";
        copy.SetActive(template.gameObject.activeSelf);
        copy.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);
        copy.transform.SetPositionAndRotation(template.transform.position, template.transform.rotation);
        foreach (RouteReviewPresenter presenter in copy.GetComponents<RouteReviewPresenter>()) Object.DestroyImmediate(presenter);
        RemoveChildren(copy.transform, "Trigger_");

        TutorialModalUI modal = copy.GetComponent<TutorialModalUI>();
        Set(modal, "_tutorialData", data);
        Set(modal, "_hasChallenges", true);
        Set(modal, "_enableAutoNext", false);
        ChallengeTutorialTrigger trigger = CreateTrigger(copy.transform, "TRIGGER_M1_AssessmentComplete");
        SetArray(modal, "_challengeTutorialTriggerList", new List<Object> { trigger });

        GameObject container = Get<GameObject>(modal, "_container");
        GameObject anchor = Empty("QuizAnchor_M1", template.transform.parent, Vector3.zero);
        Undo.RegisterCreatedObjectUndo(anchor, "Restore M1_09");
        PlaceQuizAnchor(anchor.transform, quizUi.transform, quizUi.transform.Find("Container"), container != null ? container.transform : copy.transform);

        AssessmentLauncher launcher = Undo.AddComponent<AssessmentLauncher>(copy);
        Set(launcher, "_assessmentManager", assessmentManager);
        Set(launcher, "_launchOnce", true);
        Set(launcher, "_hideDuringQuiz", container);
        Set(launcher, "_quizRoot", quizUi.transform);
        Set(launcher, "_quizAnchor", anchor.transform);

        Set(gate, "_challengeTrigger", trigger);
        Set(gate, "_showOnPass", container);

        InsertInFlow(tutorialManager, TrainingID.OrderPicking, modal, last);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[PackingBuilder] M1_09_Assessment restored (copy of M1_08 at the A-aisle spot): trigger TRIGGER_M1_AssessmentComplete, " +
                  "AssessmentLauncher (quiz moved to the A aisle while it runs), OrderPicking gate re-wired, inserted before M1_10. Save the scene.");
    }

    // =====================================================================================================
    // Helpers
    // =====================================================================================================

    private static void LoadMaterials()
    {
        _materials = new Dictionary<string, Material>
        {
            ["Cardboard"] = Mat("M2_Cardboard", new Color(0.72f, 0.55f, 0.36f)),
            ["CardboardDark"] = Mat("M2_CardboardDark", new Color(0.62f, 0.46f, 0.29f)),
            ["CardboardSeam"] = Mat("M2_CardboardSeam", new Color(0.35f, 0.26f, 0.16f)),
            ["Tape"] = Mat("M2_Tape", new Color(0.62f, 0.47f, 0.27f), 0.75f),
            ["Guide"] = Mat("M2_Guide", new Color(1f, 0.82f, 0.1f)),
            ["Pillow"] = Mat("M2_AirPillow", new Color(0.93f, 0.95f, 0.98f), 0.85f),
            ["LabelPaper"] = Mat("M2_LabelPaper", new Color(0.97f, 0.97f, 0.95f)),
            ["Ink"] = Mat("M2_Ink", Ink),
            ["BenchTop"] = Mat("M2_BenchTop", new Color(0.45f, 0.34f, 0.24f)),
            ["Steel"] = Mat("M2_Steel", new Color(0.56f, 0.58f, 0.62f), 0.5f, 0.6f),
            ["PanelBack"] = Mat("M2_PanelBack", new Color(0.08f, 0.09f, 0.11f)),
            ["SignBoard"] = Mat("M2_SignBoard", new Color(0.1f, 0.2f, 0.38f)),
            ["Belt"] = Mat("M2_Belt", new Color(0.12f, 0.12f, 0.13f), 0.2f),
            ["Rail"] = Mat("M2_Rail", new Color(0.95f, 0.75f, 0.1f), 0.4f),
            ["ToolRed"] = Mat("M2_ToolRed", new Color(0.8f, 0.15f, 0.1f), 0.5f),
            ["ToolGrey"] = Mat("M2_ToolGrey", new Color(0.3f, 0.32f, 0.35f), 0.4f),
            ["CutterYellow"] = Mat("M2_CutterYellow", new Color(0.98f, 0.8f, 0.1f), 0.5f),
            ["ProductBlue"] = Mat("M2_ProductBlue", new Color(0.15f, 0.35f, 0.75f), 0.4f),
            ["ProductOrange"] = Mat("M2_ProductOrange", new Color(0.9f, 0.45f, 0.1f), 0.4f),
            ["ProductPurple"] = Mat("M2_ProductPurple", new Color(0.45f, 0.25f, 0.6f), 0.4f),
            ["ProductGreen"] = Mat("M2_ProductGreen", new Color(0.2f, 0.6f, 0.3f), 0.4f),
            ["ProductRed"] = Mat("M2_ProductRed", new Color(0.7f, 0.15f, 0.2f), 0.4f),
        };
    }

    private static Material Mat(string name, Color color, float smoothness = 0.25f, float metallic = 0f)
    {
        EnsureFolder(MaterialFolder);
        string path = $"{MaterialFolder}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", metallic);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static T LoadPrefab<T>(string name) where T : Component
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{name}.prefab");
        return go != null ? go.GetComponent<T>() : null;
    }

    private static GameObject FindRoot(string name) =>
        SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == name);

    private static Dictionary<string, TutorialModalUI> ScenePanels() =>
        Resources.FindObjectsOfTypeAll<TutorialModalUI>()
            .Where(p => p.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(p))
            .GroupBy(p => p.name)
            .ToDictionary(g => g.Key, g => g.First());

    private static GameObject Empty(string name, Transform parent, Vector3 localPosition, Quaternion? localRotation = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation ?? Quaternion.identity;
        return go;
    }

    private static GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material,
        bool keepCollider = false, Quaternion? localRotation = null) =>
        Prim(PrimitiveType.Cube, name, parent, localPosition, size, material, keepCollider, localRotation);

    private static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale,
        Material material, bool keepCollider = false, Quaternion? localRotation = null)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation ?? Quaternion.identity;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    /// <summary>World-space TextMeshPro: readable when looking along the object's +Z.</summary>
    private static TextMeshPro Text3D(string name, Transform parent, Vector3 localPosition, Quaternion localRotation, string text,
        float fontSize, Vector2 size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMax = fontSize;
        tmp.fontSizeMin = fontSize * 0.4f;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.rectTransform.sizeDelta = size;
        return tmp;
    }

    private static TextMeshProUGUI UiText(Transform parent, string name, Vector2 position, Vector2 size, string text, float fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Button UiButton(Transform parent, string name, Vector2 position, string text)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(124f, 48f);
        var image = go.GetComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.color = new Color(0.18f, 0.42f, 0.82f);
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        UiText(go.transform, "Text", Vector2.zero, new Vector2(124f, 48f), text, 20f);
        return button;
    }

    private static Grabbable AddGrabbable(GameObject go, float mass, bool singleHand)
    {
        var body = go.AddComponent<Rigidbody>();
        body.mass = mass;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        var grab = go.AddComponent<Grabbable>();
        grab.singleHandOnly = singleHand;
        grab.heldNoFriction = true;
        return grab;
    }

    private static ChallengeTutorialTrigger CreateTrigger(Transform panel, string triggerName)
    {
        var go = new GameObject("Trigger_" + triggerName.Substring("TRIGGER_".Length));
        Undo.RegisterCreatedObjectUndo(go, "Create challenge trigger");
        go.transform.SetParent(panel, false);
        var trigger = go.AddComponent<ChallengeTutorialTrigger>();
        Set(trigger, "_triggerName", triggerName);
        Set(trigger, "_tutorialType", (int)TutorialType.Trigger);
        return trigger;
    }

    private static void RemoveChildren(Transform parent, string prefix)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child.name.StartsWith(prefix)) Undo.DestroyObjectImmediate(child.gameObject);
        }
    }

    /// <summary>Poses the quiz root so its question container lands exactly on <paramref name="target"/>.</summary>
    private static void PlaceQuizAnchor(Transform anchor, Transform quizRoot, Transform quizContainer, Transform target)
    {
        if (quizContainer == null) quizContainer = quizRoot;
        Quaternion inverseRoot = Quaternion.Inverse(quizRoot.rotation);
        Vector3 offset = inverseRoot * (quizContainer.position - quizRoot.position);
        Quaternion relative = inverseRoot * quizContainer.rotation;
        Quaternion rootRotation = target.rotation * Quaternion.Inverse(relative);
        anchor.SetPositionAndRotation(target.position - rootRotation * offset, rootRotation);
    }

    private static void ReplaceInFlow(TutorialManager manager, TrainingID training, Object oldModal, Object newModal)
    {
        var so = new SerializedObject(manager);
        SerializedProperty list = FlowList(so, training);
        for (int i = 0; list != null && i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == oldModal)
                list.GetArrayElementAtIndex(i).objectReferenceValue = newModal;
        so.ApplyModifiedProperties();
    }

    private static void InsertInFlow(TutorialManager manager, TrainingID training, Object modal, Object before)
    {
        var so = new SerializedObject(manager);
        SerializedProperty list = FlowList(so, training);
        if (list == null) return;
        int index = list.arraySize;
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == before) index = i;
        list.InsertArrayElementAtIndex(index);
        list.GetArrayElementAtIndex(index).objectReferenceValue = modal;
        so.ApplyModifiedProperties();
    }

    private static SerializedProperty FlowList(SerializedObject tutorialManager, TrainingID training)
    {
        SerializedProperty flows = tutorialManager.FindProperty("_tutorialFlowList");
        for (int i = 0; i < flows.arraySize; i++)
        {
            SerializedProperty flow = flows.GetArrayElementAtIndex(i);
            if (flow.FindPropertyRelative("ID").intValue == (int)training) return flow.FindPropertyRelative("List");
        }

        Debug.LogError($"[PackingBuilder] TutorialManager has no flow list for {training}.");
        return null;
    }

    private static void SetOrders(PackingStation station, (string Id, string Panel, Vector3 Size, Object[] Items)[] orders)
    {
        var so = new SerializedObject(station);
        SerializedProperty list = so.FindProperty("_orders");
        list.arraySize = orders.Length;
        for (int i = 0; i < orders.Length; i++)
        {
            SerializedProperty order = list.GetArrayElementAtIndex(i);
            order.FindPropertyRelative("Id").stringValue = orders[i].Id;
            order.FindPropertyRelative("PanelText").stringValue = orders[i].Panel;
            order.FindPropertyRelative("PackedSizeCm").vector3Value = orders[i].Size;
            SerializedProperty items = order.FindPropertyRelative("ItemPrefabs");
            items.arraySize = orders[i].Items.Length;
            for (int j = 0; j < orders[i].Items.Length; j++)
                items.GetArrayElementAtIndex(j).objectReferenceValue = orders[i].Items[j];
        }

        so.ApplyModifiedProperties();
    }

    private static void SetSupplies(PackingStation station, (CartonSize Size, PackingCarton Prefab, Transform Slot)[] supplies)
    {
        var so = new SerializedObject(station);
        SerializedProperty list = so.FindProperty("_supplies");
        list.arraySize = supplies.Length;
        for (int i = 0; i < supplies.Length; i++)
        {
            SerializedProperty supply = list.GetArrayElementAtIndex(i);
            supply.FindPropertyRelative("Size").intValue = (int)supplies[i].Size;
            supply.FindPropertyRelative("Prefab").objectReferenceValue = supplies[i].Prefab;
            supply.FindPropertyRelative("Slot").objectReferenceValue = supplies[i].Slot;
        }

        so.ApplyModifiedProperties();
    }

    private static T Get<T>(Object target, string property) where T : Object
    {
        SerializedProperty p = new SerializedObject(target).FindProperty(property);
        return p != null ? p.objectReferenceValue as T : null;
    }

    private static void Set(Object target, string property, object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(property);
        if (p == null)
        {
            Debug.LogError($"[PackingBuilder] {target.GetType().Name} has no serialized field '{property}'.");
            return;
        }

        switch (value)
        {
            case null: p.objectReferenceValue = null; break;
            case Object o: p.objectReferenceValue = o; break;
            case string s: p.stringValue = s; break;
            case bool b: p.boolValue = b; break;
            case int i: p.intValue = i; break;
            case float f: p.floatValue = f; break;
            case Vector3 v: p.vector3Value = v; break;
            default:
                Debug.LogError($"[PackingBuilder] Unsupported value type {value.GetType().Name} for '{property}'.");
                return;
        }

        so.ApplyModifiedProperties();
    }

    private static void SetArray(Object target, string property, IList<Object> values)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(property);
        if (p == null)
        {
            Debug.LogError($"[PackingBuilder] {target.GetType().Name} has no serialized field '{property}'.");
            return;
        }

        p.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedProperties();
    }
}
