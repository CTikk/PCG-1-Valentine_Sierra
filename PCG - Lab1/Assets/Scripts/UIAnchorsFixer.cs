using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
[ExecuteAlways]
#endif
[DisallowMultipleComponent]
public class UIAnchorsFixer : MonoBehaviour
{
    [Header("Asigna tu ScrollRect principal de la pestaña")]
    public ScrollRect scrollRect;                 // tu ScrollRect contenido
    [Tooltip("Content del ScrollRect (si no se asigna, se toma de scrollRect.content)")]
    public RectTransform scrollContent;

    [Header("Opciones de corrección")]
    public bool fixRowsStretchX = true;           // filas (HorizontalLayoutGroup) se estiran en X
    public bool fixViewportAnchors = true;        // viewport full-rect
    public bool fixInputsTextArea = true;         // TextArea de TMP_InputField rellena el input
    public bool fixLabelsNoWrap = true;           // labels sin word-wrap + ancho preferido
    public float labelPreferredWidth = 260f;      // ancho de columna izquierda
    public float inputPreferredWidth = 240f;      // ancho por defecto inputs/dropdowns
    public Vector2 inputPadding = new Vector2(6, 6);

    [ContextMenu("Apply Fix Now")]
    public void ApplyFixNow()
    {
        if (!scrollRect) { Debug.LogWarning("UIAnchorsFixer: Asigna ScrollRect."); return; }
        if (!scrollContent) scrollContent = scrollRect.content;

        // 1) Viewport anchors (llena su rect)
        if (fixViewportAnchors && scrollRect.viewport)
        {
            var vp = scrollRect.viewport;
            vp.anchorMin = Vector2.zero;
            vp.anchorMax = Vector2.one;
            vp.offsetMin = Vector2.zero;
            vp.offsetMax = Vector2.zero;
        }

        // 2) Content del Scroll: anclado arriba, stretch en X (crece hacia abajo)
        if (scrollContent)
        {
            var c = scrollContent;
            c.anchorMin = new Vector2(0, 1);
            c.anchorMax = new Vector2(1, 1);
            c.pivot = new Vector2(0.5f, 1);
            c.offsetMin = new Vector2(c.offsetMin.x, c.offsetMin.y);
            c.offsetMax = new Vector2(c.offsetMax.x, c.offsetMax.y);
        }

        // 3) Filas: todo HorizontalLayoutGroup se estira en X
        if (fixRowsStretchX && scrollContent)
        {
            var rows = scrollContent.GetComponentsInChildren<HorizontalLayoutGroup>(true);
            foreach (var row in rows)
            {
                var rt = row.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 1);
                rt.sizeDelta = new Vector2(0, rt.sizeDelta.y); // que tome todo el ancho
            }
        }

        // 4) Labels (TMP): sin quebrar texto verticalmente
        if (fixLabelsNoWrap && scrollContent)
        {
            var labels = scrollContent.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in labels)
            {
                // Heurística: sólo tocar etiquetas (no el texto interno de inputs)
                if (t.GetComponentInParent<TMP_InputField>(true) != null) continue;
                if (t.GetComponentInParent<TMP_Dropdown>(true) != null) continue;

                t.enableWordWrapping = false;
                t.overflowMode = TextOverflowModes.Ellipsis;

                var le = t.GetComponent<LayoutElement>();
                if (!le) le = t.gameObject.AddComponent<LayoutElement>();
                if (!le.ignoreLayout && le.preferredWidth <= 0f)
                    le.preferredWidth = labelPreferredWidth;
            }
        }

        // 5) TMP_InputField: que el Text Area llene el input, y darle ancho por defecto
        if (fixInputsTextArea && scrollContent)
        {
            var inputs = scrollContent.GetComponentsInChildren<TMP_InputField>(true);
            foreach (var inp in inputs)
            {
                var rtIn = inp.GetComponent<RectTransform>();
                EnsurePreferredWidth(rtIn, inputPreferredWidth);

                // text viewport
                if (inp.textViewport)
                {
                    var rta = inp.textViewport;
                    rta.anchorMin = Vector2.zero;
                    rta.anchorMax = Vector2.one;
                    rta.offsetMin = inputPadding;
                    rta.offsetMax = -inputPadding;
                }
                // text component
                if (inp.textComponent)
                {
                    inp.textComponent.enableWordWrapping = false;
                    inp.textComponent.overflowMode = TextOverflowModes.Overflow;
                }
            }

            // 6) Dropdowns y Toggles: ancho razonable si no lo tenían
            var dds = scrollContent.GetComponentsInChildren<TMP_Dropdown>(true);
            foreach (var dd in dds)
                EnsurePreferredWidth(dd.GetComponent<RectTransform>(), inputPreferredWidth);

            var togs = scrollContent.GetComponentsInChildren<Toggle>(true);
            foreach (var tg in togs)
                EnsurePreferredWidth(tg.GetComponent<RectTransform>(), 60f);
        }

        // 7) Rebuild layouts para que el cambio sea visible al instante
        if (scrollContent)
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent);
        if (scrollRect.viewport)
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.viewport);
    }

    void EnsurePreferredWidth(RectTransform rt, float width)
    {
        if (!rt) return;
        var le = rt.GetComponent<LayoutElement>();
        if (!le) le = rt.gameObject.AddComponent<LayoutElement>();
        if (le.preferredWidth <= 0f) le.preferredWidth = width;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Aplica automáticamente en Edit al cambiar opciones
        if (isActiveAndEnabled && scrollRect)
            ApplyFixNow();
    }
#endif
}