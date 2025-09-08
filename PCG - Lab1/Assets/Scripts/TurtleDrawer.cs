using System.Collections.Generic;
using UnityEngine;

public class TurtleDrawer : MonoBehaviour
{
    [Header("Tortuga")]
    public float step = 0.2f;
    public float angle = 25f;
    public bool is3D = false;

    [Header("Render")]
    public bool useLineRenderer = true;
    public Material lineMaterial;
    public float lineWidth = 0.05f;
    public GameObject cylinderPrefab;
    public Transform drawParent;
    public bool worldSpaceLines = true;

    struct TurtleState { public Vector3 pos; public Quaternion rot; }

    void EnsureParent()
    {
        if (drawParent == null)
        {
            var go = new GameObject("LSystemDraw");
            go.transform.SetParent(transform, false);
            drawParent = go.transform;
        }
    }

    public void Clear()
    {
        EnsureParent();
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            for (int i = drawParent.childCount - 1; i >= 0; i--)
                DestroyImmediate(drawParent.GetChild(i).gameObject);
        }
        else
#endif
        {
            for (int i = drawParent.childCount - 1; i >= 0; i--)
                Destroy(drawParent.GetChild(i).gameObject);
        }
    }

    bool TryReadFloat(string s, ref int i, out float value)
    {
        int start = i + 1, j = start; bool sawDigit = false;
        if (j < s.Length && (s[j] == '+' || s[j] == '-')) j++;
        while (j < s.Length)
        {
            char ch = s[j];
            if ((ch >= '0' && ch <= '9') || ch == '.') { sawDigit = true; j++; }
            else break;
        }
        if (!sawDigit) { value = 1f; return false; }
        string token = s.Substring(start, j - start);
        if (float.TryParse(token, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out value))
        { i = j - 1; return true; }
        value = 1f; return false;
    }

    // ========= NUEVO: Dibuja con posición/rotación inicial explícita =========
    public void DrawAt(string sequence, Vector3 startPosition, Quaternion startRotation)
    {
        InternalDraw(sequence, startPosition, startRotation);
    }

    // Mantén compatibilidad con el controller actual
    public void Draw(string sequence) => InternalDraw(sequence, Vector3.zero, Quaternion.identity);

    void InternalDraw(string sequence, Vector3 startPos, Quaternion startRot)
    {
        EnsureParent();
        Clear();

        Vector3 pos = startPos;             // ? ahora arrancamos donde nos pidas
        Quaternion rot = startRot;
        var stack = new Stack<TurtleState>();

        var strokes = new List<List<Vector3>>();
        List<Vector3> currentStroke = null;
        bool penDown = false;

        void StartNewStroke()
        {
            currentStroke = new List<Vector3>();
            strokes.Add(currentStroke);
            currentStroke.Add(pos);
        }

        for (int i = 0; i < sequence.Length; i++)
        {
            char c = sequence[i];

            switch (c)
            {
                case 'F':
                    {
                        float k; TryReadFloat(sequence, ref i, out k);
                        if (useLineRenderer && !penDown) { StartNewStroke(); penDown = true; }
                        Vector3 newPos = pos + rot * Vector3.forward * (step * k);

                        if (useLineRenderer)
                        {
                            currentStroke.Add(newPos); // ? ya en mundo si worldSpaceLines=true
                        }
                        else if (cylinderPrefab != null)
                        {
                            var seg = Instantiate(cylinderPrefab, drawParent);
                            Vector3 dir = (newPos - pos);
                            float len = dir.magnitude;

                            if (worldSpaceLines)
                            {
                                seg.transform.position = pos + dir * 0.5f;
                                seg.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(90, 0, 0);
                            }
                            else
                            {
                                // interpretar pos como local al drawParent
                                Vector3 localPos = drawParent.InverseTransformPoint(pos);
                                Vector3 localNew = drawParent.InverseTransformPoint(newPos);
                                Vector3 localDir = (localNew - localPos);
                                seg.transform.localPosition = localPos + localDir * 0.5f;
                                seg.transform.localRotation = Quaternion.LookRotation(localDir.normalized, Vector3.up) * Quaternion.Euler(90, 0, 0);
                            }
                            seg.transform.localScale = new Vector3(lineWidth, len * 0.5f, lineWidth);
                        }

                        pos = newPos;
                        break;
                    }

                case 'f':
                    { float k; TryReadFloat(sequence, ref i, out k); pos += rot * Vector3.forward * (step * k); penDown = false; break; }

                case '+': { float k; TryReadFloat(sequence, ref i, out k); rot = rot * Quaternion.Euler(0f, angle * k, 0f); break; }
                case '-': { float k; TryReadFloat(sequence, ref i, out k); rot = rot * Quaternion.Euler(0f, -angle * k, 0f); break; }
                case '&': if (is3D) { float k; TryReadFloat(sequence, ref i, out k); rot = rot * Quaternion.Euler(angle * k, 0f, 0f); } break;
                case '^': if (is3D) { float k; TryReadFloat(sequence, ref i, out k); rot = rot * Quaternion.Euler(-angle * k, 0f, 0f); } break;
                case '\\': if (is3D) { float k; TryReadFloat(sequence, ref i, out k); rot = rot * Quaternion.Euler(0f, 0f, angle * k); } break;
                case '/': if (is3D) { float k; TryReadFloat(sequence, ref i, out k); rot = rot * Quaternion.Euler(0f, 0f, -angle * k); } break;
                case '|': rot = rot * Quaternion.Euler(0f, 180f, 0f); break;

                case '[': stack.Push(new TurtleState { pos = pos, rot = rot }); break;
                case ']':
                    if (stack.Count > 0)
                    {
                        var s = stack.Pop();
                        pos = s.pos; rot = s.rot; penDown = false;
                    }
                    break;

                default: break; // ignora X/Y u otros símbolos no geométricos
            }
        }

        if (useLineRenderer && strokes.Count > 0)
        {
            foreach (var poly in strokes)
            {
                if (poly == null || poly.Count < 2) continue;
                var go = new GameObject("Stroke");
                go.transform.SetParent(drawParent, false);

                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = poly.Count;
                lr.useWorldSpace = worldSpaceLines;
                lr.material = lineMaterial != null ? lineMaterial : new Material(Shader.Find("Sprites/Default"));
                lr.startWidth = lr.endWidth = lineWidth;
                lr.numCornerVertices = lr.numCapVertices = 4;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;

                if (worldSpaceLines) lr.SetPositions(poly.ToArray());
                else
                {
                    // convertir puntos mundo -> locales al drawParent
                    Vector3[] local = new Vector3[poly.Count];
                    for (int p = 0; p < poly.Count; p++)
                        local[p] = drawParent.InverseTransformPoint(poly[p]);
                    lr.SetPositions(local);
                }
            }
        }
    }
}