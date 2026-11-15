using UnityEngine;

public class DistributionModule: ZoneModule
{
    [Header("Fault consequences")]
    [SerializeField] float burnNow = 30f;
    [SerializeField] float burnPerDay = 5f;
    [SerializeField] string brokenParam = "Broken";

    [Header("Repair")]
    [Tooltip("Тривалість ремонту інженером. Інші ролі ремонтувати цей блок не можуть.")]
    [SerializeField] float engineerRepairHours = 8f;

    protected override void Reset()
    {
        energyUse = 0f;
        oxygenUse = 0f;
        eventId = 5;
        repairChoiceNumber = 2;
    }

    protected override void OnFaultBegan() => SetBool(brokenParam, true);

    // ремонт не почали вчасно: згорає 30% одразу...
    protected override void OnPenalty() => AddEnergy(-burnNow);

    // ...і ще 5% щодня, поки не полагоджено
    protected override void OnPenaltyDay() => AddEnergy(-burnPerDay);

    protected override void OnFaultRepaired(bool wasPenalized) => SetBool(brokenParam, false);

    // ---- Ремонт: лише інженер, 8 годин ----
    public override bool CanWorkerDo(Worker w, WorkType task) =>
        task != WorkType.Repair || w.Role == WorkerRole.Engineer;

    public override float AdjustTaskHours(Worker w, WorkType task, float defaultHours) =>
        task == WorkType.Repair ? engineerRepairHours : base.AdjustTaskHours(w, task, defaultHours);

    // ремонт вважається розпочатим, лише якщо в блок призначено інженера
    protected override bool HasAssignedWorker() =>
        Worker.All.Exists(w => w != null && w.CurrentZone == zone && w.Role == WorkerRole.Engineer);
}
