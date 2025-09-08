using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PanelToggle : MonoBehaviour
{
    [Header("Wire")]
    public RectTransform panel;     // El panel a abrir/cerrar (ej: tu ventana/tab root)
    public Button toggleButton;     // Botón que invoca el toggle (opcional)

    [Header("Animación")]
    public bool slideFromLeft = true;
    [Range(0.05f, 0.6f)] public float animTime = 0.18f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Tecla rápida")]
    public KeyCode hotkey = KeyCode.Tab; // pulsa Tab para mostrar/ocultar

    [Header("Estado inicial")]
    public bool startOpen = true;

    Vector2 shownPos;
    Vector2 hiddenPos;
    bool isOpen;
    Coroutine co;

    void Awake()
    {
        if (!panel)
        {
            Debug.LogError("[PanelToggle] Asigna el RectTransform del panel.");
            enabled = false;
            return;
        }

        // Guardamos la posición "visible" actual
        shownPos = panel.anchoredPosition;

        // Calculamos posición oculta fuera de pantalla en X o Y
        var size = panel.rect.size;
        if (slideFromLeft) hiddenPos = shownPos + new Vector2(-size.x - 40f, 0f);
        else hiddenPos = shownPos + new Vector2(0f, size.y + 40f);

        if (toggleButton) toggleButton.onClick.AddListener(TogglePanel);

        // Estado inicial
        SetInstant(startOpen);
    }

    void OnDestroy()
    {
        if (toggleButton) toggleButton.onClick.RemoveListener(TogglePanel);
    }

    void Update()
    {
        if (hotkey != KeyCode.None && Input.GetKeyDown(hotkey))
            TogglePanel();
    }

    public void TogglePanel()
    {
        SetOpen(!isOpen, animated: true);
    }

    public void Open() => SetOpen(true, true);
    public void Close() => SetOpen(false, true);

    void SetInstant(bool open)
    {
        isOpen = open;
        panel.anchoredPosition = open ? shownPos : hiddenPos;
        var cg = panel.GetComponent<CanvasGroup>() ?? panel.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = open ? 1f : 0f;
        cg.blocksRaycasts = cg.interactable = open;
    }

    void SetOpen(bool open, bool animated)
    {
        isOpen = open;
        if (!animated)
        {
            SetInstant(open);
            return;
        }
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(Animate(open));
    }

    IEnumerator Animate(bool open)
    {
        var fromPos = panel.anchoredPosition;
        var toPos = open ? shownPos : hiddenPos;

        var cg = panel.GetComponent<CanvasGroup>() ?? panel.gameObject.AddComponent<CanvasGroup>();
        if (open) { panel.gameObject.SetActive(true); cg.blocksRaycasts = true; cg.interactable = true; }

        float t = 0f;
        while (t < animTime)
        {
            t += Time.unscaledDeltaTime; // no depende del timescale
            float k = ease.Evaluate(Mathf.Clamp01(t / animTime));
            panel.anchoredPosition = Vector2.LerpUnclamped(fromPos, toPos, k);
            cg.alpha = Mathf.Lerp(open ? 0f : 1f, open ? 1f : 0f, k);
            yield return null;
        }

        panel.anchoredPosition = toPos;
        cg.alpha = open ? 1f : 0f;
        cg.blocksRaycasts = cg.interactable = open;
        if (!open) panel.gameObject.SetActive(true); // dejamos activo para no romper layouts
        co = null;
    }
}