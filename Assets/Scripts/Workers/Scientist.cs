using UnityEngine;

public class Scientist : Worker
{
    [SerializeField] float repairHours = 4f;
    [SerializeField] float healHours = 5f;
    [SerializeField] float researchHours = 2f;
    [SerializeField] float produceHours = 8f;

    public override WorkerRole Role => WorkerRole.Scientist;

    public override float GetWorkHours(WorkType work) => work switch
    {
        WorkType.Repair => repairHours,
        WorkType.Heal => healHours,
        WorkType.Research => researchHours,
        WorkType.Produce => produceHours,
        _ => 0f
    };
}