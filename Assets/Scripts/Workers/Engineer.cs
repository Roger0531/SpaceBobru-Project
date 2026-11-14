using UnityEngine;

public class Engineer : Worker
{
    [SerializeField] float repairHours = 2f;
    [SerializeField] float healHours = 6f;
    [SerializeField] float researchHours = 6f;
    [SerializeField] float produceHours = 8f;

    public override WorkerRole Role => WorkerRole.Engineer;

    public override float GetWorkHours(WorkType work) => work switch
    {
        WorkType.Repair => repairHours,
        WorkType.Heal => healHours,
        WorkType.Research => researchHours,
        _ => 0f
    };
}
