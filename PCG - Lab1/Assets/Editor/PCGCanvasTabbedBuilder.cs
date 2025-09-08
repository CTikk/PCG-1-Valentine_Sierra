#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections.Generic;

public class PCGCanvasTabbedBuilder : Editor
{
    const string DIR = "Assets/Prefabs";
    const string PATH = DIR + "/PCG_UI_Tabs.prefab";

    [MenuItem("Tools/PCG/Build Tabbed PCG UI Prefab")]
    public static void Build()
    {
        // Canvas + EventSystem
        var canvasGO = new GameObject("PCG_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900);
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));

        // Ventana
        var window = Create("Window", canvasGO.transform);
        var wImg = window.AddComponent<Image>(); wImg.color = new Color(0.08f, 0.08f, 0.1f, 0.92f);
        var wRT = window.GetComponent<RectTransform>();
        wRT.sizeDelta = new Vector2(1000, 640);
        wRT.anchorMin = wRT.anchorMax = new Vector2(0.5f, 0.5f);
        wRT.pivot = new Vector2(0.5f, 0.5f);
        wRT.anchoredPosition = Vector2.zero;

        var hSplit = window.AddComponent<HorizontalLayoutGroup>();
        hSplit.spacing = 8; hSplit.childForceExpandHeight = true; hSplit.childForceExpandWidth = false;
        var winPad = window.AddComponent<LayoutElement>(); winPad.minHeight = 640; winPad.minWidth = 900;

        // Sidebar tabs
        var sidebar = Create("Sidebar", window.transform);
        var sbRT = sidebar.GetComponent<RectTransform>(); sbRT.sizeDelta = new Vector2(180, 0);
        var sbLayout = sidebar.AddComponent<VerticalLayoutGroup>();
        sbLayout.padding = new RectOffset(12, 12, 12, 12); sbLayout.spacing = 8;
        sidebar.AddComponent<Image>().color = new Color(1, 1, 1, 0.05f);

        // Content column
        var content = Create("Content", window.transform);
        var cLayout = content.AddComponent<VerticalLayoutGroup>();
        cLayout.padding = new RectOffset(12, 12, 12, 12); cLayout.spacing = 8; cLayout.childForceExpandHeight = true; cLayout.childForceExpandWidth = true;
        var cImg = content.AddComponent<Image>(); cImg.color = new Color(1, 1, 1, 0.03f);
        var cRT = content.GetComponent<RectTransform>(); cRT.sizeDelta = new Vector2(0, 0);

        // Title
        var titleGO = Create("Title", content.transform);
        var title = titleGO.AddComponent<TextMeshProUGUI>();
        title.text = "Terrain"; title.fontSize = 24; title.fontStyle = FontStyles.Bold; title.color = Color.white;
        var titleLE = titleGO.AddComponent<LayoutElement>(); titleLE.minHeight = 32;

        // Scroll area
        var scrollRoot = Create("ScrollRoot", content.transform);
        var scrollLE = scrollRoot.AddComponent<LayoutElement>(); scrollLE.flexibleHeight = 1f;
        var scrollView = scrollRoot.AddComponent<ScrollRect>();
        var viewport = Create("Viewport", scrollRoot.transform);
        var mask = viewport.AddComponent<Mask>(); mask.showMaskGraphic = false;
        viewport.AddComponent<Image>().color = new Color(1, 1, 1, 0.02f);
        scrollView.viewport = viewport.GetComponent<RectTransform>();
        var contentGO = Create("ScrollContent", viewport.transform);
        var v = contentGO.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(6, 6, 6, 6); v.spacing = 6; v.childForceExpandHeight = false; v.childForceExpandWidth = true;
        var fit = contentGO.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollView.content = contentGO.GetComponent<RectTransform>();

        // Bottom buttons
        var bottom = Create("BottomBar", content.transform);
        var bLayout = bottom.AddComponent<HorizontalLayoutGroup>();
        bLayout.spacing = 8; bLayout.childAlignment = TextAnchor.MiddleRight;
        var bottomLE = bottom.AddComponent<LayoutElement>(); bottomLE.minHeight = 44;
        var btnRandom = MakeButton(bottom.transform, "Randomize Seeds");
        var btnRegen = MakeButton(bottom.transform, "Regenerate");

