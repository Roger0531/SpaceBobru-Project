using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Mission
{
    public string id;
    public string displayName;
    public float durationHours;
    public float energyCost;
    public float oxygenCost;
}

public class Rover : MonoBehaviour
{
    public static Rover Instance { get; private set; }

    [SerializeField] float hpLossPerTrip = 1f;
    [SerializeField] float minEnergyAfterTrip = 0f;
    [SerializeField] float minOxygenAfterTrip = 0f;
    [SerializeField]
    List<Mission> missions = new()
    {
        new Mission { id = "relay",   displayName = "Install relay",         durationHours = 4f,  energyCost = 5f,  oxygenCost = 3f },
        new Mission { id = "quest_2", displayName = "Quest 2",               durationHours = 4f,  energyCost = 5f,  oxygenCost = 3f },
        new Mission { id = "pragyan", displayName = "Fix Pragyan rover LRA", durationHours = 12f, energyCost = 15f, oxygenCost = 9f },
        new Mission { id = "quest_4", displayName = "Quest 4",               durationHours = 8f,  energyCost = 10f, oxygenCost = 6f },
    };

    public IReadOnlyList<Mission> Missions => missions;
    public Mission CurrentMission { get; private set; }
    public Worker Driver { get; private set; }
    public bool IsBusy => CurrentMission != null;
    public float HoursLeft { get; private set; }
    public float Progress => hoursTotal > 0f ? Mathf.Clamp01(1f - HoursLeft / hoursTotal) : 0f;

    public event Action<Mission> OnMissionUnlocked;
    public event Action<Mission, Worker> OnMissionStarted;
    public event Action<Mission, Worker> OnMissionCompleted;
    public event Action<Mission, string> OnMissionCancelled;

    readonly HashSet<string> unlocked = new();
    readonly HashSet<string> completed = new();
    float hoursTotal;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        TimeManager.Instance.OnHourTick += HandleHourTick;
    }

    void OnDestroy()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnHourTick -= HandleHourTick;
    }

    void HandleHourTick(int day, int hour)
    {
        if (!IsBusy) return;

        HoursLeft -= 1f;
        if (HoursLeft <= 0f) Complete();
    }

    public void Unlock(string missionId)
    {
        var mission = Find(missionId);
        if (mission == null)
        {
            Debug.LogWarning($"Mission '{missionId}' not found");
            return;
        }
        if (completed.Contains(missionId) || !unlocked.Add(missionId)) return;

        Debug.Log($"Mission available: {mission.displayName}");
        OnMissionUnlocked?.Invoke(mission);
    }

    public bool IsUnlocked(string missionId) => unlocked.Contains(missionId);
    public bool IsCompleted(string missionId) => completed.Contains(missionId);

    public List<Mission> GetAvailableMissions() =>
        IsBusy ? new List<Mission>() : missions.FindAll(m => unlocked.Contains(m.id) && !completed.Contains(m.id));

    public bool TrySend(int workerId, string missionId) => TrySend(Worker.FindById(workerId), missionId);

    public bool TrySend(Worker worker, string missionId)
    {
        var mission = Find(missionId);
        if (mission == null)
        {
            Debug.LogWarning($"Mission '{missionId}' not found");
            return false;
        }

        if (!CanSend(worker, mission, out string reason))
        {
            Debug.LogWarning($"Mission '{mission.displayName}' cancelled: {reason}");
            OnMissionCancelled?.Invoke(mission, reason);
            return false;
        }

        var rm = ResourceManager.Instance;
        if (rm != null)
        {
            rm.Change(ResourceType.Energy, -mission.energyCost);
            rm.Change(ResourceType.Oxygen, -mission.oxygenCost);
        }

        worker.Unassign();
        worker.ChangeHp(-hpLossPerTrip);
        Driver = worker;

        CurrentMission = mission;
        hoursTotal = HoursLeft = mission.durationHours;

        Debug.Log($"{worker.DisplayName} left on mission: {mission.displayName}");
        OnMissionStarted?.Invoke(mission, worker);
        return true;
    }

    public bool CanSend(Worker worker, string missionId)
    {
        var mission = Find(missionId);
        return mission != null && CanSend(worker, mission, out _);
    }

    public bool CanSend(Worker worker, Mission mission, out string reason)
    {
        reason = null;

        if (completed.Contains(mission.id)) { reason = "mission already completed"; return false; }
        if (!unlocked.Contains(mission.id)) { reason = "mission is not available yet"; return false; }
        if (IsBusy) { reason = "rover is busy"; return false; }
        if (worker == null || worker.IsCritical) { reason = "worker can't go"; return false; }

        if (StormManager.Instance != null && StormManager.Instance.IsActive)
        {
            reason = "solar storm";
            return false;
        }

        var rm = ResourceManager.Instance;
        if (rm != null)
        {
            if (rm.GetValue(ResourceType.Energy) - mission.energyCost < minEnergyAfterTrip)
            {
                reason = "not enough energy";
                return false;
            }
            if (rm.GetValue(ResourceType.Oxygen) - mission.oxygenCost < minOxygenAfterTrip)
            {
                reason = "not enough oxygen";
                return false;
            }
        }

        return true;
    }

    public bool IsOnMission(Worker worker) => IsBusy && worker != null && Driver == worker;

    public Mission Find(string missionId) => missions.Find(m => m.id == missionId);

    void Complete()
    {
        var mission = CurrentMission;
        var worker = Driver;

        Driver = null;
        CurrentMission = null;
        HoursLeft = hoursTotal = 0f;
        unlocked.Remove(mission.id);
        completed.Add(mission.id);

        Debug.Log($"{worker.DisplayName} completed mission: {mission.displayName}");
        OnMissionCompleted?.Invoke(mission, worker);
    }
}