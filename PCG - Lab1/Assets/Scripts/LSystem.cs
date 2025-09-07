using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System;

[Serializable]
public class LRule
{
    public char symbol;
    [TextArea] public string[] productions; // permite varias para ser estocástico, nosesifunciona xd
}

public static class VillageMasksUtil
{
    public static bool IsOutsideRooms(Vector2Int cell, List<BSPDungeonGenerator.Room> rooms)
    {
        foreach (var r in rooms)
        {
            if (r == null) continue;
            if (cell.x >= r.y && cell.x < r.y + r.h &&
                cell.y >= r.x && cell.y < r.x + r.w)
                return false; // está dentro de una casa
        }
        return true;
    }
}

[CreateAssetMenu(fileName = "LSystemDef", menuName = "PCG/L-System Definition")]

public class LSystem : ScriptableObject
{
    [Header("Axioma")]
    public string axiom = "F";

    [Header("Reglas")]
    public List<LRule> rules = new();

    [Header("Semilla")]
    public int seed = 0;

    System.Random prng;
    Dictionary<char, List<string>> map;
    bool dirty = true; // indica que hay que reconstruir

    void OnEnable()
    {
        prng = new System.Random(seed);
        dirty = true; // cada vez que se habilita, marcamos sucio
    }

    public void BuildMap()
    {
        map = new Dictionary<char, List<string>>();
        foreach (var r in rules)
        {
            if (r == null) continue;
            if (!map.ContainsKey(r.symbol)) map[r.symbol] = new List<string>();
            if (r.productions == null) continue;
            foreach (var p in r.productions)
            {
                if (!string.IsNullOrWhiteSpace(p))
                    map[r.symbol].Add(p.Trim());
            }
        }
        dirty = false;
    }

    public string Generate(int iterations)
    {
        // reconstruye si está sucio o si map es null
        if (dirty || map == null) BuildMap();
        if (prng == null) prng = new System.Random(seed);

        string current = axiom ?? "F";
        for (int i = 0; i < iterations; i++)
        {
            var sb = new System.Text.StringBuilder(current.Length * 2);
            foreach (char c in current)
            {
                if (map.TryGetValue(c, out var list) && list.Count > 0)
                {
                    // determinista si hay 1 producción; estocástica si hay varias
                    string prod = (list.Count == 1) ? list[0] : list[prng.Next(list.Count)];
                    sb.Append(prod);
                }
                else
                {
                    sb.Append(c);
                }
            }
            current = sb.ToString();
        }
        return current;
    }
    public void MarkDirty() => dirty = true;
}