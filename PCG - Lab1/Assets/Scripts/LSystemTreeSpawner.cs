using System.Collections.Generic;
using UnityEngine;

public class LSystemTreeSpawner : MonoBehaviour
{
    public enum SpawnMode { PerRoom = 0, GlobalScatter = 1 }

    [Header("Refs")]
    public PerlinTerrainGenerator perlin;
    public BSPDungeonGenerator bsp;
    public LSystem lsystem;                 // reglas Árbol 3D
    public TurtleDrawer turtlePrefab;       // prefab con TurtleDrawer

    [Header("Parámetros L-System")]
    [Range(1, 6)] public int iterations = 3;
    public float turtleStep = 0.02f;
    public float turtleAngle = 22.5f;
    public float baseYOffset = 0.02f;

    [Header("Distribución")]
    public SpawnMode spawnMode = SpawnMode.PerRoom;
    // PerRoom
    public int treesPerRoom = 2;
    public int ringMarginCells = 1;
    // GlobalScatter
    public int globalCount = 30;

    [Header("Filtros")]
    public bool avoidWater = true;
    public bool avoidRooms = true;
    public Vector2 jitterXZ = new Vector2(0.2f, 0.2f);

    Transform treesParent;

    public void Clear()
    {
        if (treesParent != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(treesParent.gameObject);
            else Destroy(treesParent.gameObject);
#else
            Destroy(treesParent.gameObject);
#endif
        }
    }

    public void RegenerateTrees()
    {
        if (!perlin || !bsp || !lsystem || !turtlePrefab) return;

        Clear();
        treesParent = new GameObject("Trees").transform;
        treesParent.SetParent(transform, false);

        Ensure3DTreeRules(lsystem);
        Vector3 origin = perlin.transform.position;

        if (spawnMode == SpawnMode.PerRoom)
            SpawnPerRoom(origin);
        else
            SpawnGlobal(origin);
    }

    void SpawnPerRoom(Vector3 origin)
    {
        var rooms = bsp.GetRooms();
        foreach (var r in rooms)
        {
            var ring = CollectRingCells(r, ringMarginCells);
            int placed = 0, safety = 500;

            while (placed < treesPerRoom && safety-- > 0 && ring.Count > 0)
            {
                int idx = Random.Range(0, ring.Count);
                var cell = ring[idx];
                ring.RemoveAt(idx);
                if (!CellAllowed(cell, rooms)) continue;
                PlaceTreeAtCell(origin, cell);
                placed++;
            }
        }
    }

    void SpawnGlobal(Vector3 origin)
    {
        int H = Mathf.Min(perlin.depth, bsp.height);
        int W = Mathf.Min(perlin.width, bsp.width);

        var rooms = bsp.GetRooms();
        int placed = 0, safety = 10000;

        while (placed < globalCount && safety-- > 0)
        {
            int y = Random.Range(0, H);
            int x = Random.Range(0, W);
            var cell = new Vector2Int(y, x);
            if (!CellAllowed(cell, rooms)) continue;
            PlaceTreeAtCell(origin, cell);
            placed++;
        }
    }

    void PlaceTreeAtCell(Vector3 origin, Vector2Int cell)
    {
        float h01 = perlin.Height01Raw(cell.x, cell.y); // (fila, col)
        int tier = perlin.GetTierIndexFrom01(h01);
        float y = perlin.GetTierFlatY(tier) + baseYOffset;

        Vector3 pos = origin + new Vector3((cell.y + 0.5f) * bsp.cellSize, y, (cell.x + 0.5f) * bsp.cellSize);
        if (jitterXZ != Vector2.zero)
        {
            pos.x += Random.Range(-jitterXZ.x, jitterXZ.x);
            pos.z += Random.Range(-jitterXZ.y, jitterXZ.y);
        }

        var turtle = Instantiate(turtlePrefab, treesParent);
        turtle.worldSpaceLines = true;
        turtle.is3D = true;
        turtle.step = 0.1f;
        turtle.angle = turtleAngle;

        string seq = lsystem.Generate(iterations);
        Quaternion upRot = Quaternion.LookRotation(Vector3.up, Vector3.forward);
        turtle.DrawAt(seq, pos, upRot);
    }

    bool CellAllowed(Vector2Int cell, List<BSPDungeonGenerator.Room> rooms)
    {
        // recorte a mapa
        if (cell.x < 0 || cell.x >= bsp.height || cell.y < 0 || cell.y >= bsp.width) return false;

        if (avoidWater)
        {
            float h01 = perlin.Height01Raw(cell.x, cell.y);
            if (perlin.GetTierIndexFrom01(h01) == 0) return false;
        }

        if (avoidRooms)
        {
            foreach (var r in rooms)
            {
                if (cell.y >= r.x && cell.y < r.x + r.w &&
                    cell.x >= r.y && cell.x < r.y + r.h)
                    return false;
            }
        }
        return true;
    }

    List<Vector2Int> CollectRingCells(BSPDungeonGenerator.Room r, int margin)
    {
        var list = new List<Vector2Int>();
        int y0 = r.y - margin, y1 = r.y + r.h + margin - 1;
        int x0 = r.x - margin, x1 = r.x + r.w + margin - 1;

        for (int x = x0; x <= x1; x++) { list.Add(new Vector2Int(y0, x)); list.Add(new Vector2Int(y1, x)); }
        for (int y = y0 + 1; y <= y1 - 1; y++) { list.Add(new Vector2Int(y, x0)); list.Add(new Vector2Int(y, x1)); }

        // clamp a límites del mapa
        int H = bsp.height, W = bsp.width;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var c = list[i];
            if (c.x < 0 || c.x >= H || c.y < 0 || c.y >= W) list.RemoveAt(i);
        }
        return list;
    }

    void Ensure3DTreeRules(LSystem sys)
    {
        // Fuerza preset Árbol 3D si no está
        if (sys.rules.Count == 2 && sys.axiom == "X") return;
        sys.rules.Clear();
        sys.axiom = "X";
        sys.rules.Add(new LRule { symbol = 'X', productions = new[] { "F[+X][-X]&X" } });
        sys.rules.Add(new LRule { symbol = 'F', productions = new[] { "FF" } });
        sys.seed = Random.Range(int.MinValue / 2, int.MaxValue / 2);
        sys.MarkDirty();
    }
}