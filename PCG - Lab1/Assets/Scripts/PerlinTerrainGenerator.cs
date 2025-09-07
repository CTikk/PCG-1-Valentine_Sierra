using System.Collections.Generic;
using System.Collections;
using UnityEngine;

[ExecuteAlways]
public class PerlinTerrainGenerator : MonoBehaviour
{
    [Header("Terrazas")]
    [Range(1, 10)] public int tiers = 3;          // cuántos niveles
    public float tierHeight = 4f;                 // altura entre niveles
    public bool smoothEdges = true;               // suaviza borde del escalón
    [Range(0.01f, 0.4f)] public float smoothRange = 0.10f; // ancho de suavizado


    // ===== Config de máscaras por contexto =====
    // define qué tiers son agua / construibles / tierra
    [Header("Contexto (máscaras por tier)")]
    public List<int> waterTierIndices = new List<int> { 0 };
    public List<int> buildableTierIndices = new List<int> { 1, 2 };
    public List<int> landTierIndices = new List<int> { 1, 2, 3 };


    [Header("Grid (celdas)")]
    public int width = 200;     // X
    public int depth = 200;     // Z
    [Range(0.1f, 500f)] public float noiseScale = 50f;

    [Header("Altura")]
    [Range(0, 100)] public float heightMultiplier = 20f;

    [Header("Octavas")]
    [Range(1, 8)] public int octaves = 4;
    [Range(0.1f, 4f)] public float lacunarity = 2f;   // factor de frecuencia
    [Range(0.1f, 1f)] public float persistence = 0.5f; // factor de amplitud

    [Header("Semilla/Desplazamiento")]
    public int seed = 0;
    public bool randomizeSeedOnPlay = true;
    public Vector2 offset; // desplaza el patrón

    [Header("Colores")]
    public Gradient heightGradient; // define colores por altura (No funciona)
    public bool useVertexColors = true;

    [Header("Componentes")]
    public bool addMeshCollider = true;
    public bool autoUpdate = true;

    // 0 = sin meseta, 1 = a un mismo nivel, 2 = reducir amplitud
    public enum PlateauMode { None, ClampToLevel, ReduceMultiplier }

    [Header("Mesetas / Llanuras visuales")]
    public PlateauMode plateauMode = PlateauMode.ClampToLevel;

    // Rango de alturas normalizadas [0..1] donde quieres la llanura (debe
    // corresponderse visualmente con donde colocas casas/caminos).
    [Range(0f, 1f)] public float plateauMin = 0.35f;
    [Range(0f, 1f)] public float plateauMax = 0.55f;

    // Si ClampToLevel: el nivel plano final (0..1)
    [Range(0f, 1f)] public float plateauLevel01 = 0.44f;

    // Si ReduceMultiplier: cuánta “rugosidad” queda (0 = totalmente plano, 1 = igual que fuera del rango)
    [Range(0f, 1f)] public float plateauMultFactor = 0.25f;

    // (Opcional) cuantización/terrazas para dar “steps” (0 = off)
    [Range(0, 12)] public int terraceSteps = 0;

    // (Opcional) curva global de altura para afinar perfil (default lineal)
    public AnimationCurve heightCurve = AnimationCurve.Linear(0, 0, 1, 1);

    MeshFilter mf;
    MeshRenderer mr;
    MeshCollider mc;
    Mesh mesh;

    float[,] noiseMap;
    float minH, maxH;

    void OnEnable()
    {
        EnsureComponents();
        if (randomizeSeedOnPlay && Application.isPlaying)
        {
            seed = Random.Range(int.MinValue / 2, int.MaxValue / 2);
        }
        Generate();
    }

    void OnValidate()
    {
        width = Mathf.Max(1, width);
        depth = Mathf.Max(1, depth);
        noiseScale = Mathf.Max(0.001f, noiseScale);
        lacunarity = Mathf.Max(0.01f, lacunarity);
        persistence = Mathf.Clamp01(persistence);
        if (autoUpdate) Generate(); // generar mapa nuevo en cada ejecución
    }

    void EnsureComponents() // Asegurarse de que los componentes existan :)
    {
        if (!mf) mf = GetComponent<MeshFilter>();
        if (!mr) mr = GetComponent<MeshRenderer>();
        if (!mf) mf = gameObject.AddComponent<MeshFilter>();
        if (!mr) mr = gameObject.AddComponent<MeshRenderer>();
        if (addMeshCollider)
        {
            if (!mc) mc = GetComponent<MeshCollider>();
            if (!mc) mc = gameObject.AddComponent<MeshCollider>();
        }
    }

    public void Generate()
    {
        EnsureComponents();
        GenerateNoiseMap();
        BuildMesh();
    }

