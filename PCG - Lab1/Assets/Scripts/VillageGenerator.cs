using System.Collections.Generic;
using UnityEngine;

public class VillageGenerator : MonoBehaviour
{
    [Header("Refs")]
    public PerlinTerrainGenerator perlin;
    public BSPDungeonGenerator bsp;
    public LSystem lsystem;
    public TurtleDrawer turtlePrefab;

    [Header("Grid")]
    public int gridWidth = 80, gridHeight = 80;
    public float cellSize = 1f;

    [Header("Árboles / deco")]
    public int treesPerRoom = 8;
    public float minTreeDistFromRoom = 2f;
    public int treePlacementJitter = 5;
    public float decoYOffset = 0.02f; // leve offset sobre terreno

    [Header("Umbrales de terreno")]
    [Range(0f, 1f)] public float waterThreshold = 0.30f;
    public Vector2 plainBand = new Vector2(0.35f, 0.55f);
    [Range(0f, 0.5f)] public float maxPlainSlope = 0.10f;

    [Header("Alturas mundo")]
    public float yOffset = 0.0f;  // elevar ligeramente los árboles/markers

    // --- Internos ---
    bool[,] waterMask, plainMask, landMask;

    void Start()
    {
        GenerateVillage();
    }

    public void GenerateVillage()
    {
        // 1) Asegura que el Perlin esté generado
        perlin.Generate(); // esto rellena noiseMap, minH, maxH

        // 2) Sincroniza tamaños
        bsp.width = gridWidth; bsp.height = gridHeight;
        bsp.cellSize = cellSize;
        bsp.perlinRef = perlin;
        bsp.yOffsetOnTerrain = 0.02f;

        // 3) Máscaras por tier
        var buildableMask = perlin.GetBuildableMask();
        var landMask = perlin.GetLandMask();
        var waterMask = perlin.GetWaterMask();

        // 4) Pásale sólo la "construible" al BSP (las salas no caerán sobre agua/pendiente alta)
        bsp.buildableMask = buildableMask;

        // 5) Genera el BSP
        bsp.Generate();
        if (bsp.buildPrefabsOnPlay) bsp.BuildPrefabs();

        // 6) Coloca árboles/deco sólo en tierra y fuera de salas
        PlaceTreesAroundRooms(landMask);
    }

    void PlaceTreesAroundRooms(bool[,] landMask)
    {
        var rooms = bsp.GetRooms();
        var rnd = new System.Random(bsp.seed ^ 0x51F1A);

        int H = landMask.GetLength(0);
        int W = landMask.GetLength(1);

        bool InB(int y, int x) => y >= 0 && y < H && x >= 0 && x < W;

        foreach (var room in rooms)
        {
            if (room == null) continue;

            int placed = 0, tries = 0, maxTries = treesPerRoom * 50;
            while (placed < treesPerRoom && tries < maxTries)
            {
                tries++;
                int cx = room.CenterX, cy = room.CenterY;
                int tx = cx + rnd.Next(-treePlacementJitter, treePlacementJitter + 1);
                int ty = cy + rnd.Next(-treePlacementJitter, treePlacementJitter + 1);

                // fuera del rectángulo de la sala + margen
                bool tooClose =
                    tx >= room.x - minTreeDistFromRoom && tx < room.x + room.w + minTreeDistFromRoom &&
                    ty >= room.y - minTreeDistFromRoom && ty < room.y + room.h + minTreeDistFromRoom;
                if (tooClose || !InB(ty, tx) || !landMask[ty, tx]) continue;

                // Altura EXACTA del terreno por tier
                float y = perlin.GetWorldY(ty, tx) + decoYOffset;
                var td = Instantiate(turtlePrefab, transform);
                td.transform.position = new Vector3(tx * cellSize, y, ty * cellSize);

                var seq = lsystem.Generate(Random.Range(3, 5));
                td.Draw(seq);

                placed++;
            }
        }
    }

    float SampleTerrainHeight01(int z, int x)
    {
        var h01 = perlin.GetHeight01Map();
        z = Mathf.Clamp(z, 0, h01.GetLength(0) - 1);
        x = Mathf.Clamp(x, 0, h01.GetLength(1) - 1);
        return h01[z, x];
    }
}