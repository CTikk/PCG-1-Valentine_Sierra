using System.Collections.Generic;
using UnityEngine;

public class TurtleDrawer : MonoBehaviour
{
    [Header("Tortuga")]
    [Tooltip("Longitud base del avance")]
    public float step = 0.2f;

    [Tooltip("Ángulo base en grados para rotaciones")]
    public float angle = 25f;

    [Tooltip("Habilita rotaciones 3D (&,^,\\,/,|). Si está OFF, solo + y - (yaw)")]
    public bool is3D = false;

    [Header("Render")]
    [Tooltip("Si ON usa LineRenderer; si OFF usa cilindros por segmento")]
    public bool useLineRenderer = true;

    [Tooltip("Material del LineRenderer (p.ej. Sprites/Default)")]
    public Material lineMaterial;

    [Tooltip("Grosor de línea o radio del cilindro")]
    public float lineWidth = 0.05f;

    [Tooltip("Prefab de cilindro (eje Y) para modo sin LineRenderer")]
    public GameObject cylinderPrefab;

    [Tooltip("Padre de todos los strokes/segmentos generados")]
    public Transform drawParent;

    [Tooltip("Dibujar en coordenadas de mundo (recomendado TRUE)")]
    public bool worldSpaceLines = true;

    struct TurtleState
    {
        public Vector3 pos;
        public Quaternion rot;
    }

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

    // Lee un número opcional pegado al símbolo (ej: F2.5, +30, -45). Si no hay número, devuelve false y value=1.
    bool TryReadFloat(string s, ref int i, out float value)
    {
        int start = i + 1;
        int j = start;
        bool sawDigit = false;

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
        {
            i = j - 1; // avanzamos el índice del for principal
            return true;
        }
        value = 1f;
        return false;
    }

    public void Draw(string sequence)
    {
        EnsureParent();
        Clear();

        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;
        var stack = new Stack<TurtleState>();

        // Múltiples strokes (un LineRenderer por trazo)
        var strokes = new List<List<Vector3>>();
        List<Vector3> currentStroke = null;

        bool penDown = false; // “lápiz” levantado hasta que llegue un F

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
                case 'F': // avanzar y dibujar (con multiplicador opcional)
                    {
                        float k; TryReadFloat(sequence, ref i, out k);
                        if (useLineRenderer && !penDown) { StartNewStroke(); penDown = true; }

                        Vector3 newPos = pos + rot * Vector3.forward * (step * k);

                        if (useLineRenderer)
                        {
                            currentStroke.Add(newPos);
                        }
                        else if (cylinderPrefab != null)
                        {
                            var seg = Instantiate(cylinderPrefab, drawParent);
                            Vector3 dir = (newPos - pos);
                            float len = dir.magnitude;

                            seg.transform.position = worldSpaceLines
                                ? drawParent.TransformPoint(pos + dir * 0.5f)
                                : pos + dir * 0.5f;

                            seg.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(90, 0, 0);
                            seg.transform.localScale = new Vector3(lineWidth, len * 0.5f, lineWidth);
                        }

                        pos = newPos;
                        break;
                    }

                case 'f': // avanzar sin dibujar (con multiplicador)
                    {
                        float k; TryReadFloat(sequence, ref i, out k);
                        pos += rot * Vector3.forward * (step * k);
                        penDown = false; // próximo F inicia un stroke nuevo
                        break;
                    }

                // Rotaciones 2D (+/-) y 3D (&,^,\,/)
                case '+': { float k; TryReadFloat(sequence, ref i, out k); rot = rot * Quaternion.Euler(0f, angle * k, 0f); break; }
                case '-': { float k; TryReadFloat(sequence, ref i, out k); rot = rot * Quaternion.Euler(0f, -angle * k, 0f); break; }
                case '&': if (is3D) { float k; TryReadFloat(sequence, ref i, out k); rot = rot * Quaternion.Euler(angle * k, 0f, 0f); } break;
                case '^': if (is3D) { float k; TryReadFloat(sequence, ref i, out k); rot = rot * Quaternion.Euler(-angle * k, 0f, 0f); } break;
                case '\\': if (is3D) { float k; TryReadFloat(sequence, ref i, out k); rot = rot * Quaternion.Euler(0f, 0f, angle * k); } break;
                case '/': if (is3D) { float k; TryReadFloat(sequence, ref i, out k); rot = rot * Quaternion.Euler(0f, 0f, -angle * k); } break;
                case '|': rot = rot * Quaternion.Euler(0f, 180f, 0f); break;

                case '[': // push estado
                    stack.Push(new TurtleState { pos = pos, rot = rot });
                    break;

                case ']': // pop estado + levantar lápiz para cortar stroke
                    if (stack.Count > 0)
                    {
                        var s = stack.Pop();
                        pos = s.pos;
                        rot = s.rot;
                        penDown = false;
                    }
                    break;

                default:
                    // ignora símbolos no geométricos (X,Y, etc.)
                    break;
            }
        }

        // Render de strokes (LineRenderers)
        if (useLineRenderer && strokes.Count > 0)
        {
            foreach (var poly in strokes)
            {
                if (poly == null || poly.Count < 2) continue;

                Vector3[] pts = poly.ToArray();
                if (worldSpaceLines)
                {
                    for (int p = 0; p < pts.Length; p++)
                        pts[p] = drawParent.TransformPoint(pts[p]);
                }

                var go = new GameObject("Stroke");
                go.transform.SetParent(drawParent, false);

                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = pts.Length;
                lr.SetPositions(pts);
                lr.useWorldSpace = worldSpaceLines;
                lr.material = lineMaterial != null ? lineMaterial : new Material(Shader.Find("Sprites/Default"));
                lr.startWidth = lr.endWidth = lineWidth;
                lr.numCornerVertices = lr.numCapVertices = 4;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
            }
        }
    }
}