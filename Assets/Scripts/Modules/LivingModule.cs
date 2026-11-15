using System;
using UnityEngine;

public class LivingModule: ZoneModule
{
    [Header("Night protection")]
    [Tooltip("Енергія, що списується на відбої (мінус бонус від досліджень)")]
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

    protected override void Start()
    {
        base.Start();

        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnBedtime += StartRest;
            TimeManager.Instance.OnWake += EndRest;
        }
    }

    protected override void OnDestroy()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnBedtime -= StartRest;
            TimeManager.Instance.OnWake -= EndRest;
        }

        base.OnDestroy();
    }

    void StartRest()
    {
        var rm = ResourceManager.Instance;
        float cost = Mathf.Max(0f, protectionEnergyCost - Bonus(ResearchEffectKind.LivingRestEnergy));

        string failure = null;
        if (rm == null) failure = "ResourceManager not found";
        else if (zone.IsDamaged) failure = "living module is damaged";
        else if (!rm.TrySpendPercent(ResourceType.Energy, cost)) failure = $"not enough energy ({cost} needed)";

        if (failure == null)
        {
            ProtectionActive = true;
            SetBool(protectionParam, true);
            OnProtectionChanged?.Invoke(true);
            return;
        }

        Debug.LogWarning($"LivingModule: night protection failed: {failure}");

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
