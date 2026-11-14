using UnityEngine;

public class Medic : Worker
{
    [SerializeField] float repairHours = 4f;
    [SerializeField] float healHours = 2f;
    [SerializeField] float researchHours = 5f;
    [SerializeField] float produceHours = 4f;

    public override WorkerRole Role => WorkerRole.Medic;

    public override float GetWorkHours(WorkType work) => work switch
    {
        WorkType.Repair => repairHours,
        WorkType.Heal => healHours,
        WorkType.Research => researchHours,
        WorkType.Produce => produceHours,
        _ => 0f
    };
}