using UnityEngine;

public class ScienceModule: ZoneModule
{
    [Header("Research costs")]
    [SerializeField] float oxygenPerResearcher = 3f;

    [Header("Healing costs (за 4 години лікування, на групу)")]
    [SerializeField] float healFood = 1f;
    [SerializeField] float healOxygen = 10f;
    [SerializeField] float healEnergy = 5f;

    protected override void Reset()
    {
        energyUse = 2.5f;
        oxygenUse = 3f;
    }

    protected override void OnCycle()
    {
        ApplyBaseCycle();

        // кожен, хто зараз досліджує (має обрану соту), витрачає кисень
        int researchers = zone.CountDoing(WorkType.Research);
        if (researchers > 0) AddOxygen(-oxygenPerResearcher * researchers);

        // лікування: витрати на всю групу
        if (zone.CountDoing(WorkType.Heal) >= zone.HealWorkersRequired)
        {
            var rm = ResourceManager.Instance;
            if (rm != null) rm.Change(ResourceType.Food, -healFood);
            AddOxygen(-healOxygen);
            AddEnergy(-healEnergy);
        }
    }

    public override bool CanWorkerDo(Worker w, WorkType task)
    {
        // досліджувати можна лише обрану соту (тривалість рахує Worker через ResearchTree)
        if (task == WorkType.Research)
            return ResearchTree.Instance != null && ResearchTree.Instance.HasClaim(w);

        // без їжі лікування не йде
        if (task == WorkType.Heal)
        {
            var rm = ResourceManager.Instance;
            return rm != null && rm.GetValue(ResourceType.Food) >= healFood;
        }

        return true;
    }
}