        // Tabs + panels
        var buttons = new List<Button>();
        var panels = new List<GameObject>();
        var tabNames = new List<string> { "Terrain", "BSP", "Houses", "Trees" };

        // Botones sidebar
        foreach (var name in tabNames)
            buttons.Add(MakeTabButton(sidebar.transform, name));

        // Pestañas de contenido (en ScrollContent)
        var perlinPanel = MakeTabPanel(contentGO.transform, "PerlinPanel");
        var bspPanel = MakeTabPanel(contentGO.transform, "BSPPanel");
        var housePanel = MakeTabPanel(contentGO.transform, "HousesPanel");
        var treePanel = MakeTabPanel(contentGO.transform, "TreesPanel");

        panels.Add(perlinPanel); panels.Add(bspPanel); panels.Add(housePanel); panels.Add(treePanel);

        // Campos PERLIN
        var inWidth = AddRowInt(perlinPanel.transform, "Width", "80");
        var inDepth = AddRowInt(perlinPanel.transform, "Depth", "80");
        var inNoiseScale = AddRowFloat(perlinPanel.transform, "Noise Scale", "50");
        var inOctaves = AddRowInt(perlinPanel.transform, "Octaves (1..8)", "4");
        var inLacunarity = AddRowFloat(perlinPanel.transform, "Lacunarity", "2");
        var inPersistence = AddRowFloat(perlinPanel.transform, "Persistence (0..1)", "0.5");
        var inTiers = AddRowInt(perlinPanel.transform, "Tiers (>=1)", "3");
        var inTierHeight = AddRowFloat(perlinPanel.transform, "Tier Height", "4");
        var ddEdgeMode = AddRowDropdown(perlinPanel.transform, "Edge Mode", new[] { "HardStep", "SmoothStep" }, 0);
        var inSmoothRange = AddRowFloat(perlinPanel.transform, "SmoothRange (0..1)", "0.1");

        // Campos BSP
        var inMapWidth = AddRowInt(bspPanel.transform, "Grid Width", "80");
        var inMapHeight = AddRowInt(bspPanel.transform, "Grid Height", "80");
        var inCellSize = AddRowFloat(bspPanel.transform, "Cell Size", "1");
        var inMinRoom = AddRowInt(bspPanel.transform, "Min Room", "4");
        var inMaxRoom = AddRowInt(bspPanel.transform, "Max Room", "8");
        var inMinLeaf = AddRowInt(bspPanel.transform, "Min Leaf", "8");
        var ddCorr = AddRowDropdown(bspPanel.transform, "Corridor Logic", new[] { "RandomOrder", "HorizontalFirst", "VerticalFirst", "StraightAxis" }, 0);
        var tgForbidMT = AddRowToggle(bspPanel.transform, "Forbid Multi-Tier Rooms", true);
        var inEnemies = AddRowInt(bspPanel.transform, "Enemies per Room", "2");

        // Campos Houses
        var tgSpawnH = AddRowToggle(housePanel.transform, "Spawn Houses", false);
        var inHMargin = AddRowInt(housePanel.transform, "House Margin", "1");
        var inHProb = AddRowFloat(housePanel.transform, "Fill Probability (0..1)", "1.0");
        var inHYOff = AddRowFloat(housePanel.transform, "House Y Offset", "0");

        // Campos Trees
        var ddMode = AddRowDropdown(treePanel.transform, "Spawn Mode", new[] { "PerRoom", "GlobalScatter" }, 0);
        var inIter = AddRowInt(treePanel.transform, "Iterations (1..6)", "3");
        var inStep = AddRowFloat(treePanel.transform, "Turtle Step", "0.25");
        var inAngle = AddRowFloat(treePanel.transform, "Turtle Angle", "22.5");
        var inTrees = AddRowInt(treePanel.transform, "Trees per Room", "2");
        var inGCount = AddRowInt(treePanel.transform, "Global Count", "30");
        var inRing = AddRowInt(treePanel.transform, "Ring Margin Cells", "1");
        var tgAvoidW = AddRowToggle(treePanel.transform, "Avoid Water", true);
        var tgAvoidR = AddRowToggle(treePanel.transform, "Avoid Rooms", true);

