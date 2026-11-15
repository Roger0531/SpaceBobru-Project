using UnityEngine;

public class LifeSupportModule: ZoneModule
{
    [Header("Fault consequences")]
    [Tooltip("0.5 = вироблення кисню зменшується на 50% від того, що було")]
    [SerializeField, Range(0f, 1f)] float brokenProductionMultiplier = 0.5f;
    [SerializeField] float oxygenLossNow = 40f;
    [SerializeField] string brokenParam = "Broken";
    protected override float OxygenProducedBonus => Bonus(ResearchEffectKind.LifeSupportOxygen);

    protected override void Reset()
    {
        energyUse = 2.5f;
        oxygenProduced = 30f;
        eventId = 5;               // перевірте: у розподільного блоку теж 5
        repairChoiceNumber = 1;
    }

    protected override void OnFaultBegan() => SetBool(brokenParam, true);

    protected override void OnPenalty()
    {
        oxygenProductionMul = brokenProductionMultiplier;
        AddOxygen(-oxygenLossNow);
    }

    protected override void OnFaultRepaired(bool wasPenalized)
    {
        oxygenProductionMul = 1f;
        SetBool(brokenParam, false);
    }
}
