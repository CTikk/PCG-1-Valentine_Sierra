using System;
using System.Collections.Generic;
using UnityEngine;

public class BSPDungeonGenerator : MonoBehaviour
{
    [Header("Tamaño mapa (celdas)")]
    public int width = 40;
    public int height = 20;

    [Header("Salas")]
    public int minRoomSize = 4;
    public int maxRoomSize = 8;
    public int minLeafSize = 8;

    [Header("Pasillos")]
    public CorridorLogic corridorLogic = CorridorLogic.RandomOrder;

    [Header("Semilla")]
    public int seed = 0;
    public bool useRandomSeed = true;

    [Header("Prefabs (muros / marcadores / fallback piso)")]
    public GameObject floorPrefab;          // ← respaldo si no asignas los de abajo
    public GameObject wallPrefab;
    public GameObject playerMarkerPrefab;
    public GameObject exitMarkerPrefab;
    public GameObject enemyPrefab;
    public int enemiesPerRoom = 5;

    [Header("Prefabs de piso")]
    public GameObject roomFloorPrefab;      // ← HABITACIÓN
    public GameObject corridorFloorPrefab;  // ← PASILLO

    [Header("Visualización")]
    public bool drawGizmos = true;
    public bool buildPrefabsOnPlay = true;
    public float cellSize = 1f;

    [Header("Casas (opcional)")]
    public bool spawnHouses = false;           // activar/desactivar
    public GameObject housePrefab;             // prefab a instanciar en celdas interiores
    [Range(0, 3)] public int houseMargin = 1;  // nº de celdas de margen con los bordes de la habitación
    [Range(0f, 1f)] public float houseFillProbability = 1f; // 1 = todas las celdas interiores
    public float houseYOffset = 0f;            // pequeño lift si hace falta
    public PrefabPivot housePivot = PrefabPivot.Base; // cómo corregir el pivot
    public Vector2 houseJitter = new Vector2(0f, 0f); // desplazamiento aleatorio dentro de la celda

    [Header("Símbolos del mapa")]
    public char floorChar = '.';    // pasillo
    public char roomChar = 'r';    // habitación
    public char wallChar = '#';
    public char enemyChar = 'E';
    public char playerChar = 'P';
    public char exitChar = 'S';

    // ===== Integración con terreno / tiers =====
    public enum PrefabPivot { Center, Base }

    [Header("Integración con terreno")]
    public PerlinTerrainGenerator perlinRef;
    public float yOffsetOnTerrain = 0.02f;
    public bool floorsUseTierFlatY = true;

    [Header("Pivotes de prefabs")]
    public PrefabPivot floorPivot = PrefabPivot.Center;
    public PrefabPivot wallPivot = PrefabPivot.Center;
    public float floorExtraLift = 0f;
    public float wallExtraSink = 0f;

    [Header("Máscaras externas")]
    public bool[,] buildableMask;             // dónde pueden ir salas (ej. todos los tiers menos agua)

    [Header("Restricciones de salas")]
    public bool forbidMultiTierRooms = true;   // no permitir salas que crucen tiers

    // --- Internos ---
    private System.Random prng;
    private char[,] map;          // [y, x]
    private Leaf root;
    private List<Leaf> leaves = new();
    private List<Room> rooms = new();
    private Transform builtParent;

    // Estructuras
    [Serializable]
    public class Room
    {
        public int x, y, w, h;
        public int CenterX => x + w / 2;
        public int CenterY => y + h / 2;
    }

    public class Leaf
    {
        public int x, y, w, h;
        public Leaf left;
        public Leaf right;
        public Room room;

        public Leaf(int x, int y, int w, int h)
        { this.x = x; this.y = y; this.w = w; this.h = h; }
    }

    public enum CorridorLogic { RandomOrder, HorizontalFirst, VerticalFirst, StraightAxis }

    // ============= Ciclo de vida =============
    void Start()
    {
        EnsurePRNG();
        Generate();
        if (buildPrefabsOnPlay) BuildPrefabs();
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos || map == null) return;

        Vector3 origin = perlinRef != null ? perlinRef.transform.position : transform.position;

