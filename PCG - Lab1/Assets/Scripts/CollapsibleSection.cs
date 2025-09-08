using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class CollapsibleSection : MonoBehaviour
{
    [Header("Wire")]
    public Toggle enableToggle;         // Activa/desactiva la categoría (lógica)
    public Button foldButton;           // Plegar/Desplegar (UI)
    public TMP_Text titleText;          // "Perlin", "BSP", etc.
    public RectTransform contentRoot;   // Contenido de controles

    [Header("Estado")]
    public bool startExpanded = true;
    public bool startEnabled = true;

    public System.Action<bool> onEnableChanged; // callback opcional

    bool _expanded;

    void Awake()
    {
        if (enableToggle) enableToggle.onValueChanged.AddListener(OnEnableChanged);
        if (foldButton) foldButton.onClick.AddListener(ToggleFold);
    }

    void OnDestroy()
    {
        if (enableToggle) enableToggle.onValueChanged.RemoveListener(OnEnableChanged);
        if (foldButton) foldButton.onClick.RemoveListener(ToggleFold);
    }

    void Start()
    {
        SetExpanded(startExpanded, true);
        if (enableToggle) enableToggle.isOn = startEnabled;
        RefreshFoldGlyph();
    }

    public void SetTitle(string title)
    {
        if (titleText) titleText.text = title;
    }

    public void SetExpanded(bool expanded, bool instant = false)
    {
        _expanded = expanded;
        if (contentRoot) contentRoot.gameObject.SetActive(_expanded);
        RefreshFoldGlyph();
    }

    public void ToggleFold() => SetExpanded(!_expanded);

    void OnEnableChanged(bool on)
    {
        // Opcional: atenuar visualmente cuando está desactivado
        if (contentRoot)
        {
            var cg = contentRoot.GetComponent<CanvasGroup>();
            if (!cg) cg = contentRoot.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = on ? 1f : 0.45f;
        }
        onEnableChanged?.Invoke(on);
    }

    void RefreshFoldGlyph()
    {
        if (!foldButton) return;
        var label = foldButton.GetComponentInChildren<TMP_Text>();
        if (label) label.text = _expanded ? "v" : ">";
    }

    // Helpers para que GenerationUI consulte estado:
    public bool IsEnabled() => enableToggle ? enableToggle.isOn : true;
    public bool IsExpanded() => _expanded;
}