    public void RandomizeSeed()
    {
        seed = Random.Range(int.MinValue / 2, int.MaxValue / 2);
        Generate();
    }
    void GenerateNoiseMap()
    {
        noiseMap = new float[depth + 1, width + 1];
        System.Random prng = new System.Random(seed); // Randoms por seed
        Vector2[] octaveOffsets = new Vector2[octaves];
        for (int i = 0; i < octaves; i++)
        {
            float offX = prng.Next(-100000, 100000) + offset.x;
            float offY = prng.Next(-100000, 100000) + offset.y;
            octaveOffsets[i] = new Vector2(offX, offY);
        }

        minH = float.MaxValue;
        maxH = float.MinValue;

        for (int z = 0; z <= depth; z++)
        {
            for (int x = 0; x <= width; x++)
            {
                float amplitude = 1f;
                float frequency = 1f;
                float noiseHeight = 0f;

                for (int o = 0; o < octaves; o++)
                {
                    float sampleX = (x + octaveOffsets[o].x) / noiseScale * frequency;
                    float sampleZ = (z + octaveOffsets[o].y) / noiseScale * frequency;
                    float perlin = Mathf.PerlinNoise(sampleX, sampleZ) * 2f - 1f; // [-1,1]
                    noiseHeight += perlin * amplitude;

                    amplitude *= persistence;
                    frequency *= lacunarity;
                }

                minH = Mathf.Min(minH, noiseHeight);
                maxH = Mathf.Max(maxH, noiseHeight);
                noiseMap[z, x] = noiseHeight;
            }
        }
    }

    void BuildMesh()
    {
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "PerlinTerrainMesh";
            mf.sharedMesh = mesh;
        }
        mesh.Clear();

        int vCountX = width + 1;
        int vCountZ = depth + 1;
        int vCount = vCountX * vCountZ;

        var vertices = new Vector3[vCount];
        var uvs = new Vector2[vCount];
        var colors = new Color[vCount];

        int tiCount = width * depth * 6;
        var triangles = new int[tiCount];

        int vi = 0;
        for (int z = 0; z < vCountZ; z++)
        {
            for (int x = 0; x < vCountX; x++)
            {
                /*float hNorm = Mathf.InverseLerp(minH, maxH, noiseMap[z, x]); // [0,1]
                // Aplica meseta y curva global
                float hShaped = ApplyPlateau(hNorm);
                float y = heightCurve.Evaluate(hShaped) * heightMultiplier;*/

                float hNorm = Mathf.InverseLerp(minH, maxH, noiseMap[z, x]); // [0,1]
                float y = GetWorldYFrom01(hNorm);


                vertices[vi] = new Vector3(x, y, z);
                uvs[vi] = new Vector2(x / (float)width, z / (float)depth);

                colors[vi] = heightGradient != null
                    ? heightGradient.Evaluate(hNorm)
                    : Color.Lerp(Color.black, Color.white, hNorm);

                vi++;
            }
        }

        int ti = 0;
        int vx = vCountX;
        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int a = z * vx + x;
                int b = a + vx;
                int c = b + 1;
                int d = a + 1;

