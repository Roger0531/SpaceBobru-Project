using System;
using UnityEngine;

public class LivingModule: ZoneModule
{
    [Header("Rest / protection")]
    [SerializeField, Range(0, 23)] int restStartHour = 22;
    [SerializeField, Range(0, 23)] int restEndHour = 6;
    [SerializeField] float protectionEnergyCost = 25f;
    [SerializeField] float hpLossWithoutProtection = 1f;
    [SerializeField] string protectionParam = "Protection";

    public bool ProtectionActive { get; private set; }
    public event Action<bool> OnProtectionChanged;   // true = увімкнено, false = вимкнено
    public event Action OnProtectionFailed;

    protected override void Reset()
    {
        energyUse = 2.5f;
        oxygenUse = 12f;
    }

    protected override void OnHour(int day, int hour)
    {
        if (hour == restStartHour) StartRest();
        else if (hour == restEndHour) EndRest();
    }

    void StartRest()
    {
        var rm = ResourceManager.Instance;
        bool ok = rm != null && !zone.IsDamaged &&
                  rm.TrySpendPercent(ResourceType.Energy, protectionEnergyCost);

        if (ok)
        {
            ProtectionActive = true;
            SetBool(protectionParam, true);
            OnProtectionChanged?.Invoke(true);
            return;
        }

        // захист не ввімкнувся: уся команда втрачає HP
        foreach (var w in Worker.All.ToArray())
            if (w != null) w.ChangeHp(-hpLossWithoutProtection);

        OnProtectionFailed?.Invoke();
    }

    void EndRest()
    {
        if (!ProtectionActive) return;
        ProtectionActive = false;
        SetBool(protectionParam, false);
        OnProtectionChanged?.Invoke(false);
    }
}
