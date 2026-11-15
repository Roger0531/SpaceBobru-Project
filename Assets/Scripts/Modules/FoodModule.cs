using UnityEngine;

public class FoodModule: ZoneModule
{
    [Header("Work costs")]
    [SerializeField] float oxygenPerPersonProducing = 3f;

    [Header("Fungus event")]
    [SerializeField] float cleaningHours = 16f;
    [SerializeField] float cleaningEnergyCost = 5f;
    [SerializeField] string infectedParam = "Infected";

    bool infected;

    protected override void Reset()
    {
        energyUse = 2.5f;
        oxygenUse = 10f;
        eventId = 6;
        repairChoiceNumber = 1;   // номер відповіді «чистити»
    }

    public override float ProductionBonus => Bonus(ResearchEffectKind.FoodYield);

    protected override void OnCycle()
    {
        ApplyBaseCycle();

        // додаткові витрати кисню за кожну людину, поки виробляється їжа
        int producing = zone.CountDoing(WorkType.Produce);
        float perPerson = Mathf.Max(0f, oxygenPerPersonProducing - Bonus(ResearchEffectKind.FoodOxygenPerPerson));
        if (producing > 0 && perPerson > 0f) AddOxygen(-perPerson * producing);
    }

    protected override void HandleEventShown(EventInfo e)
    {
        if (eventId < 0 || e.eventID != eventId || infected) return;

        infected = true;
        zone.Break();
        SetBool(infectedParam, true);

        // подія без вибору: чистити може лише медик, тож він їде одразу
        if (!HasChoices(e)) TryAutoRepair(force: false);
    }

    protected override void HandleChoiceSelected(EventInfo e, EventChoice c)
    {
        if (eventId < 0 || e.eventID != eventId || !infected) return;

        if (ChoiceNumber(e, c) == repairChoiceNumber)
        {
            TryAutoRepair(force: true);   // обрано чистку: медик їде одразу
            return;
        }

        // не чистити: весь запас їжі втрачено, грибок знищено разом із ним
        var rm = ResourceManager.Instance;
        if (rm != null) rm.Change(ResourceType.Food, -rm.GetValue(ResourceType.Food));

        infected = false;
        zone.RepairFully();
        SetBool(infectedParam, false);
    }

    // чистить лише медик, 16 годин поспіль
    public override bool CanWorkerDo(Worker w, WorkType task) =>
        !(infected && task == WorkType.Repair) || w.Role == WorkerRole.Medic;

    public override float AdjustTaskHours(Worker w, WorkType task, float defaultHours) =>
        infected && task == WorkType.Repair ? cleaningHours : base.AdjustTaskHours(w, task, defaultHours);

    protected override void OnRepairApplied()
    {
        if (!infected) return;

        infected = false;
        SetBool(infectedParam, false);
        AddEnergy(-cleaningEnergyCost);
    }
}
