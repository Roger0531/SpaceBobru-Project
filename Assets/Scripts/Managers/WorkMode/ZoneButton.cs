using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


/// <summary>Один варіант наказу: зона + робота в ній.</summary>
public class ZoneOption
{
    public readonly WorkZone zone;
    public readonly WorkType work;      // None = у зоні немає роботи, Repair = зона пошкоджена
    readonly bool showWork;

    public ZoneOption(WorkZone zone, WorkType work, bool showWork)
    {
        this.zone = zone;
        this.work = work;
        this.showWork = showWork;
    }

    /// <summary>Що записується працівнику: лише друга робота зони має значення.</summary>
    public WorkType Preferred =>
        work != WorkType.None && work == zone.SecondaryWork ? work : WorkType.None;

    public static string WorkText(WorkType w) => w switch
    {
        WorkType.Repair => "Repair",
        WorkType.Heal => "Healing",
        WorkType.Research => "Research",
        WorkType.Produce => "Production",
        _ => ""
    };

    public string Label =>
        work == WorkType.Repair || (showWork && work != WorkType.None)
            ? $"{zone.DisplayName} — {WorkText(work)}"
            : zone.DisplayName;

    /// <summary>Короткий підпис під іконкою героя (у два рядки).</summary>
    public string ShortLabel =>
        work == WorkType.Repair || (showWork && work != WorkType.None)
            ? $"{zone.DisplayName}\n{WorkText(work)}"
            : zone.DisplayName;
}

public class ZoneButton: MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] TMP_Text label;
    [SerializeField] GameObject selectedFrame;

    public ZoneOption Option { get; private set; }

    public void Setup(ZoneOption option, Action<ZoneOption> onClick)
    {
        Option = option;
        button.onClick.AddListener(() => onClick(option));
    }

    public void Refresh(bool isSelected, int occupied, bool interactable, bool isCurrent, string note)
    {
        string text = $"{Option.Label}  {occupied}/{Option.zone.MaxWorkers}";
        if (isCurrent) text += "  (current)";
        if (!string.IsNullOrEmpty(note)) text += $"  [{note}]";

        label.text = text;
        if (selectedFrame != null) selectedFrame.SetActive(isSelected);
        button.interactable = interactable;
    }
}
