using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OrderPanel : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] Transform rowsParent;
    [SerializeField] OrderRow rowPrefab;
    [SerializeField] Button confirmButton;

    readonly List<OrderRow> rows = new();
    WorkManager wm;

    void Start()
    {
        wm = WorkManager.Instance;
        if (wm == null) { Debug.LogWarning("OrdersPanel: WorkManager not found"); return; }

        wm.OnOrdersOpened += Open;
        wm.OnOrdersClosed += Hide;
        confirmButton.onClick.AddListener(Confirm);

        panel.SetActive(false);
    }

    void OnDestroy()
    {
        if (wm == null) return;
        wm.OnOrdersOpened -= Open;
        wm.OnOrdersClosed -= Hide;
    }

    void Open(List<Worker> workers)
    {
        ClearRows();

        var missions = Rover.Instance != null
            ? Rover.Instance.GetAvailableMissions()
            : null;

        foreach (var w in workers)
        {
            var row = Instantiate(rowPrefab, rowsParent);
            row.Setup(w, WorkZone.All, missions);
            rows.Add(row);
        }

        panel.SetActive(true);
    }

    void Confirm()
    {
        foreach (var row in rows)
        {
            var mission = row.SelectedMission;
            if (mission != null)
            {
                if (Rover.Instance != null)
                    Rover.Instance.TrySend(row.Worker, mission.id);
                continue;
            }

            var zone = row.SelectedZone;
            if (zone != null && zone != row.Worker.CurrentZone)
                wm.TryGiveOrder(row.Worker, zone.Id);
        }

        wm.CloseOrders();
    }

    void Hide()
    {
        panel.SetActive(false);
        ClearRows();
    }

    void ClearRows()
    {
        foreach (var r in rows)
            if (r != null) Destroy(r.gameObject);
        rows.Clear();
    }

}