        // TabSwitcher
        var switcher = window.AddComponent<TabSwitcher>();
        switcher.tabButtons = buttons;
        switcher.tabPanels = panels;
        switcher.titleLabel = title;
        switcher.tabNames = tabNames;

        // GenerationUI + auto-wire refs de escena
        var genUI = window.AddComponent<GenerationUI>();
        genUI.perlin = FindObjectOfType<PerlinTerrainGenerator>();
        genUI.bsp = FindObjectOfType<BSPDungeonGenerator>();
        genUI.village = FindObjectOfType<VillageGenerator>();
        genUI.treeSpawner = FindObjectOfType<LSystemTreeSpawner>();
        genUI.btnRandomizeSeeds = btnRandom;
        genUI.btnRegenerate = btnRegen;

        // Wire de campos
        genUI.inWidth = inWidth; genUI.inDepth = inDepth; genUI.inNoiseScale = inNoiseScale;
        genUI.inOctaves = inOctaves; genUI.inLacunarity = inLacunarity; genUI.inPersistence = inPersistence;
        genUI.inTiers = inTiers; genUI.inTierHeight = inTierHeight; genUI.ddEdgeMode = ddEdgeMode; genUI.inSmoothRange = inSmoothRange;

        genUI.inMapWidth = inMapWidth; genUI.inMapHeight = inMapHeight; genUI.inCellSize = inCellSize;
        genUI.inMinRoom = inMinRoom; genUI.inMaxRoom = inMaxRoom; genUI.inMinLeaf = inMinLeaf;
        genUI.ddCorridorLogic = ddCorr; genUI.tgForbidMultiTier = tgForbidMT; genUI.inEnemiesPerRoom = inEnemies;

        genUI.tgSpawnHouses = tgSpawnH; genUI.inHouseMargin = inHMargin; genUI.inHouseProb = inHProb; genUI.inHouseYOffset = inHYOff;

        genUI.ddSpawnMode = ddMode; genUI.inTreeIterations = inIter; genUI.inTreeStep = inStep; genUI.inTreeAngle = inAngle;
        genUI.inTreesPerRoom = inTrees; genUI.inGlobalCount = inGCount; genUI.inRingMargin = inRing;
        genUI.tgAvoidWater = tgAvoidW; genUI.tgAvoidRooms = tgAvoidR;

        // Guardar prefab
        if (!Directory.Exists(DIR)) Directory.CreateDirectory(DIR);
        var prefab = PrefabUtility.SaveAsPrefabAsset(canvasGO, PATH, out bool ok);
        if (ok)
        {
            Selection.activeObject = prefab; EditorGUIUtility.PingObject(prefab);
            Debug.Log("PCG UI (Tabs) guardado: " + PATH);
        }
        else Debug.LogWarning("No se pudo guardar el prefab.");

