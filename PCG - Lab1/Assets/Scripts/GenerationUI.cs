using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GenerationUI : MonoBehaviour
{
    [Header("Refs")]
    public PerlinTerrainGenerator perlin;
    public BSPDungeonGenerator bsp;
    public VillageGenerator village;
    public LSystemTreeSpawner treeSpawner;

    [Header("Common UI")]
    public Button btnRegenerate;
    public Button btnRandomizeSeeds;

    // ---- Perlin UI ----
    [Header("Perlin UI")]
    public TMP_InputField inWidth, inDepth, inNoiseScale, inOctaves, inLacunarity, inPersistence, inTiers, inTierHeight, inSmoothRange;
    public TMP_Dropdown ddEdgeMode;

    // ---- BSP UI ----
    [Header("BSP UI")]
    public TMP_InputField inMapWidth, inMapHeight, inCellSize, inMinRoom, inMaxRoom, inMinLeaf, inEnemiesPerRoom;
    public TMP_Dropdown ddCorridorLogic;
    public Toggle tgForbidMultiTier;

    // ---- Houses UI ----
    [Header("Houses UI")]
    public Toggle tgSpawnHouses;
    public TMP_InputField inHouseMargin, inHouseProb, inHouseYOffset;

    // ---- L-System UI ----
    [Header("L-System UI")]
    public TMP_Dropdown ddSpawnMode;
    public TMP_InputField inTreeIterations, inTreeStep, inTreeAngle, inTreesPerRoom, inGlobalCount, inRingMargin;
    public Toggle tgAvoidWater, tgAvoidRooms;

    void Awake()
    {
        if (btnRegenerate) btnRegenerate.onClick.AddListener(OnRegenerate);
        if (btnRandomizeSeeds) btnRandomizeSeeds.onClick.AddListener(OnRandomizeSeeds);
    }
    void OnDestroy()
    {
        if (btnRegenerate) btnRegenerate.onClick.RemoveListener(OnRegenerate);
        if (btnRandomizeSeeds) btnRandomizeSeeds.onClick.RemoveListener(OnRandomizeSeeds);
    }

    void OnRandomizeSeeds()
    {
        if (perlin) perlin.RandomizeSeed();
        if (bsp) bsp.useRandomSeed = true;
    }

    void OnRegenerate()
    {
        if (!perlin || !bsp || !village || !treeSpawner) return;

        // === PERLIN ===
        perlin.width = ParseInt(inWidth, perlin.width);
        perlin.depth = ParseInt(inDepth, perlin.depth);
        perlin.noiseScale = ParseFloat(inNoiseScale, perlin.noiseScale);
        perlin.octaves = Mathf.Clamp(ParseInt(inOctaves, perlin.octaves), 1, 8);
        perlin.lacunarity = Mathf.Max(0.01f, ParseFloat(inLacunarity, perlin.lacunarity));
        perlin.persistence = Mathf.Clamp01(ParseFloat(inPersistence, perlin.persistence));
        perlin.tiers = Mathf.Max(1, ParseInt(inTiers, perlin.tiers));
        perlin.tierHeight = Mathf.Max(0.01f, ParseFloat(inTierHeight, perlin.tierHeight));
        if (ddEdgeMode) perlin.edgeMode = (PerlinTerrainGenerator.TierEdgeMode)ddEdgeMode.value;
        perlin.smoothRange = Mathf.Clamp01(ParseFloat(inSmoothRange, perlin.smoothRange));

        // === BSP ===
        bsp.width = ParseInt(inMapWidth, bsp.width);
        bsp.height = ParseInt(inMapHeight, bsp.height);
        bsp.cellSize = ParseFloat(inCellSize, bsp.cellSize);
        bsp.minRoomSize = ParseInt(inMinRoom, bsp.minRoomSize);
        bsp.maxRoomSize = ParseInt(inMaxRoom, bsp.maxRoomSize);
        bsp.minLeafSize = ParseInt(inMinLeaf, bsp.minLeafSize);
        if (ddCorridorLogic) bsp.corridorLogic = (BSPDungeonGenerator.CorridorLogic)ddCorridorLogic.value;
        if (tgForbidMultiTier) bsp.forbidMultiTierRooms = tgForbidMultiTier.isOn;
        bsp.enemiesPerRoom = ParseInt(inEnemiesPerRoom, bsp.enemiesPerRoom);

        // === Houses ===
        if (tgSpawnHouses) bsp.spawnHouses = tgSpawnHouses.isOn;
        bsp.houseMargin = Mathf.Max(0, ParseInt(inHouseMargin, bsp.houseMargin));
        bsp.houseFillProbability = Mathf.Clamp01(ParseFloat(inHouseProb, bsp.houseFillProbability));
        bsp.houseYOffset = ParseFloat(inHouseYOffset, bsp.houseYOffset);

        // 1) Generar terreno + BSP + agua
        village.GenerateVillage();

        // === L-System ===
        if (ddSpawnMode) treeSpawner.spawnMode = (LSystemTreeSpawner.SpawnMode)ddSpawnMode.value;
        treeSpawner.iterations = Mathf.Clamp(ParseInt(inTreeIterations, treeSpawner.iterations), 1, 6);
        treeSpawner.turtleStep = ParseFloat(inTreeStep, treeSpawner.turtleStep);
        treeSpawner.turtleAngle = ParseFloat(inTreeAngle, treeSpawner.turtleAngle);
        treeSpawner.treesPerRoom = Mathf.Max(0, ParseInt(inTreesPerRoom, treeSpawner.treesPerRoom));
        treeSpawner.globalCount = Mathf.Max(0, ParseInt(inGlobalCount, treeSpawner.globalCount));
        treeSpawner.ringMarginCells = Mathf.Clamp(ParseInt(inRingMargin, treeSpawner.ringMarginCells), 0, 6);
        if (tgAvoidWater) treeSpawner.avoidWater = tgAvoidWater.isOn;
        if (tgAvoidRooms) treeSpawner.avoidRooms = tgAvoidRooms.isOn;

        // 2) Generar árboles
        treeSpawner.RegenerateTrees();
    }

    int ParseInt(TMP_InputField f, int def) => (f && int.TryParse(f.text, out var v)) ? v : def;
    float ParseFloat(TMP_InputField f, float d) => (f && float.TryParse(f.text, out var v)) ? v : d;
}