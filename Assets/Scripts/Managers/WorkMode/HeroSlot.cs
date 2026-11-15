using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HeroSlot: MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] Image icon;
    [SerializeField] GameObject selectedFrame;
    [SerializeField] GameObject unavailableOverlay;
    [SerializeField] TMP_Text zoneLabel;

    public void Setup(Worker worker, bool available, Action onClick)
    {
        if (icon != null && worker.Icon != null) icon.sprite = worker.Icon;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick());
        button.interactable = available;

        if (unavailableOverlay != null) unavailableOverlay.SetActive(!available);
        SetSelected(false);
    }

    public void SetSelected(bool value)
    {
        if (selectedFrame != null) selectedFrame.SetActive(value);
    }

    public void SetZoneLabel(string text)
    {
        if (zoneLabel != null) zoneLabel.text = text;
    }
}
