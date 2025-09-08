using System.Collections.Generic;
using UnityEngine;

public class VillageGenerator : MonoBehaviour
{
    [Header("Refs")]
    public PerlinTerrainGenerator perlin;
    public BSPDungeonGenerator bsp;

    [Header("Grid")]
    public int gridWidth = 80, gridHeight = 80;
    public float cellSize = 1f;

    [Header("Agua")]
    public GameObject waterTilePrefab; // 1x1 en XZ, pivot en Base ideal
    public float waterYOffset = 0.01f;
    Transform waterParent;

    void Start() => GenerateVillage();

    public void GenerateVillage()
    {
        // 1) Generar terreno
        perlin.Generate();

        // 2) Configurar máscaras:
        // Agua = tier 0
        perlin.waterTierIndices = new List<int> { 0 };

        // Construible = todos los tiers excepto el más bajo,
        // salvo caso especial de 1 tier (construir en ese único plano).
        var buildable = new List<int>();
        if (perlin.tiers <= 1) buildable.Add(0);
        else for (int i = 1; i < perlin.tiers; i++) buildable.Add(i);
        perlin.buildableTierIndices = buildable;

        // Caminable = todo menos agua
        var land = new List<int>();
        for (int i = 1; i < perlin.tiers; i++) land.Add(i);
        perlin.landTierIndices = land;

        // 3) Sincronizar BSP
        bsp.width = gridWidth; bsp.height = gridHeight;
        bsp.cellSize = cellSize;
        bsp.perlinRef = perlin;
        bsp.yOffsetOnTerrain = 0.02f;
        bsp.floorsUseTierFlatY = true;

        // 4) Pasar máscaras
        var buildableMask = perlin.GetBuildableMask();
        var landMask = perlin.GetLandMask();
        var waterMask = perlin.GetWaterMask();

        bsp.buildableMask = buildableMask;
        //bsp.passableMaskForCorridors = landMask; // si luego usas pathfinding

        // 5) Generar BSP
        bsp.Generate();
        if (bsp.buildPrefabsOnPlay) bsp.BuildPrefabs();

        // 6) Construir agua visible
        BuildWaterTiles(waterMask);
    }

    void BuildWaterTiles(bool[,] waterMask)
    {
        if (waterTilePrefab == null || waterMask == null) return;

        // limpia anterior
        if (waterParent != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(waterParent.gameObject);
            else Destroy(waterParent.gameObject);
#else
        Destroy(waterParent.gameObject);
#endif
        }
        waterParent = new GameObject("WaterTiles").transform;
        waterParent.SetParent(transform, false);

        // ------------- CLAVES -------------
        // 1) Origen: el del terreno (para que coincida con la malla)
        Vector3 origin = perlin.transform.position;

        // 2) Recorte: no más allá del grid de BSP
        int Hmask = waterMask.GetLength(0);
        int Wmask = waterMask.GetLength(1);
        int H = Mathf.Min(Hmask, gridHeight);
        int W = Mathf.Min(Wmask, gridWidth);

        // 3) Evitar la última fila/col del noise (+1) si tu grid usa celdas [0..W-1]
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                if (!waterMask[y, x]) continue;

                float wy = perlin.GetTierFlatY(0) + waterYOffset;
                Vector3 pos = origin + new Vector3(x * cellSize, wy, y * cellSize);
                Instantiate(waterTilePrefab, pos, Quaternion.identity, waterParent);
            }
        }
    }
}