        Undo.RegisterCreatedObjectUndo(canvasGO, "Create PCG UI Tabs");
    }

    // ------- helpers -------
    static GameObject Create(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
        return go;
    }

    static Button MakeTabButton(Transform parent, string text)
    {
        var go = Create(text + "_Tab", parent);
        var img = go.AddComponent<Image>(); img.color = new Color(1, 1, 1, 0.10f);
        var btn = go.AddComponent<Button>();
        var lab = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        lab.transform.SetParent(go.transform, false);
        lab.text = text; lab.fontSize = 18; lab.color = Color.white; lab.fontStyle = FontStyles.Bold;
        var rt = lab.GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var le = go.AddComponent<LayoutElement>(); le.minHeight = 40;
        return btn;
    }

    static GameObject MakeTabPanel(Transform parent, string name)
    {
        var panel = Create(name, parent);
        var v = panel.AddComponent<VerticalLayoutGroup>();
        v.spacing = 6; v.childForceExpandHeight = false; v.childForceExpandWidth = true;
        var img = panel.AddComponent<Image>(); img.color = new Color(1, 1, 1, 0.025f);
        return panel;
    }

    static TMP_InputField AddRowInt(Transform parent, string label, string def)
    {
        var row = Row(parent, label, out var text);
        var input = MakeInput(row.transform, true, def);
        return input;
    }
    static TMP_InputField AddRowFloat(Transform parent, string label, string def)
    {
        var row = Row(parent, label, out var text);
        var input = MakeInput(row.transform, false, def);
        return input;
    }
    static TMP_Dropdown AddRowDropdown(Transform parent, string label, string[] options, int def)
    {
        var row = Row(parent, label, out var _);
        var ddGO = Create(label + "_Dropdown", row.transform);
        ddGO.AddComponent<Image>().color = new Color(0, 0, 0, 0.35f);
        var dd = ddGO.AddComponent<TMP_Dropdown>();
        dd.options.Clear();
        foreach (var o in options) dd.options.Add(new TMP_Dropdown.OptionData(o));
        dd.value = Mathf.Clamp(def, 0, dd.options.Count - 1);
        LayoutElement(ddGO).preferredWidth = 240;
        return dd;
    }
    static Toggle AddRowToggle(Transform parent, string label, bool def)
    {
        var row = Row(parent, label, out var _);
        var togGO = Create(label + "_Toggle", row.transform);
        togGO.AddComponent<Image>().color = new Color(0, 0, 0, 0.35f);
        var tog = togGO.AddComponent<Toggle>(); tog.isOn = def;
        LayoutElement(togGO).preferredWidth = 60;
        return tog;
    }

    static GameObject Row(Transform parent, string label, out TextMeshProUGUI lab)
    {
        var row = Create(label + "_Row", parent);
        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 8; h.childForceExpandHeight = false; h.childForceExpandWidth = true;
        lab = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        lab.transform.SetParent(row.transform, false);
        lab.text = label; lab.color = Color.white; lab.fontSize = 18; lab.enableAutoSizing = false;
        LayoutElement(lab.gameObject).preferredWidth = 260;
        return row;
    }

    static TMP_InputField MakeInput(Transform parent, bool integer, string def)
    {
        var inputGO = Create("Input", parent);
        inputGO.AddComponent<Image>().color = new Color(0, 0, 0, 0.35f);
        var input = inputGO.AddComponent<TMP_InputField>();
        var area = Create("TextArea", inputGO.transform);
        var text = area.AddComponent<TextMeshProUGUI>(); text.fontSize = 18; text.color = Color.white;
        input.textViewport = area.GetComponent<RectTransform>();
        input.textComponent = text;
        input.contentType = integer ? TMP_InputField.ContentType.IntegerNumber : TMP_InputField.ContentType.DecimalNumber;
        input.text = def;
        LayoutElement(inputGO).preferredWidth = 240;
        return input;
    }

    static LayoutElement LayoutElement(GameObject go)
    {
        var le = go.GetComponent<LayoutElement>();
        if (!le) le = go.AddComponent<LayoutElement>();
        return le;
    }

    static Button MakeButton(Transform parent, string text)
    {
        var btnGO = Create(text + "_Button", parent);
        var img = btnGO.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.15f);

        var btn = btnGO.AddComponent<Button>();

        var lab = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        lab.transform.SetParent(btnGO.transform, false);
        lab.text = text;
        lab.fontSize = 18;
        lab.fontStyle = FontStyles.Bold;
        lab.color = Color.white;
        lab.alignment = TextAlignmentOptions.Center;

        // que el label llene el botón
        var rt = lab.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // tamaño por defecto para el layout
        var le = btnGO.AddComponent<LayoutElement>();
        le.preferredWidth = 180;
        le.preferredHeight = 40;

        return btn;
    }
}
#endif