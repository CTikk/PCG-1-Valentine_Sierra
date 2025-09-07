using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class LSystemController : MonoBehaviour
{
    [Header("Refs")]
    public LSystem lsystem;
    public TurtleDrawer drawer;

    [Header("UI")]
    public TMP_Dropdown presetDropdown;
    public Slider iterSlider, angleSlider, stepSlider;
    public TMP_Text iterText, angleText, stepText;
    public Toggle is3DToggle, lineToggle;

    bool _initialized;

    void Awake()
    {
        // Busca drawer si se te olvidó arrastrarlo
        if (!drawer) drawer = FindObjectOfType<TurtleDrawer>();
    }

    void OnEnable()
    {
        HookUI();
        SafeDefaults();     // valores mínimo
        BuildPreset(presetDropdown ? presetDropdown.value : 0);
        ApplyUI();
        GenerateAndDraw();
        _initialized = true;
    }

    void HookUI()
    {
        if (presetDropdown)
        {
            presetDropdown.onValueChanged.RemoveAllListeners();
            presetDropdown.onValueChanged.AddListener(OnPresetChanged);
        }
        if (iterSlider)
        {
            iterSlider.onValueChanged.RemoveAllListeners();
            iterSlider.onValueChanged.AddListener(_ => OnAnyUIChanged());
        }
        if (angleSlider)
        {
            angleSlider.onValueChanged.RemoveAllListeners();
            angleSlider.onValueChanged.AddListener(_ => OnAnyUIChanged());
        }
        if (stepSlider)
        {
            stepSlider.onValueChanged.RemoveAllListeners();
            stepSlider.onValueChanged.AddListener(_ => OnAnyUIChanged());
        }
        if (is3DToggle)
        {
            is3DToggle.onValueChanged.RemoveAllListeners();
            is3DToggle.onValueChanged.AddListener(_ => OnAnyUIChanged());
        }
        if (lineToggle)
        {
            lineToggle.onValueChanged.RemoveAllListeners();
            lineToggle.onValueChanged.AddListener(_ => OnAnyUIChanged());
        }
    }

    void SafeDefaults()
    {
        if (iterSlider && iterSlider.value < 1) iterSlider.value = 3;        // evita iters=0
        if (angleSlider && angleSlider.value <= 0f) angleSlider.value = 25f; // evita ángulo 0
        if (stepSlider && stepSlider.value <= 0f) stepSlider.value = 0.18f;  // evita step 0
        if (lineToggle) lineToggle.isOn = true;
    }

    public void ApplyUI()
    {
        if (!drawer) return;
        drawer.is3D = is3DToggle && is3DToggle.isOn;
        drawer.useLineRenderer = lineToggle && lineToggle.isOn;
        if (stepSlider) drawer.step = stepSlider.value;
        if (angleSlider) drawer.angle = angleSlider.value;

        if (iterText && iterSlider) iterText.text = ((int)iterSlider.value).ToString();
        if (angleText && angleSlider) angleText.text = angleSlider.value.ToString("F0");
        if (stepText && stepSlider) stepText.text = stepSlider.value.ToString("F2");
    }

    public void OnAnyUIChanged()
    {
        if (!_initialized) return;
        ApplyUI();
        GenerateAndDraw();
    }

    public void OnPresetChanged(int idx)
    {
        BuildPreset(idx);
        ApplyUI();
        GenerateAndDraw();
    }

    void BuildPreset(int idx)
    {
        if (!lsystem) lsystem = ScriptableObject.CreateInstance<LSystem>();
        lsystem.rules.Clear();

        switch (idx)
        {
            // 0) Koch 2D
            default:
            case 0:
                lsystem.axiom = "F";
                lsystem.rules.Add(new LRule { symbol = 'F', productions = new[] { "F+F--F+F" } });
                if (drawer) { drawer.is3D = false; drawer.angle = 60f; drawer.step = 0.2f; }
                break;

            // 1) Planta bracketed 2D
            case 1:
                lsystem.axiom = "X";
                lsystem.rules.Add(new LRule { symbol = 'X', productions = new[] { "F-[[X]+X]+F[+FX]-X" } });
                lsystem.rules.Add(new LRule { symbol = 'F', productions = new[] { "FF" } });
                if (drawer) { drawer.is3D = false; drawer.angle = 25f; drawer.step = 0.15f; }
                if (iterSlider && iterSlider.value < 4) iterSlider.value = 4; // para que haya suficientes F
                break;

            // 2) Árbol 3D
            case 2:
                lsystem.axiom = "X";
                lsystem.rules.Add(new LRule { symbol = 'X', productions = new[] { "F[+X][-X]&X" } });
                lsystem.rules.Add(new LRule { symbol = 'F', productions = new[] { "FF" } });
                if (drawer) { drawer.is3D = true; drawer.angle = 22.5f; drawer.step = 0.25f; }
                if (iterSlider && iterSlider.value < 3) iterSlider.value = 3;
                break;
        }
        lsystem.seed = Random.Range(int.MinValue / 2, int.MaxValue / 2);
        lsystem.MarkDirty();
    }

    public void GenerateAndDraw()
    {
        if (!lsystem || !drawer) return;
        int iters = (int)(iterSlider ? iterSlider.value : 3);

        var seq = lsystem.Generate(iters);
        // Debug: ver cuántos F reales hay
        Debug.Log($"len={seq.Length}, F={System.Linq.Enumerable.Count(seq, c => c=='F')}, iters={iters}");

        drawer.Draw(seq);
    }

    public void Clear() => drawer?.Clear();
}