using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PerlinUI : MonoBehaviour
{
    public PerlinTerrainGenerator gen;
    public Slider sNoiseScale, sHeight, sOctaves, sLacunarity, sPersistence;
    public Text tNoiseScale, tHeight, tOctaves, tLacunarity, tPersistence;

    void Start() { SyncFromGen(); }

    public void OnNoiseScale(float v) { gen.noiseScale = v; tNoiseScale.text = v.ToString("F1"); gen.Generate(); }
    // public void OnHeight(float v) { gen.heightMultiplier = v; tHeight.text = v.ToString("F1"); gen.Generate(); }
    public void OnOctaves(float v) { gen.octaves = Mathf.RoundToInt(v); tOctaves.text = gen.octaves.ToString(); gen.Generate(); }
    public void OnLacunarity(float v) { gen.lacunarity = v; tLacunarity.text = v.ToString("F2"); gen.Generate(); }
    public void OnPersistence(float v) { gen.persistence = v; tPersistence.text = v.ToString("F2"); gen.Generate(); }

    public void RandomizeSeed() { gen.RandomizeSeed(); }

    void SyncFromGen()
    {
        if (!gen) return;
        sNoiseScale.value = gen.noiseScale; tNoiseScale.text = gen.noiseScale.ToString("F1");
        //sHeight.value = gen.heightMultiplier; tHeight.text = gen.heightMultiplier.ToString("F1");
        sOctaves.value = gen.octaves; tOctaves.text = gen.octaves.ToString();
        sLacunarity.value = gen.lacunarity; tLacunarity.text = gen.lacunarity.ToString("F2");
        sPersistence.value = gen.persistence; tPersistence.text = gen.persistence.ToString("F2");
    }
}