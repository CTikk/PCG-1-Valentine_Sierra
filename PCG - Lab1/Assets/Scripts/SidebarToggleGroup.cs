using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class SidebarToggleGroup : MonoBehaviour
{
    [System.Serializable]
    public class Section
    {
        [Header("Wiring")]
        public Button button;             // Botón de la izquierda
        public GameObject panel;          // El panel a abrir/cerrar (ScrollRect root o contenedor)

        [Header("Opcional (visual)")]
        public TMP_Text chevronText;      // Un TMP_Text para mostrar ▾ / ▸ (opcional)
        public RectTransform chevronIcon; // O un ícono (Image) para rotarlo (opcional)

        [Header("Estado inicial")]
        public bool startOpen = true;

        [Header("Animación (opcional)")]
        public bool animate = true;
        [Range(0.05f, 0.5f)] public float animTime = 0.15f;

        // Internos
        [HideInInspector] public bool isOpen;
        [HideInInspector] public Coroutine animCo;
        [HideInInspector] public LayoutElement layout; // si queremos animación suave
        [HideInInspector] public float expandedHeight = -1f;
    }

    [Header("Secciones")]
    public List<Section> sections = new List<Section>();

    void Awake()
    {
        // Hook de botones + estado inicial
        foreach (var s in sections)
        {
            if (s.button != null)
                s.button.onClick.AddListener(() => ToggleSection(s));

            // Asegura LayoutElement si hay animación
            if (s.panel != null && s.animate)
            {
                s.layout = s.panel.GetComponent<LayoutElement>();
                if (s.layout == null) s.layout = s.panel.AddComponent<LayoutElement>();
                s.layout.minHeight = 0f;
                s.layout.flexibleHeight = 0f;
                s.layout.preferredHeight = -1f; // autosize
            }
        }
    }

    void Start()
    {
        foreach (var s in sections)
        {
            SetOpen(s, s.startOpen, instant: true);
        }
    }

    public void ToggleSection(Section s)
    {
        SetOpen(s, !s.isOpen, instant: false);
    }

    public void SetOpen(Section s, bool open, bool instant)
    {
        s.isOpen = open;

        // Actualiza chevron (texto o icono)
        if (s.chevronText)
            s.chevronText.text = open ? "v" : ">";
        if (s.chevronIcon)
            s.chevronIcon.localEulerAngles = open ? Vector3.zero : new Vector3(0, 0, 90f);

        if (s.panel == null)
            return;

        if (!s.animate || instant)
        {
            if (s.animate && s.layout != null)
            {
                s.layout.preferredHeight = -1f; // autosize cuando está abierto
                s.layout.minHeight = 0f;
            }
            s.panel.SetActive(open);
            return;
        }

        // Animación suave: expandir/colapsar height
        if (s.animCo != null) StopCoroutine(s.animCo);
        s.animCo = StartCoroutine(AnimateSection(s, open));
    }

    IEnumerator AnimateSection(Section s, bool open)
    {
        // si abrimos, activar el panel antes de medir
        if (open) s.panel.SetActive(true);

        if (s.layout == null)
        {
            s.layout = s.panel.GetComponent<LayoutElement>();
            if (!s.layout) s.layout = s.panel.AddComponent<LayoutElement>();
        }

        // Medir altura "natural" del panel cuando está abierto
        if (open)
        {
            // Forzar una actualización de layout para obtener altura correcta
            LayoutRebuilder.ForceRebuildLayoutImmediate(s.panel.GetComponent<RectTransform>());
            s.expandedHeight = s.panel.GetComponent<RectTransform>().rect.height;
        }
        else
        {
            if (s.expandedHeight < 0f)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(s.panel.GetComponent<RectTransform>());
                s.expandedHeight = s.panel.GetComponent<RectTransform>().rect.height;
            }
        }

        float from = open ? 0f : Mathf.Max(0.0001f, s.expandedHeight);
        float to = open ? Mathf.Max(0.0001f, s.expandedHeight) : 0f;

        float t = 0f;
        float dur = Mathf.Max(0.01f, s.animTime);
        s.layout.minHeight = 0f;   // evita “saltos”
        s.layout.flexibleHeight = 0f;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime; // sin afectar por timescale
            float k = Mathf.SmoothStep(0f, 1f, t / dur);
            float h = Mathf.Lerp(from, to, k);
            s.layout.preferredHeight = h;
            yield return null;
        }
        s.layout.preferredHeight = to;

        if (!open)
            s.panel.SetActive(false); // al final, desactivar cuando está colapsado

        s.animCo = null;
    }

    // Abre/cierra todas (opcional; puedes llamarlo desde botones extra)
    public void OpenAll()
    {
        foreach (var s in sections) SetOpen(s, true, instant: false);
    }
    public void CloseAll()
    {
        foreach (var s in sections) SetOpen(s, false, instant: false);
    }
}