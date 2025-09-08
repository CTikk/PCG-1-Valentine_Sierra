using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TabSwitcher : MonoBehaviour
{
    [Header("Botones (en orden)")]
    public List<Button> tabButtons;

    [Header("Panels (en mismo orden que botones)")]
    public List<GameObject> tabPanels;

    [Header("Título")]
    public TMP_Text titleLabel;

    [Header("Nombres de pestañas")]
    public List<string> tabNames = new List<string> { "Terrain", "BSP", "Houses", "Trees" };

    int _active = -1;

    void Awake()
    {
        for (int i = 0; i < tabButtons.Count; i++)
        {
            int idx = i;
            tabButtons[i].onClick.AddListener(() => Activate(idx));
        }
    }

    void Start()
    {
        Activate(0);
    }

    public void Activate(int index)
    {
        if (index < 0 || index >= tabPanels.Count) return;
        if (_active == index) return;
        _active = index;

        for (int i = 0; i < tabPanels.Count; i++)
            if (tabPanels[i]) tabPanels[i].SetActive(i == index);

        if (titleLabel && index < tabNames.Count)
            titleLabel.text = tabNames[index];
    }

    public int ActiveIndex() => _active;
}