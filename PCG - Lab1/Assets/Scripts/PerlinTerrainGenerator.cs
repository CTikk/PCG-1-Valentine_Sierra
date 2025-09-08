using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class PerlinTerrainGenerator : MonoBehaviour
{
    [Header("Grid (celdas)")]
    public int width = 80;      // X
    public int depth = 80;      // Z
    [Range(0.1f, 500f)] public float noiseScale = 50f;

    [Header("Octavas")]
    [Range(1, 8)] public int octaves = 4;
    [Range(0.1f, 4f)] public float lacunarity = 2f;
    [Range(0.1f, 1f)] public float persistence = 0.5f;

    [Header("Semilla/Desplazamiento")]
    public int seed = 0;
    public bool randomizeSeedOnPlay = true;
    public Vector2 offset;

    [Header("Render")]
    public bool addMeshCollider = true;
    public bool autoUpdate = true;
    public bool useVertexColors = true;
    public Gradient heightGradient;

    // ===== Tiers / Terrazas =====
    public enum TierEdgeMode { HardStep, SmoothStep }

    [Header("Tiers (terrazas)")]
    [Range(1, 10)] public int tiers = 3;
    public float tierHeight = 4f;
    public TierEdgeMode edgeMode = TierEdgeMode.HardStep;
    [Range(0.01f, 0.40f)] public float smoothRange = 0.10f; // solo para SmoothStep

    [Header("Contexto (máscaras por tier)")]
    public List<int> waterTierIndices = new List<int> { 0 };            // agua = tier 0 por defecto
    public List<int> buildableTierIndices = new List<int> { 1, 2 };          // dónde se puede construir
    public List<int> landTierIndices = new List<int> { 1, 2 };          // caminable (≠ agua)

    // Componentes
    MeshFilter mf; MeshRenderer mr; MeshCollider mc; Mesh mesh;

    // Cache ruido
    float[,] noiseMap;
    float minH, maxH;

    void OnEnable()
    {
        EnsureComponents();
        if (randomizeSeedOnPlay && Application.isPlaying)
            seed = Random.Range(int.MinValue / 2, int.MaxValue / 2);
        Generate();
    }

    void OnValidate()
    {
        width = Mathf.Max(1, width);
        depth = Mathf.Max(1, depth);
        noiseScale = Mathf.Max(0.001f, noiseScale);
        lacunarity = Mathf.Max(0.01f, lacunarity);
        persistence = Mathf.Clamp01(persistence);
        tiers = Mathf.Max(1, tiers);
        tierHeight = Mathf.Max(0.001f, tierHeight);
        if (autoUpdate) Generate();
    }

    void EnsureComponents()
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

    // ===================== Público =====================
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

    // ===================== Ruido =======================
    void GenerateNoiseMap()
    {
        noiseMap = new float[depth + 1, width + 1];

        System.Random prng = new System.Random(seed);
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

    // ===================== API de alturas y tiers =======================
    public float Height01Raw(int z, int x)
    {
        z = Mathf.Clamp(z, 0, noiseMap.GetLength(0) - 1);
        x = Mathf.Clamp(x, 0, noiseMap.GetLength(1) - 1);
        return Mathf.InverseLerp(minH, maxH, noiseMap[z, x]);
    }

    public int GetTierIndexFrom01(float h01)
    {
        int idx = Mathf.FloorToInt(Mathf.Clamp01(h01) * tiers);
        if (idx >= tiers) idx = tiers - 1;
        return idx;
    }

    public float GetTierFlatY(int tierIndex)
    {
        tierIndex = Mathf.Clamp(tierIndex, 0, tiers - 1);
        return tierIndex * tierHeight;
    }

    public float GetWorldYFrom01(float h01)
    {
        if (edgeMode == TierEdgeMode.HardStep)
        {
            int ti = GetTierIndexFrom01(h01);
            return GetTierFlatY(ti);
        }

        // SmoothStep (opcional)
        float t = Mathf.Clamp01(h01) * tiers;
        int tierIndex = Mathf.FloorToInt(t);
        if (tierIndex >= tiers) tierIndex = tiers - 1;
        float frac = t - tierIndex;

        float baseY = GetTierFlatY(tierIndex);
        if (frac < smoothRange && tierIndex > 0)
        {
            float blend = frac / smoothRange;
            return Mathf.Lerp(GetTierFlatY(tierIndex - 1), baseY, blend);
        }
        if (frac > 1f - smoothRange && tierIndex < tiers - 1)
        {
            float blend = (frac - (1f - smoothRange)) / smoothRange;
            return Mathf.Lerp(baseY, GetTierFlatY(tierIndex + 1), blend);
        }
        return baseY;
    }

    public float GetWorldY(int z, int x) => GetWorldYFrom01(Height01Raw(z, x));

    // Máscaras por tier
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

    public bool[,] GetWaterMask() => GetMaskForTiers(waterTierIndices);
    public bool[,] GetBuildableMask() => GetMaskForTiers(buildableTierIndices);
    public bool[,] GetLandMask() => GetMaskForTiers(landTierIndices);

    // ===================== Mesh =======================
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
                float hNorm = Mathf.InverseLerp(minH, maxH, noiseMap[z, x]);
                float y = GetWorldYFrom01(hNorm);

                vertices[vi] = new Vector3(x, y, z);
                uvs[vi] = new Vector2(x / (float)width, z / (float)depth);

                if (useVertexColors)
                {
                    if (heightGradient != null)
                        colors[vi] = heightGradient.Evaluate(hNorm);
                    else
                        colors[vi] = Color.Lerp(Color.black, Color.white, hNorm);
                }

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
}