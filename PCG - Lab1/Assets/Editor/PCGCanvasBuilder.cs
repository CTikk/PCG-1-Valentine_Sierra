/*#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.IO;

public class PCGCanvasBuilder : Editor
{
    const string PREFAB_DIR = "Assets/Prefabs";
    const string PREFAB_PATH = PREFAB_DIR + "/PCG_UI.prefab";

    [MenuItem("Tools/PCG/Build Generation Canvas Prefab")]
    public static void BuildCanvasPrefab()
    {
        // ===== Canvas =====
        var canvasGO = new GameObject("PCG_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1440, 900);
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));

        // ===== Panel raíz =====
        var panel = CreateUIObject("Panel", canvasGO.transform);
        var img = panel.AddComponent<Image>(); img.color = new Color(0f, 0f, 0f, 0.4f);
        var vroot = panel.AddComponent<VerticalLayoutGroup>();
        vroot.childForceExpandHeight = false; vroot.childForceExpandWidth = true; vroot.spacing = 8;
        vroot.padding = new RectOffset(12, 12, 12, 12);
        var fitter = panel.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(14, -14);

        AddHeader(panel.transform, "PCG Controls");

        // ===== Secciones colapsables =====
        // PERLIN
        var secPerlin = CreateCollapsible(panel.transform, "Perlin", true, true, out var perlinContent);
        var inWidth = AddLabeledTMPInt(perlinContent, "Width", "80");
        var inDepth = AddLabeledTMPInt(perlinContent, "Depth", "80");
        var inNoiseScale = AddLabeledTMPFloat(perlinContent, "Noise Scale", "50");
        var inOctaves = AddLabeledTMPInt(perlinContent, "Octaves (1..8)", "4");
        var inLacunarity = AddLabeledTMPFloat(perlinContent, "Lacunarity", "2");
        var inPersistence = AddLabeledTMPFloat(perlinContent, "Persistence (0..1)", "0.5");
        var inTiers = AddLabeledTMPInt(perlinContent, "Tiers (>=1)", "3");
        var inTierHeight = AddLabeledTMPFloat(perlinContent, "Tier Height", "4");
        var ddEdgeMode = AddDropdown(perlinContent, "Edge Mode", new[] { "HardStep", "SmoothStep" }, 0);
        var inSmoothRange = AddLabeledTMPFloat(perlinContent, "SmoothRange (0..1)", "0.10");

        // BSP
        var secBSP = CreateCollapsible(panel.transform, "BSP", true, true, out var bspContent);
        var inMapWidth = AddLabeledTMPInt(bspContent, "Grid Width", "80");
        var inMapHeight = AddLabeledTMPInt(bspContent, "Grid Height", "80");
        var inCellSize = AddLabeledTMPFloat(bspContent, "Cell Size", "1");
        var inMinRoom = AddLabeledTMPInt(bspContent, "Min Room", "4");
        var inMaxRoom = AddLabeledTMPInt(bspContent, "Max Room", "8");
        var inMinLeaf = AddLabeledTMPInt(bspContent, "Min Leaf", "8");
        var ddCorr = AddDropdown(bspContent, "Corridor Logic", new[] { "RandomOrder", "HorizontalFirst", "VerticalFirst", "StraightAxis" }, 0);
        var tgForbidMT = AddToggle(bspContent, "Forbid Multi-Tier Rooms", true);
        var inEnemies = AddLabeledTMPInt(bspContent, "Enemies per Room", "2");

        // Houses
        var secHouses = CreateCollapsible(panel.transform, "Houses", false, false, out var houseContent);
        var tgSpawnH = AddToggle(houseContent, "Spawn Houses", false);
        var inHMargin = AddLabeledTMPInt(houseContent, "House Margin", "1");
        var inHProb = AddLabeledTMPFloat(houseContent, "Fill Probability (0..1)", "1.0");
        var inHYOff = AddLabeledTMPFloat(houseContent, "House Y Offset", "0");

        // L-System
        var secLS = CreateCollapsible(panel.transform, "L-System Trees", true, true, out var lsContent);
        var inIter = AddLabeledTMPInt(lsContent, "Iterations (1..6)", "3");
        var inStep = AddLabeledTMPFloat(lsContent, "Turtle Step", "0.25");
        var inAngle = AddLabeledTMPFloat(lsContent, "Turtle Angle", "22.5");
        var inTrees = AddLabeledTMPInt(lsContent, "Trees per Room", "2");
        var inRing = AddLabeledTMPInt(lsContent, "Ring Margin Cells", "1");
        var tgAvoidW = AddToggle(lsContent, "Avoid Water", true);
        var tgAvoidR = AddToggle(lsContent, "Avoid Rooms", true);
        var ddMode = AddDropdown(lsContent, "Spawn Mode", new[] { "PerRoom", "GlobalScatter" }, 0);
        var inGCount = AddLabeledTMPInt(lsContent, "Global Count", "30");

        // Row botones
        var row = CreateRow(panel.transform);
        var btnRandom = AddButton(row.transform, "Randomize Seeds");
        var btnRegen = AddButton(row.transform, "Regenerar");

        // ===== Asignación a GenerationUI =====
        var genUI = panel.AddComponent<GenerationUI>();
        genUI.perlin = FindObjectOfType<PerlinTerrainGenerator>();
        genUI.bsp = FindObjectOfType<BSPDungeonGenerator>();
        genUI.village = FindObjectOfType<VillageGenerator>();
        genUI.treeSpawner = FindObjectOfType<LSystemTreeSpawner>();

        genUI.btnRandomizeSeeds = btnRandom;
        genUI.btnRegenerate = btnRegen;

        genUI.secPerlin = secPerlin; genUI.secBSP = secBSP; genUI.secHouses = secHouses; genUI.secLSystem = secLS;

        genUI.inWidth = inWidth; genUI.inDepth = inDepth; genUI.inNoiseScale = inNoiseScale;
        genUI.inOctaves = inOctaves; genUI.inLacunarity = inLacunarity; genUI.inPersistence = inPersistence;
        genUI.inTiers = inTiers; genUI.inTierHeight = inTierHeight; genUI.ddEdgeMode = ddEdgeMode; genUI.inSmoothRange = inSmoothRange;

        genUI.inMapWidth = inMapWidth; genUI.inMapHeight = inMapHeight; genUI.inCellSize = inCellSize;
        genUI.inMinRoom = inMinRoom; genUI.inMaxRoom = inMaxRoom; genUI.inMinLeaf = inMinLeaf;
        genUI.ddCorridorLogic = ddCorr; genUI.tgForbidMultiTier = tgForbidMT; genUI.inEnemiesPerRoom = inEnemies;

        genUI.tgSpawnHouses = tgSpawnH; genUI.inHouseMargin = inHMargin; genUI.inHouseProb = inHProb; genUI.inHouseYOffset = inHYOff;

        genUI.inTreeIterations = inIter; genUI.inTreeStep = inStep; genUI.inTreeAngle = inAngle;
        genUI.inTreesPerRoom = inTrees; genUI.inRingMargin = inRing; genUI.tgAvoidWater = tgAvoidW; genUI.tgAvoidRooms = tgAvoidR;
        genUI.ddSpawnMode = ddMode; genUI.inGlobalCount = inGCount;

        // ===== Guardar prefab =====
        if (!Directory.Exists(PREFAB_DIR)) Directory.CreateDirectory(PREFAB_DIR);
        var prefab = PrefabUtility.SaveAsPrefabAsset(canvasGO, PREFAB_PATH, out bool success);
        if (success)
        {
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("PCG UI prefab guardado en: " + PREFAB_PATH);
        }
        else Debug.LogWarning("No se pudo guardar el prefab.");

        Undo.RegisterCreatedObjectUndo(canvasGO, "Create PCG Canvas");
    }

    // ---------- Helpers UI ----------
    static GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static void AddHeader(Transform parent, string text)
    {
        var t = CreateText(parent, text, 20, FontStyles.Bold);
        var l = t.gameObject.AddComponent<LayoutElement>(); l.minHeight = 28;
    }

    // ===== Collapsible section factory =====
    static CollapsibleSection CreateCollapsible(Transform parent, string title, bool startExpanded, bool startEnabled, out Transform contentOut)
    {
        // Root
        var root = CreateUIObject(title + "_Section", parent);
        var v = root.AddComponent<VerticalLayoutGroup>();
        v.childForceExpandHeight = false; v.childForceExpandWidth = true; v.spacing = 4;
        var img = root.AddComponent<Image>(); img.color = new Color(1, 1, 1, 0.06f);

        // Header row
        var header = CreateRow(root.transform);
        var enableGO = CreateUIObject("Enable_Toggle", header.transform);
        var enableImg = enableGO.AddComponent<Image>(); enableImg.color = new Color(0, 0, 0, 0.35f);
        var enable = enableGO.AddComponent<Toggle>();
        var enableLE = enableGO.AddComponent<LayoutElement>(); enableLE.preferredWidth = 36;

        var titleLabel = CreateText(header.transform, title, 16, FontStyles.Bold);
        var spacer = CreateUIObject("Spacer", header.transform);
        var spLE = spacer.AddComponent<LayoutElement>(); spLE.flexibleWidth = 1f;

        var foldBtnGO = CreateUIObject("Fold_Button", header.transform);
        var foldImg = foldBtnGO.AddComponent<Image>(); foldImg.color = new Color(1, 1, 1, 0.15f);
        var foldBtn = foldBtnGO.AddComponent<Button>();
        var foldTxt = CreateText(foldBtnGO.transform, "▾", 16, FontStyles.Bold);
        foldTxt.alignment = TextAlignmentOptions.Center;
        var foldLE = foldBtnGO.AddComponent<LayoutElement>();
        foldLE.preferredWidth = 30; foldLE.preferredHeight = 26;

        // Content
        var content = CreateUIObject("Content", root.transform);
        var v2 = content.AddComponent<VerticalLayoutGroup>();
        v2.childForceExpandHeight = false; v2.childForceExpandWidth = true; v2.spacing = 4;
        var fit = content.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // CollapsibleSection component
        var col = root.AddComponent<CollapsibleSection>();
        col.enableToggle = enable;
        col.foldButton = foldBtn;
        col.titleText = titleLabel;
        col.contentRoot = content.GetComponent<RectTransform>();
        col.startExpanded = startExpanded;
        col.startEnabled = startEnabled;
        col.SetTitle(title);

        contentOut = content.transform;
        return col;
    }

    static Transform CreateRow(Transform parent)
    {
        var row = CreateUIObject("Row", parent);
        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.childForceExpandHeight = false; h.childForceExpandWidth = true; h.spacing = 6;
        var fit = row.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return row.transform;
    }

    static TMP_Text CreateText(Transform parent, string text, int size = 16, FontStyles style = FontStyles.Normal)
    {
        var go = CreateUIObject("Text", parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.fontStyle = style; tmp.color = Color.white;
        var le = go.AddComponent<LayoutElement>(); le.minHeight = 22;
        return tmp;
    }

    static TMP_InputField AddLabeledTMPInt(Transform parent, string label, string def = "")
    {
        var row = CreateRow(parent);
        CreateText(row, label);
        var inputGO = CreateUIObject(label + "_Input", row);
        var img = inputGO.AddComponent<Image>(); img.color = new Color(0, 0, 0, 0.35f);
        var input = inputGO.AddComponent<TMP_InputField>();
        var textArea = CreateUIObject("TextArea", inputGO.transform);
        var text = textArea.AddComponent<TextMeshProUGUI>();
        input.textViewport = textArea.GetComponent<RectTransform>();
        input.textComponent = text; input.contentType = TMP_InputField.ContentType.IntegerNumber; input.text = def;
        var le = inputGO.AddComponent<LayoutElement>(); le.preferredWidth = 200;
        text.fontSize = 16; text.color = Color.white;
        return input;
    }

    static TMP_InputField AddLabeledTMPFloat(Transform parent, string label, string def = "")
    {
        var row = CreateRow(parent);
        CreateText(row, label);
        var inputGO = CreateUIObject(label + "_Input", row);
        var img = inputGO.AddComponent<Image>(); img.color = new Color(0, 0, 0, 0.35f);
        var input = inputGO.AddComponent<TMP_InputField>();
        var textArea = CreateUIObject("TextArea", inputGO.transform);
        var text = textArea.AddComponent<TextMeshProUGUI>();
        input.textViewport = textArea.GetComponent<RectTransform>();
        input.textComponent = text; input.contentType = TMP_InputField.ContentType.DecimalNumber; input.text = def;
        var le = inputGO.AddComponent<LayoutElement>(); le.preferredWidth = 200;
        text.fontSize = 16; text.color = Color.white;
        return input;
    }

    static Toggle AddToggle(Transform parent, string label, bool def)
    {
        var row = CreateRow(parent);
        CreateText(row, label);
        var togGO = CreateUIObject(label + "_Toggle", row);
        var togImg = togGO.AddComponent<Image>(); togImg.color = new Color(0, 0, 0, 0.35f);
        var tog = togGO.AddComponent<Toggle>(); tog.isOn = def;
        var le = togGO.AddComponent<LayoutElement>(); le.preferredWidth = 40;
        return tog;
    }

    static TMP_Dropdown AddDropdown(Transform parent, string label, string[] options, int defIndex)
    {
        var row = CreateRow(parent);
        CreateText(row, label);
        var ddGO = CreateUIObject(label + "_Dropdown", row);
        var img = ddGO.AddComponent<Image>(); img.color = new Color(0, 0, 0, 0.35f);
        var dd = ddGO.AddComponent<TMP_Dropdown>();
        dd.options.Clear();
        foreach (var o in options) dd.options.Add(new TMP_Dropdown.OptionData(o));
        dd.value = Mathf.Clamp(defIndex, 0, dd.options.Count - 1);
        var le = ddGO.AddComponent<LayoutElement>(); le.preferredWidth = 220;
        return dd;
    }

    static Button AddButton(Transform parent, string text)
    {
        var btnGO = CreateUIObject(text + "_Button", parent);
        var img = btnGO.AddComponent<Image>(); img.color = new Color(1, 1, 1, 0.15f);
        var btn = btnGO.AddComponent<Button>();
        var lab = CreateText(btnGO.transform, text, 16, FontStyles.Bold);
        lab.alignment = TextAlignmentOptions.Center;
        var rt = lab.GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var le = btnGO.AddComponent<LayoutElement>(); le.preferredWidth = 160; le.preferredHeight = 34;
        return btn;
    }
}
#endif*/