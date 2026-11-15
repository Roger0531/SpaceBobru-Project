using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class OrderRow: MonoBehaviour
{
    [SerializeField] TMP_Text nameLabel;
    [SerializeField] TMP_Dropdown zoneDropdown;

    List<WorkZone> zones;

    public Worker Worker { get; private set; }

    public WorkZone SelectedZone =>
        zones != null && zones.Count > 0 ? zones[zoneDropdown.value] : null;

    public void Setup(Worker worker, List<WorkZone> zoneList)
    {
        Worker = worker;
        zones = new List<WorkZone>(zoneList);
        nameLabel.text = $"{worker.DisplayName} ({worker.Role})";

        var options = new List<string>();
        int selected = 0;
        for (int i = 0; i < zones.Count; i++)
        {
            options.Add(zones[i].DisplayName);
            if (zones[i] == worker.CurrentZone) selected = i;
        }

        zoneDropdown.ClearOptions();
        zoneDropdown.AddOptions(options);
        zoneDropdown.SetValueWithoutNotify(selected);
        zoneDropdown.RefreshShownValue();
    }
}
