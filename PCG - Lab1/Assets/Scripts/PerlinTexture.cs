using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public static class PerlinTexture
{
    static float noise(float x, float y)
    {
        var X = Mathf.FloorToInt(x) & 0xff;
        var Y = Mathf.FloorToInt(y) & 0xff;

        x-= Mathf.FloorToInt(x);
        y-= Mathf.FloorToInt(y);

        var u = fade(x);
        var v = fade(y);

        var A = (perm[X] + Y) & 0xff;
        var B = (perm[X + 1] + Y) & 0xff;
        return lerp(v, lerp(u, Grad(perm[A], x, y), Grad(perm[B], x - 1, y)),
                       lerp(u, Grad(perm[A + 1], x, y - 1), Grad(perm[B + 1], x - 1, y - 1)));

    }

    static float fade(float t)
    {
        return ((6 * t - 15) * t + 10) * t * t * t;
    }
    static float lerp(float t, float a1, float a2)
    {
        return a1 + t * (a2 - a1);
    }

    static float Grad(int hash, float x, float y)
    {
        return ((hash & 1) == 0 ? x : -x) + ((hash & 2) == 0 ? y : -y);
    }

    static int[] generatePerm()
    {
        int seed = Random.Range(int.MinValue / 2, int.MaxValue / 2);
        System.Random prng = new System.Random(seed);
        int[] permTable = new int[256];

        for(int i = 0; i < 256; i++)
        {
            permTable[i] = prng.Next(-100000, 100000);
        }
        return permTable;
    }

    static int[] perm = generatePerm();
}