        for (int y = 0; y < map.GetLength(0); y++)
        {
            for (int x = 0; x < map.GetLength(1); x++)
            {
                Vector3 pos = origin + new Vector3(x * cellSize, 0f, y * cellSize);
                char c = map[y, x];
                if (c == wallChar)
                {
                    Gizmos.color = new Color(0.15f, 0.15f, 0.15f);
                    Gizmos.DrawCube(pos + Vector3.up * 0.25f, new Vector3(cellSize, 0.5f, cellSize));
                }
                else
                {
                    Gizmos.color = (c == roomChar) ? new Color(0.9f, 0.9f, 0.5f) : new Color(0.7f, 0.7f, 0.7f);
                    Gizmos.DrawCube(pos + Vector3.up * 0.05f, new Vector3(cellSize, 0.1f, cellSize));
                }
            }
        }

        Gizmos.color = Color.cyan;
        if (rooms != null)
        {
            foreach (var r in rooms)
            {
                Vector3 origin2 = perlinRef != null ? perlinRef.transform.position : transform.position;
                Vector3 p = origin2 + new Vector3(r.x * cellSize, 0f, r.y * cellSize);
                Vector3 size = new Vector3(r.w * cellSize, 0.03f, r.h * cellSize);
                Gizmos.DrawWireCube(p + new Vector3(size.x * 0.5f, 0.2f, size.z * 0.5f), size);
            }
        }
    }

    // ============= Generación principal =============
    public void Generate()
    {
        EnsurePRNG();

        rooms.Clear();
        leaves.Clear();
        root = new Leaf(0, 0, width, height);
        leaves.Add(root);

        // mapa lleno de muros
        map = new char[height, width];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                map[y, x] = wallChar;

        // 1) Dividir hojas
        SplitAllLeaves();

        // 2) Crear salas en hojas
        CreateRooms(root);

        // 3) Volcar salas al mapa
        FillRooms(root);

        // 4) Conectar por centros
        int conexiones = 0;
        ConnectLeafRooms(root, ref conexiones);

        // 5) Player, enemigos, salida
        PlacePlayer();
        PlaceEnemies(enemiesPerRoom);
        PlaceExit();
    }

    void EnsurePRNG()
    {
        if (useRandomSeed) seed = UnityEngine.Random.Range(int.MinValue / 2, int.MaxValue / 2);
        if (prng == null) prng = new System.Random(seed);
    }

    // ============= BSP: Split =============
    void SplitAllLeaves()
    {
        bool didSplit;
        do
        {
            didSplit = false;
            List<Leaf> newLeaves = new();
            foreach (var leaf in leaves)
            {
                if (leaf.left == null && leaf.right == null)
                {
                    if (leaf.w > minLeafSize * 2 || leaf.h > minLeafSize * 2)
                    {
                        if (SplitLeaf(leaf))
                        {
                            newLeaves.Add(leaf.left);
                            newLeaves.Add(leaf.right);
                            didSplit = true;
                        }
                    }
                }
            }
            leaves.AddRange(newLeaves);
        } while (didSplit);
    }

    bool SplitLeaf(Leaf leaf)
    {
        if (leaf.left != null || leaf.right != null) return false;

        bool splitH = (leaf.w <= leaf.h);
        if (leaf.w / (float)leaf.h >= 1.25f) splitH = false;
        else if (leaf.h / (float)leaf.w >= 1.25f) splitH = true;

        int max = splitH ? leaf.h : leaf.w;
        if (max < minLeafSize * 2) return false;

        int split = Range(minLeafSize, max - minLeafSize);
        if (splitH)
        {
            leaf.left = new Leaf(leaf.x, leaf.y, leaf.w, split);
            leaf.right = new Leaf(leaf.x, leaf.y + split, leaf.w, leaf.h - split);
        }
        else
        {
            leaf.left = new Leaf(leaf.x, leaf.y, split, leaf.h);
            leaf.right = new Leaf(leaf.x + split, leaf.y, leaf.w - split, leaf.h);
        }
        return true;
    }

    // ============= Rooms =============
    void CreateRooms(Leaf leaf)
    {
        if (leaf.left != null || leaf.right != null)
        {
            if (leaf.left != null) CreateRooms(leaf.left);
            if (leaf.right != null) CreateRooms(leaf.right);
            return;
        }

        int maxW = Mathf.Min(maxRoomSize, leaf.w - 2);
        int maxH = Mathf.Min(maxRoomSize, leaf.h - 2);
        if (minRoomSize > maxW || minRoomSize > maxH) return;

        int rw = Mathf.Min(Range(minRoomSize, maxW), leaf.w - 2);
        int rh = Mathf.Min(Range(minRoomSize, maxH), leaf.h - 2);

        int rx = Range(leaf.x + 1, leaf.x + leaf.w - rw - 1);
        int ry = Range(leaf.y + 1, leaf.y + leaf.h - rh - 1);

        var candidate = new Room { x = rx, y = ry, w = rw, h = rh };

        if (!RoomAllowed(candidate))
        {
            bool placed = false;
            for (int t = 0; t < 25 && !placed; t++)
            {
                rx = Range(leaf.x + 1, Mathf.Max(leaf.x + 1, leaf.x + leaf.w - rw - 1));
                ry = Range(leaf.y + 1, Mathf.Max(leaf.y + 1, leaf.y + leaf.h - rh - 1));
                candidate = new Room { x = rx, y = ry, w = rw, h = rh };
                if (RoomAllowed(candidate)) placed = true;
            }
            if (!placed) { leaf.room = null; return; }
        }

        leaf.room = candidate;
    }

    bool RoomAllowed(Room r)
    {
        // máscara de construible
        if (buildableMask != null)
        {
            for (int y = r.y; y < r.y + r.h; y++)
                for (int x = r.x; x < r.x + r.w; x++)
                    if (!(y >= 0 && y < buildableMask.GetLength(0) &&
                          x >= 0 && x < buildableMask.GetLength(1) &&
                          buildableMask[y, x]))
                        return false;
        }

        // un solo tier (si está activado)
        if (forbidMultiTierRooms && perlinRef != null)
        {
            int? tier = null;
            for (int y = r.y; y < r.y + r.h; y++)
            {
                for (int x = r.x; x < r.x + r.w; x++)
                {
                    float h01 = perlinRef.Height01Raw(y, x);
                    int ti = perlinRef.GetTierIndexFrom01(h01);
                    if (tier == null) tier = ti;
                    else if (ti != tier.Value) return false;
                }
            }
        }

        return true;
    }

    void FillRooms(Leaf leaf)
    {
        if (leaf.left != null || leaf.right != null)
        {
            if (leaf.left != null) FillRooms(leaf.left);
            if (leaf.right != null) FillRooms(leaf.right);
            return;
        }

        if (leaf.room != null)
        {
            rooms.Add(leaf.room);
            for (int y = leaf.room.y; y < leaf.room.y + leaf.room.h; y++)
                for (int x = leaf.room.x; x < leaf.room.x + leaf.room.w; x++)
                    map[y, x] = roomChar; // ← piso de HABITACIÓN
        }
    }

    void BuildHouses(Vector3 origin)
    {
        if (!spawnHouses || housePrefab == null || rooms == null || rooms.Count == 0)
            return;

        Transform housesParent = new GameObject("Houses").transform;
        housesParent.SetParent(builtParent != null ? builtParent : transform, false);

        foreach (var r in rooms)
        {
            // dimensiones interiores válidas
            int innerW = r.w - houseMargin * 2;
            int innerH = r.h - houseMargin * 2;
            if (innerW <= 0 || innerH <= 0) continue;

            for (int y = r.y + houseMargin; y < r.y + r.h - houseMargin; y++)
            {
                for (int x = r.x + houseMargin; x < r.x + r.w - houseMargin; x++)
                {
                    // Solo sobre celdas de habitación (no pasillo / ni marcadores)
                    char c = map[y, x];
                    if (c != roomChar) continue;
                    if (UnityEngine.Random.value > houseFillProbability) continue;

                    float baseY = WorldYForCell(y, x, true); // usa tier plano de esa celda
                    float yFinal = ApplyPivotOffsetY(housePrefab, housePivot, baseY, houseYOffset);

                    // posición en el centro de la celda con pequeño jitter opcional
                    Vector3 pos = origin + new Vector3((x + 0.5f) * cellSize, yFinal,
                                                       (y + 0.5f) * cellSize);

                    if (houseJitter != Vector2.zero)
                    {
                        float jx = UnityEngine.Random.Range(-houseJitter.x, houseJitter.x);
                        float jz = UnityEngine.Random.Range(-houseJitter.y, houseJitter.y);
                        pos.x += jx; pos.z += jz;
                    }

                    Instantiate(housePrefab, pos, Quaternion.identity, housesParent);
                }
            }
        }
    }

    // ============= Conexión por centros =============
    void ConnectLeafRooms(Leaf leaf, ref int conexiones)
    {
        if (leaf.left == null || leaf.right == null) return;

        Room a = FindAnyRoom(leaf.left);
        Room b = FindAnyRoom(leaf.right);

        if (a != null && b != null)
        {
            ConnectRooms(a, b);
            conexiones++;
        }

        ConnectLeafRooms(leaf.left, ref conexiones);
        ConnectLeafRooms(leaf.right, ref conexiones);
    }

    Room FindAnyRoom(Leaf leaf)
    {
        Stack<Leaf> stack = new();
        stack.Push(leaf);
        while (stack.Count > 0)
        {
            var l = stack.Pop();
            if (l.room != null) return l.room;
            if (l.left != null) stack.Push(l.left);
            if (l.right != null) stack.Push(l.right);
        }
        return null;
    }

    void ConnectRooms(Room a, Room b)
    {
        int x1 = a.CenterX, y1 = a.CenterY;
        int x2 = b.CenterX, y2 = b.CenterY;

        void HLine(int y, int xa, int xb)
        {
            int start = Mathf.Min(xa, xb);
            int end = Mathf.Max(xa, xb);
            for (int x = start; x <= end; x++) map[y, x] = floorChar; // ← PASILLO
        }
        void VLine(int x, int ya, int yb)
        {
            int start = Mathf.Min(ya, yb);
            int end = Mathf.Max(ya, yb);
            for (int y = start; y <= end; y++) map[y, x] = floorChar; // ← PASILLO
        }

        if (corridorLogic == CorridorLogic.RandomOrder)
        {
            if (Range(0, 1) == 0) { HLine(y1, x1, x2); VLine(x2, y1, y2); }
            else { VLine(x1, y1, y2); HLine(y2, x1, x2); }
        }
        else if (corridorLogic == CorridorLogic.HorizontalFirst)
        {
            HLine(y1, x1, x2); VLine(x2, y1, y2);
        }
        else if (corridorLogic == CorridorLogic.VerticalFirst)
        {
            VLine(x1, y1, y2); HLine(y2, x1, x2);
        }
        else
        {
            if (Mathf.Abs(x1 - x2) >= Mathf.Abs(y1 - y2)) HLine(y1, x1, x2);
            else VLine(x1, y1, y2);
        }
    }

    // ============= Player, Enemigos, Salida =============
    void PlacePlayer()
    {
        if (rooms.Count == 0) return;
        int cx = rooms[0].CenterX;
        int cy = rooms[0].CenterY;
        map[cy, cx] = playerChar;
    }

    void PlaceEnemies(int perRoom)
    {
        if (perRoom <= 0) return;

        for (int i = 1; i < rooms.Count; i++)
        {
            var r = rooms[i];
            int placed = 0;
            int tries = 0;
            int maxTries = perRoom * 10;

            while (placed < perRoom && tries < maxTries)
            {
                int ex = Range(r.x, r.x + r.w - 1);
                int ey = Range(r.y, r.y + r.h - 1);
                if (map[ey, ex] == roomChar) // ← sólo dentro de HABITACIÓN
                {
                    map[ey, ex] = enemyChar;
                    placed++;
                }
                tries++;
            }
        }
    }

    void PlaceExit()
    {
        if (rooms.Count == 0) return;
        int px = rooms[0].CenterX;
        int py = rooms[0].CenterY;

        int maxDist = -1;
        int idx = 0;
        for (int i = 1; i < rooms.Count; i++)
        {
            int cx = rooms[i].CenterX;
            int cy = rooms[i].CenterY;
            int dist = Mathf.Abs(cx - px) + Mathf.Abs(cy - py);
            if (dist > maxDist) { maxDist = dist; idx = i; }
        }

        var outRoom = rooms[idx];
        int sx = outRoom.CenterX;
        int sy = outRoom.CenterY;
        if (map[sy, sx] == roomChar) map[sy, sx] = exitChar;
        else
        {
            for (int y = outRoom.y; y < outRoom.y + outRoom.h; y++)
            {
                bool done = false;
                for (int x = outRoom.x; x < outRoom.x + outRoom.w; x++)
                {
                    if (map[y, x] == roomChar) { map[y, x] = exitChar; done = true; break; }
                }
                if (done) break;
            }
        }
    }

    // ============= Build Prefabs =============
    public void BuildPrefabs()
    {
        if (builtParent != null) Destroy(builtParent.gameObject);
        builtParent = new GameObject("BSP_Built").transform;
        builtParent.SetParent(transform, false);

        Vector3 origin = perlinRef != null ? perlinRef.transform.position : transform.position;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector3 pos = origin + new Vector3(x * cellSize, 0f, y * cellSize);
                char c = map[y, x];

                if (c == wallChar)
                {
                    if (wallPrefab != null)
                    {
                        float wy = WorldYForCell(y, x, false);
                        wy = ApplyPivotOffsetY(wallPrefab, wallPivot, wy, -wallExtraSink);
                        pos.y = wy;
                        Instantiate(wallPrefab, pos, Quaternion.identity, builtParent);
                    }
                }
                else if (c == roomChar) // HABITACIÓN
                {
                    var prefab = roomFloorPrefab != null ? roomFloorPrefab : floorPrefab;
                    if (prefab != null)
                    {
                        float fy = WorldYForCell(y, x, floorsUseTierFlatY);
                        fy = ApplyPivotOffsetY(prefab, floorPivot, fy, floorExtraLift);
                        pos.y = fy;
                        Instantiate(prefab, pos, Quaternion.identity, builtParent);
                    }
                }
                else if (c == floorChar) // PASILLO
                {
                    var prefab = corridorFloorPrefab != null ? corridorFloorPrefab : floorPrefab;
                    if (prefab != null)
                    {
                        float fy = WorldYForCell(y, x, floorsUseTierFlatY);
                        fy = ApplyPivotOffsetY(prefab, floorPivot, fy, floorExtraLift);
                        pos.y = fy;
                        Instantiate(prefab, pos, Quaternion.identity, builtParent);
                    }
                }
            }
        }

        // Marcadores especiales
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector3 pos = origin + new Vector3(x * cellSize, 0f, y * cellSize);
                char c = map[y, x];

                if (c == playerChar && playerMarkerPrefab != null)
                {
                    pos.y = WorldYForCell(y, x, true);
                    Instantiate(playerMarkerPrefab, pos + Vector3.up * 0.5f, Quaternion.identity, builtParent);
                }

                if (c == exitChar && exitMarkerPrefab != null)
                {
                    pos.y = WorldYForCell(y, x, true);
                    Instantiate(exitMarkerPrefab, pos + Vector3.up * 0.5f, Quaternion.identity, builtParent);
                }

                if (c == enemyChar && enemyPrefab != null)
                {
                    pos.y = WorldYForCell(y, x, true);
                    Instantiate(enemyPrefab, pos + Vector3.up * 0.5f, Quaternion.identity, builtParent);
                }
            }
        }

        BuildHouses(perlinRef != null ? perlinRef.transform.position : transform.position);
    }

    float WorldYForCell(int gy, int gx, bool flatForFloor = false)
    {
        if (perlinRef == null) return yOffsetOnTerrain;
        float h01 = perlinRef.Height01Raw(gy, gx);
        float y = flatForFloor
            ? perlinRef.GetTierFlatY(perlinRef.GetTierIndexFrom01(h01))
            : perlinRef.GetWorldYFrom01(h01);
        return y + yOffsetOnTerrain;
    }

    float ApplyPivotOffsetY(GameObject prefab, PrefabPivot pivot, float y, float extra)
    {
        if (prefab == null) return y;
        var rend = prefab.GetComponentInChildren<Renderer>();
        if (rend == null) return y + extra;

        float sizeY = rend.bounds.size.y;
        if (pivot == PrefabPivot.Center) return y + sizeY * 0.5f + extra; // apoya base
        return y + extra;
    }

    // ============= Utilidades =============
    int Range(int minInclusive, int maxInclusive)
    {
        if (maxInclusive < minInclusive) (minInclusive, maxInclusive) = (maxInclusive, minInclusive);
        EnsurePRNG();
        return prng.Next(minInclusive, maxInclusive + 1);
    }

    public char[,] GetMap() => map;
    public List<Room> GetRooms() => rooms;
}