using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class OrderRow : MonoBehaviour
{
    [SerializeField] TMP_Text nameLabel;
    [SerializeField] TMP_Dropdown zoneDropdown;

    List<WorkZone> zones;
    List<Mission> missions;

    public Worker Worker { get; private set; }

    public WorkZone SelectedZone
    {
        get
        {
            int i = zoneDropdown.value;
            return zones != null && i < zones.Count ? zones[i] : null;
        }
    }

    public Mission SelectedMission
    {
        get
        {
            if (missions == null || zones == null) return null;
            int i = zoneDropdown.value - zones.Count;
            return i >= 0 && i < missions.Count ? missions[i] : null;
        }
    }

    public void Setup(Worker worker, List<WorkZone> zoneList, List<Mission> missionList = null)
    {
        Worker = worker;
        zones = new List<WorkZone>(zoneList);
        missions = missionList != null ? new List<Mission>(missionList) : new List<Mission>();
        nameLabel.text = $"{worker.DisplayName} ({worker.Role})";

        var options = new List<string>();
        int selected = 0;
        for (int i = 0; i < zones.Count; i++)
        {
            options.Add(zones[i].DisplayName);
            if (zones[i] == worker.CurrentZone) selected = i;
        }

        foreach (var m in missions)
            options.Add($"Rover: {m.displayName} ({m.durationHours}h)");

        zoneDropdown.ClearOptions();
        zoneDropdown.AddOptions(options);
        zoneDropdown.SetValueWithoutNotify(selected);
        zoneDropdown.RefreshShownValue();
    }
}