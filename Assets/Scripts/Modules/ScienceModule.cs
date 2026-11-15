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

        int researchers = zone.CountDoing(WorkType.Research);
        if (researchers > 0) AddOxygen(-oxygenPerResearcher * researchers);

        if (zone.CountDoing(WorkType.Heal) >= zone.HealWorkersRequired)
        {
            var rm = ResourceManager.Instance;
            if (rm != null) rm.Change(ResourceType.Food, -healFood);
            AddOxygen(-healOxygen);
            AddEnergy(-healEnergy);
        }
    }

    // без їжі лікування не йде
    public override bool CanWorkerDo(Worker w, WorkType task)
    {
        if (task != WorkType.Heal) return true;
        var rm = ResourceManager.Instance;
        return rm != null && rm.GetValue(ResourceType.Food) >= healFood;
    }
}
