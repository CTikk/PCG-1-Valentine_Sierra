using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BSPDungeonGenerator : MonoBehaviour
{
    [Header("Integración con terreno (tiers)")]
    public PerlinTerrainGenerator perlinRef; // arrástralo desde la escena
    public float yOffsetOnTerrain = 0.02f;   // para evitar z-fighting
    public bool floorsUseTierFlatY = true;   // pisos totalmente planos por tier

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

    [Header("Visualización")]
    public bool drawGizmos = true;
    public bool buildPrefabsOnPlay = true;
    public float cellSize = 1f;
    public GameObject floorPrefab;
    public GameObject wallPrefab;
    public GameObject playerMarkerPrefab;
    public GameObject exitMarkerPrefab;
    public GameObject enemyPrefab;
    public int enemiesPerRoom = 5;

    [Header("Máscara externa (opcional)")]
    public bool[,] buildableMask;   // si es null, se ignora
    public bool requirePlainsForRooms = true; // si true, exige mask=true para TODA la sala

    [Header("Símbolos del mapa")]
    public char floorChar = '.';
    public char wallChar = '#';
    public char enemyChar = 'E';
    public char playerChar = 'P';
    public char exitChar = 'S';

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
        {
            this.x = x; this.y = y; this.w = w; this.h = h;
        }
    }

    public enum CorridorLogic
    {
        RandomOrder = 0,
        HorizontalFirst = 1,
        VerticalFirst = 2,
        StraightAxis = 3 // escoge eje de mayor distancia
    }

    // ============= Ciclo de vida =============
    void Start()
    {
        if (useRandomSeed) seed = UnityEngine.Random.Range(int.MinValue / 2, int.MaxValue / 2);
        prng = new System.Random(seed);
        Generate();
        if (buildPrefabsOnPlay) BuildPrefabs();
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos || map == null) return;

        // Dibuja grid (piso/muro) + contornos de rooms y pasillos
        Vector3 origin = transform.position;
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
                    Gizmos.color = new Color(0.7f, 0.7f, 0.7f);
                    Gizmos.DrawCube(pos + Vector3.up * 0.05f, new Vector3(cellSize, 0.1f, cellSize));
                }
            }
        }

        // Contornos de rooms
        Gizmos.color = Color.cyan;
        if (rooms != null)
        {
            foreach (var r in rooms)
            {
                Vector3 p = origin + new Vector3(r.x * cellSize, 0f, r.y * cellSize);
                Vector3 size = new Vector3(r.w * cellSize, 0.03f, r.h * cellSize);
                Gizmos.DrawWireCube(p + new Vector3(size.x * 0.5f, 0.2f, size.z * 0.5f), size);
            }
        }
    }

    // ============= Generación principal =============
    public void Generate()
    {
        EnsurePRNG();

        // reset
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

        // Decide eje preferente según relación ancho/alto (como en tu C++)
        bool splitH = (leaf.w <= leaf.h); // si es más alto, corta horizontal, si es más ancho, corta vertical
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

        // Hoja terminal => crear sala si cabe
        int maxW = Mathf.Min(maxRoomSize, leaf.w - 2);
        int maxH = Mathf.Min(maxRoomSize, leaf.h - 2);
        if (minRoomSize > maxW || minRoomSize > maxH) return;

        int rw = Mathf.Min(Range(minRoomSize, maxW), leaf.w - 2);
        int rh = Mathf.Min(Range(minRoomSize, maxH), leaf.h - 2);

        int rx = Range(leaf.x + 1, leaf.x + leaf.w - rw - 1);
        int ry = Range(leaf.y + 1, leaf.y + leaf.h - rh - 1);

        leaf.room = new Room { x = rx, y = ry, w = rw, h = rh };

        // Si exige llanura (o al menos tierra), validar contra buildableMask:
        if (requirePlainsForRooms && !RoomFitsMask(leaf.room))
        {
            // Intentar algunos reintentos para ubicar otra posición dentro de la hoja
            bool placed = false;
            for (int t = 0; t < 20 && !placed; t++)
            {
                rx = Range(leaf.x + 1, Mathf.Max(leaf.x + 1, leaf.x + leaf.w - rw - 1));
                ry = Range(leaf.y + 1, Mathf.Max(leaf.y + 1, leaf.y + leaf.h - rh - 1));

                var candidate = new Room { x = rx, y = ry, w = rw, h = rh };
                if (RoomFitsMask(candidate))
                {
                    leaf.room = candidate;
                    placed = true;
                }
            }

            if (!placed) leaf.room = null; // esta hoja quedará sin sala
        }
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
                    map[y, x] = floorChar;
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
        // DFS hasta encontrar una room
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

        // Si tenemos máscara, intenta BFS para evitar agua:
        if (buildableMask != null)
        {
            var path = FindPath(new Vector2Int(y1, x1), new Vector2Int(y2, x2));
            if (path != null && path.Count > 0)
            {
                foreach (var p in path)
                {
                    map[p.x, p.y] = floorChar; // ojo: p es (y,x)
                }
                return;
            }
            // Si no encontró, cae al conector "L" original (podría cruzar agua)
        }

        // --- Conector original (L) ---
        void HLine(int y, int xa, int xb)
        {
            int start = Mathf.Min(xa, xb);
            int end = Mathf.Max(xa, xb);
            for (int x = start; x <= end; x++) map[y, x] = floorChar;
        }
        void VLine(int x, int ya, int yb)
        {
            int start = Mathf.Min(ya, yb);
            int end = Mathf.Max(ya, yb);
            for (int y = start; y <= end; y++) map[y, x] = floorChar;
        }

        if (corridorLogic == CorridorLogic.RandomOrder)
        {
            if (Range(0, 1) == 0) { HLine(y1, x1, x2); VLine(x2, y1, y2); }
            else { VLine(x1, y1, y2); HLine(y2, x1, x2); }
        }
        else if (corridorLogic == CorridorLogic.HorizontalFirst)
        {
            HLine(y1, x1, x2);
            VLine(x2, y1, y2);
        }
        else if (corridorLogic == CorridorLogic.VerticalFirst)
        {
            VLine(x1, y1, y2);
            HLine(y2, x1, x2);
        }
        else // StraightAxis
        {
            if (Mathf.Abs(x1 - x2) >= Mathf.Abs(y1 - y2)) HLine(y1, x1, x2);
            else VLine(x1, y1, y2);
        }
    }

    List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
    {
        if (buildableMask == null)
        {
            // Sin máscara = deja que el conector original haga su L
            return null;
        }

        int H = buildableMask.GetLength(0);
        int W = buildableMask.GetLength(1);

        bool InB(int y, int x) => y >= 0 && y < H && x >= 0 && x < W;

        var q = new Queue<Vector2Int>();
        var prev = new Dictionary<Vector2Int, Vector2Int>();
        var seen = new HashSet<Vector2Int>();

        q.Enqueue(start);
        seen.Add(start);

        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            if (cur == goal) break;

            foreach (var d in dirs)
            {
                var nx = cur + d;
                if (!InB(nx.y, nx.x)) continue;
                if (!buildableMask[nx.y, nx.x]) continue; // evita agua/no construible

                if (seen.Add(nx))
                {
                    prev[nx] = cur;
                    q.Enqueue(nx);
                }
            }
        }

        if (!prev.ContainsKey(goal) && goal != start) return null; // no hubo camino
                                                                   // reconstruir
        var path = new List<Vector2Int>();
        var p = goal;
        path.Add(p);
        while (p != start && prev.TryGetValue(p, out var pp))
        {
            p = pp; path.Add(p);
        }
        path.Reverse();
        return path;
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
                if (map[ey, ex] == floorChar)
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
        if (map[sy, sx] == floorChar) map[sy, sx] = exitChar;
        else
        {
            // busca primer piso libre
            for (int y = outRoom.y; y < outRoom.y + outRoom.h; y++)
            {
                bool done = false;
                for (int x = outRoom.x; x < outRoom.x + outRoom.w; x++)
                {
                    if (map[y, x] == floorChar) { map[y, x] = exitChar; done = true; break; }
                }
                if (done) break;
            }
        }
    }

    // ============= Build Prefabs =============
    public void BuildPrefabs()
    {
        // Limpia anterior
        if (builtParent != null) Destroy(builtParent.gameObject);
        builtParent = new GameObject("BSP_Built").transform;
        builtParent.SetParent(transform, false);

        Vector3 origin = transform.position;

        // Piso / Muro
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
                        pos.y = WorldYForCell(y, x, false); // pared sigue ondulación/mezcla del tier
                        Instantiate(wallPrefab, pos, Quaternion.identity, builtParent);
                    }
                }
                else
                {
                    if (floorPrefab != null)
                    {
                        pos.y = WorldYForCell(y, x, floorsUseTierFlatY); // piso plano exacto del tier
                        Instantiate(floorPrefab, pos, Quaternion.identity, builtParent);
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
                    Instantiate(playerMarkerPrefab, pos + Vector3.up * 0.5f, Quaternion.identity, builtParent);

                if (c == exitChar && exitMarkerPrefab != null)
                    Instantiate(exitMarkerPrefab, pos + Vector3.up * 0.5f, Quaternion.identity, builtParent);

                if (c == enemyChar && enemyPrefab != null)
                    Instantiate(enemyPrefab, pos + Vector3.up * 0.5f, Quaternion.identity, builtParent);
            }
        }
    }

    // ============= Utilidades =============
    void EnsurePRNG()
    {
        // Si quieres random por play, respeta useRandomSeed cada vez que generes
        if (useRandomSeed)
            seed = UnityEngine.Random.Range(int.MinValue / 2, int.MaxValue / 2);

        if (prng == null)
            prng = new System.Random(seed);
    }
    bool IsInsideMask(int y, int x)
    {
        return (buildableMask != null
            && y >= 0 && y < buildableMask.GetLength(0)
            && x >= 0 && x < buildableMask.GetLength(1));
    }

    bool IsBuildableCell(int y, int x)
    {
        if (buildableMask == null) return true;
        if (!IsInsideMask(y, x)) return false;
        return buildableMask[y, x];
    }

    bool RoomFitsMask(Room r)
    {
        if (buildableMask == null) return true;
        for (int y = r.y; y < r.y + r.h; y++)
            for (int x = r.x; x < r.x + r.w; x++)
                if (!IsBuildableCell(y, x)) return false;
        return true;
    }

    int Range(int minInclusive, int maxInclusive)
    {
        if (maxInclusive < minInclusive) (minInclusive, maxInclusive) = (maxInclusive, minInclusive);
        return prng.Next(minInclusive, maxInclusive + 1);
    }

    float SamplePerlinY(int gridY, int gridX)
    {
        if (perlinRef == null) return 0f;
        var map01 = perlinRef.GetHeight01Map();

        // Clampear por seguridad (recuerda que tu map es [y,x])
        gridY = Mathf.Clamp(gridY, 0, map01.GetLength(0) - 1);
        gridX = Mathf.Clamp(gridX, 0, map01.GetLength(1) - 1);

        float h01 = map01[gridY, gridX];
        float y = perlinRef.heightCurve.Evaluate(h01) * perlinRef.heightMultiplier;
        return y + yOffsetOnTerrain;
    }

    float WorldYForCell(int gy, int gx, bool flatForFloor = false)
    {
        if (perlinRef == null) return yOffsetOnTerrain;

        float h01 = perlinRef.Height01Raw(gy, gx);
        if (flatForFloor)
        {
            int ti = perlinRef.GetTierIndexFrom01(h01);
            return perlinRef.GetTierFlatY(ti) + yOffsetOnTerrain;
        }
        else
        {
            return perlinRef.GetWorldYFrom01(h01) + yOffsetOnTerrain;
        }
    }

    // Exponer mapa si se necesita post-procesar :)
    public char[,] GetMap() => map;
    public List<Room> GetRooms() => rooms;
}
