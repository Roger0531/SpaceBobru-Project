using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ZoneButton: MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] TMP_Text label;
    [SerializeField] GameObject selectedFrame;

    public WorkZone Zone { get; private set; }

    public void Setup(WorkZone zone, Action<WorkZone> onClick)
    {
        Zone = zone;
        button.onClick.AddListener(() => onClick(zone));
    }

    public void Refresh(bool isSelected, int occupied, bool interactable)
    {
        string text = $"{Zone.DisplayName}  {occupied}/{Zone.MaxWorkers}";
        if (Zone.IsDamaged) text += "  !";

        label.text = text;
        if (selectedFrame != null) selectedFrame.SetActive(isSelected);
        button.interactable = interactable;
    }
}
