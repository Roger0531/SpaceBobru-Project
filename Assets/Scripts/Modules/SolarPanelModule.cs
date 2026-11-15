using UnityEngine;

public class SolarPanelModule: ZoneModule
{
    [Header("Events")]
    [SerializeField] int dirtEventId = 4;
    [SerializeField] int cableEventId = 1;
    [Tooltip("0.75 = вироблення зменшується на 25% від того, що було")]
    [SerializeField, Range(0f, 1f)] float dirtMultiplier = 0.75f;

    [Header("Animator bools")]
    [SerializeField] string dirtyParam = "Dirty";
    [SerializeField] string cableBrokenParam = "CableBroken";

    bool dirty;
    bool cableBroken;

    public bool IsDirty => dirty;
    public bool IsCableBroken => cableBroken;

    protected override void Reset()
    {
        energyUse = 0f;
        oxygenUse = 0f;
        energyProduced = 15f;
    }

    protected override void HandleEventShown(EventInfo e)
    {
        if (e.eventID == dirtEventId && !dirty)
        {
            dirty = true;
            SetBool(dirtyParam, true);
        }
        else if (e.eventID == cableEventId && !cableBroken)
        {
            cableBroken = true;
            SetBool(cableBrokenParam, true);
        }
        else return;

        RecalculateProduction();
        zone.Break();
    }

    // у сонячної панелі відповіді не впливають на наслідки
    protected override void HandleChoiceSelected(EventInfo e, EventChoice c) { }

    // кожен ремонт усуває одну несправність: спершу кабель, потім забруднення
    protected override void OnRepairApplied()
    {
        if (cableBroken)
        {
            cableBroken = false;
            SetBool(cableBrokenParam, false);
        }
        else if (dirty)
        {
            dirty = false;
            SetBool(dirtyParam, false);
        }
        else return;

        RecalculateProduction();
        if (dirty || cableBroken) zone.Break(); // лишилась ще одна несправність
    }

    void RecalculateProduction() =>
        energyProductionMul = cableBroken ? 0f : (dirty ? dirtMultiplier : 1f);

    protected override float EnergyProducedBonus => Bonus(ResearchEffectKind.SolarEnergy);

    protected override bool TryAvoidEvent(EventInfo e)
    {
        if (e.eventID != dirtEventId && e.eventID != cableEventId) return false;

        float avoid = Bonus(ResearchEffectKind.SolarBreakdownChance) / 100f;
        return avoid > 0f && Random.value < avoid;
    }
}