                triangles[ti++] = a; triangles[ti++] = b; triangles[ti++] = c;
                triangles[ti++] = a; triangles[ti++] = c; triangles[ti++] = d;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        if (useVertexColors) mesh.colors = colors;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        if (mc) mc.sharedMesh = mesh;
    }

    public float[,] GetHeight01Map()
    {
        // Asegura que noiseMap esté generado
        if (noiseMap == null) GenerateNoiseMap();

        int h = noiseMap.GetLength(0);
        int w = noiseMap.GetLength(1);
        var map01 = new float[h, w];

        for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                // normalizamos con los min/max ya calculados
                float hNorm = Mathf.InverseLerp(minH, maxH, noiseMap[z, x]); // [0,1]
                map01[z, x] = hNorm;
            }

        return map01;
    }

    /// <summary>
    /// Construye máscaras booleanas para agua/llanura/tierra.
    /// Devuelve las tres del mismo tamaño del heightMap del Perlin.
    /// </summary>
    public void BuildMasks(
        float waterThreshold,               // ej: 0.30f -> bajo = agua
        Vector2 plainBand,                  // ej: (0.35, 0.55) -> llanuras por altura
        float maxSlopeForPlain,             // ej: 0.10 (pendiente baja)
        out bool[,] waterMask,
        out bool[,] plainMask,
        out bool[,] landMask)
    {
        var h01 = GetHeight01Map();
        int h = h01.GetLength(0);
        int w = h01.GetLength(1);

        waterMask = new bool[h, w];
        plainMask = new bool[h, w];
        landMask = new bool[h, w];

        // Aproximar "pendiente" como delta de altura con vecinos 4-conexión:
        float GetLocalSlope(int z, int x)
        {
            float c = h01[z, x];
            float acc = 0f; int cnt = 0;

            void Acc(int zz, int xx)
            {
                float d = Mathf.Abs(h01[zz, xx] - c);
                acc += d; cnt++;
            }

            if (z > 0) Acc(z - 1, x);
            if (z < h - 1) Acc(z + 1, x);
            if (x > 0) Acc(z, x - 1);
            if (x < w - 1) Acc(z, x + 1);

            return (cnt > 0) ? acc / cnt : 0f;
        }

        for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                float hv = h01[z, x];
                bool isWater = hv < waterThreshold;
                waterMask[z, x] = isWater;
                landMask[z, x] = !isWater;

                if (!isWater)
                {
                    float slope = GetLocalSlope(z, x);
                    bool inBand = hv >= plainBand.x && hv <= plainBand.y;
                    plainMask[z, x] = inBand && (slope <= maxSlopeForPlain);
                }
                else plainMask[z, x] = false;
            }
    }

    float ApplyPlateau(float h01Raw)
    {
        float h01 = h01Raw;

        // Terrazas discretas (después de meseta; puedes moverlo si prefieres)
        float Terrace(float v)
        {
            if (terraceSteps <= 1) return v;
            float k = terraceSteps;
            return Mathf.Floor(v * k) / k;
        }

        if (plateauMode == PlateauMode.None)
            return Terrace(h01);

        bool inRange = h01 >= plateauMin && h01 <= plateauMax;

        if (inRange)
        {
            if (plateauMode == PlateauMode.ClampToLevel)
            {
                h01 = plateauLevel01;               // totalmente plano
            }
            else // ReduceMultiplier
            {
                // Empuja el valor hacia el nivel central y reduce “relieve”
                h01 = Mathf.Lerp(h01, plateauLevel01, 1f - plateauMultFactor);
            }
        }

        return Terrace(h01);
    }

    // Normalizado [0..1] desde el noise ya calculado
    public float Height01Raw(int z, int x)
    {
        z = Mathf.Clamp(z, 0, noiseMap.GetLength(0) - 1);
        x = Mathf.Clamp(x, 0, noiseMap.GetLength(1) - 1);
        return Mathf.InverseLerp(minH, maxH, noiseMap[z, x]);
    }

    // Devuelve el índice de tier (0..tiers-1) para un valor normalizado [0..1]
    public int GetTierIndexFrom01(float h01)
    {
        int idx = Mathf.FloorToInt(Mathf.Clamp01(h01) * tiers);
        if (idx >= tiers) idx = tiers - 1;
        return idx;
    }

    // Devuelve una Y “terraceada” (con suavizado) a partir de h01
    public float GetWorldYFrom01(float h01)
    {
        float t = Mathf.Clamp01(h01) * tiers;
        int tierIndex = Mathf.FloorToInt(t);
        if (tierIndex >= tiers) tierIndex = tiers - 1;
        float frac = t - tierIndex;

        float baseY = tierIndex * tierHeight;

        float y;
        if (smoothEdges && frac < smoothRange && tierIndex > 0)
        {
            // suavizar con el tier anterior
            float blend = frac / smoothRange;
            y = Mathf.Lerp((tierIndex - 1) * tierHeight, baseY, blend);
        }
        else if (smoothEdges && frac > 1f - smoothRange && tierIndex < tiers - 1)
        {
            // suavizar con el siguiente tier
            float blend = (frac - (1f - smoothRange)) / smoothRange;
            y = Mathf.Lerp(baseY, (tierIndex + 1) * tierHeight, blend);
        }
        else
        {
            y = baseY; // flat en el tier
        }

        // Curva global de perfil (opcional): actúa como multiplicador sobre la altura máxima posible
        float shaped = heightCurve != null ? heightCurve.Evaluate(h01) : h01;
        return y * Mathf.Max(0.0001f, shaped); // mantiene proporción global si quieres
    }

    // Conveniencia para muestrear con coordenadas de celda
    public float GetWorldY(int z, int x)
    {
        return GetWorldYFrom01(Height01Raw(z, x));
    }

    // Si quieres la altura del "plano" exacto de un tier:
    public float GetTierFlatY(int tierIndex)
    {
        tierIndex = Mathf.Clamp(tierIndex, 0, tiers - 1);
        return tierIndex * tierHeight;
    }

    // ============== UUUUUUUUUUUUUUUUUUUUUUUUUUUUUGHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH

    public bool[,] GetMaskForTiers(List<int> allowedTiers)
    {
        int H = noiseMap.GetLength(0);
        int W = noiseMap.GetLength(1);
        var mask = new bool[H, W];

        var allow = new HashSet<int>(allowedTiers ?? new List<int>());

        for (int z = 0; z < H; z++)
            for (int x = 0; x < W; x++)
            {
                float h01 = Height01Raw(z, x);
                int ti = GetTierIndexFrom01(h01);
                mask[z, x] = allow.Contains(ti);
            }
        return mask;
    }

    // Atajos por contexto actual
    public bool[,] GetWaterMask() => GetMaskForTiers(waterTierIndices);
    public bool[,] GetBuildableMask() => GetMaskForTiers(buildableTierIndices);
    public bool[,] GetLandMask() => GetMaskForTiers(landTierIndices);
}