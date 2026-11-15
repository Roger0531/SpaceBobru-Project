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
    [Tooltip("Необов'язково: галочка, коли героєві віддано наказ")]
    [SerializeField] GameObject orderedMark;
    [SerializeField] TMP_Text zoneLabel;

    [Header("HP")]
    [Tooltip("Image з Image Type = Filled, Fill Method = Horizontal")]
    [SerializeField] Image hpFill;
    [SerializeField] TMP_Text hpLabel;
    [Tooltip("Червона рамка для героя на межі (HP не більше порога допомоги)")]
    [SerializeField] GameObject helpFrame;

    static readonly Color HpNormal = new Color(0.30f, 0.80f, 0.35f);
    static readonly Color HpDanger = new Color(0.90f, 0.15f, 0.15f);

    public void Setup(Worker worker, bool available, Action onClick)
    {
        if (icon != null && worker.Icon != null) icon.sprite = worker.Icon;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick());
        button.interactable = available;

        if (unavailableOverlay != null) unavailableOverlay.SetActive(!available);
        SetSelected(false);
        SetOrdered(false);
        ShowHp(worker);
    }

    public void ShowHp(Worker w)
    {
        float max = Mathf.Max(0.01f, w.MaxHp);

        if (hpFill != null)
        {
            hpFill.fillAmount = Mathf.Clamp01(w.Hp / max);
            hpFill.color = w.NeedsHelp ? HpDanger : HpNormal;
        }
        if (hpLabel != null) hpLabel.text = $"{w.Hp:0.#}/{w.MaxHp:0.#}";
        if (helpFrame != null) helpFrame.SetActive(w.NeedsHelp);
    }

    public void SetSelected(bool value)
    {
        if (selectedFrame != null) selectedFrame.SetActive(value);
    }

    public void SetOrdered(bool value)
    {
        if (orderedMark != null) orderedMark.SetActive(value);
    }

    public void SetZoneLabel(string text)
    {
        if (zoneLabel != null) zoneLabel.text = text;
    }
}
