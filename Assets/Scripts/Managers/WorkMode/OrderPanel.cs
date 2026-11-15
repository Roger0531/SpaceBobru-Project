using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OrderPanel : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] GameObject panel;
    [SerializeField] Button confirmButton;

    [Header("Heroes")]
    [Tooltip("Три іконки героїв")]
    [SerializeField] HeroSlot[] heroSlots;
    [SerializeField] Button prevButton;
    [SerializeField] Button nextButton;
    [SerializeField] TMP_Text heroNameLabel;

    [Header("Zones of the selected hero")]
    [SerializeField] Transform zonesParent;
    [SerializeField] ZoneButton zoneButtonPrefab;

    readonly List<Worker> heroes = new();
    readonly HashSet<Worker> available = new();
    readonly Dictionary<Worker, WorkZone> pending = new();   // вибір кожного героя
    readonly List<ZoneButton> zoneButtons = new();

    int selected = -1;
    WorkManager wm;

    void Start()
    {
        wm = WorkManager.Instance;
        if (wm == null) { Debug.LogWarning("OrdersPanel: WorkManager not found"); return; }

        wm.OnOrdersOpened += Open;
        wm.OnOrdersClosed += Hide;

        confirmButton.onClick.AddListener(Confirm);
        prevButton.onClick.AddListener(() => Step(-1));
        nextButton.onClick.AddListener(() => Step(1));

        panel.SetActive(false);
    }

    void OnDestroy()
    {
        if (wm == null) return;
        wm.OnOrdersOpened -= Open;
        wm.OnOrdersClosed -= Hide;
    }

    // ---------- Відкриття / закриття ----------

    void Open(List<Worker> availableWorkers)
    {
        heroes.Clear();
        available.Clear();
        pending.Clear();

        heroes.AddRange(Worker.All);
        heroes.Sort((a, b) => a.Id.CompareTo(b.Id));
        if (heroes.Count > heroSlots.Length)
            heroes.RemoveRange(heroSlots.Length, heroes.Count - heroSlots.Length);

        foreach (var w in availableWorkers) available.Add(w);
        foreach (var h in heroes) pending[h] = h.CurrentZone;

        BuildZoneButtons();

        for (int i = 0; i < heroSlots.Length; i++)
        {
            if (i >= heroes.Count)
            {
                heroSlots[i].gameObject.SetActive(false);
                continue;
            }

            int index = i;
            heroSlots[i].gameObject.SetActive(true);
            heroSlots[i].Setup(heroes[i], available.Contains(heroes[i]), () => Select(index));
        }

        panel.SetActive(true);

        selected = -1;
        Select(heroes.FindIndex(h => available.Contains(h)));
    }

    void Hide()
    {
        panel.SetActive(false);
        ClearZoneButtons();
    }

    void Confirm()
    {
        foreach (var hero in heroes)
        {
            if (!available.Contains(hero)) continue;

            var zone = pending[hero];
            if (zone != null && zone != hero.CurrentZone)
                wm.TryGiveOrder(hero, zone.Id);
        }

        wm.CloseOrders();
    }

    // ---------- Вибір героя ----------

    void Select(int index)
    {
        // -1 = нікого не вибрано; недоступного героя вибрати не можна
        if (index >= 0 && (index >= heroes.Count || !available.Contains(heroes[index]))) return;

        selected = index;
        RefreshAll();
    }

    void Step(int dir)
    {
        if (heroes.Count == 0) return;

        for (int i = 1; i <= heroes.Count; i++)
        {
            int idx = ((selected + dir * i) % heroes.Count + heroes.Count) % heroes.Count;
            if (available.Contains(heroes[idx]))
            {
                Select(idx);
                return;
            }
        }
    }

    // ---------- Зони ----------

    void BuildZoneButtons()
    {
        ClearZoneButtons();

        var zones = new List<WorkZone>(WorkZone.All);
        zones.Sort((a, b) => a.Id.CompareTo(b.Id));

        foreach (var z in zones)
        {
            var zb = Instantiate(zoneButtonPrefab, zonesParent);
            zb.Setup(z, OnZoneClicked);
            zoneButtons.Add(zb);
        }
    }

    void ClearZoneButtons()
    {
        foreach (var zb in zoneButtons)
            if (zb != null) Destroy(zb.gameObject);
        zoneButtons.Clear();
    }

    void OnZoneClicked(WorkZone zone)
    {
        if (selected < 0) return;
        pending[heroes[selected]] = zone;
        RefreshAll();
    }

    /// <summary>Скільки людей займає зону, не рахуючи цього героя (за вибором у панелі або поточною зоною).</summary>
    int OccupiedByOthers(WorkZone zone, Worker hero)
    {
        int n = 0;
        foreach (var w in Worker.All)
        {
            if (w == hero) continue;
            WorkZone z = pending.TryGetValue(w, out var p) ? p : w.CurrentZone;
            if (z == zone) n++;
        }
        return n;
    }

    // ---------- Оновлення інтерфейсу ----------

    void RefreshAll()
    {
        for (int i = 0; i < heroes.Count; i++)
        {
            heroSlots[i].SetSelected(i == selected);

            var z = pending[heroes[i]];
            heroSlots[i].SetZoneLabel(z != null ? z.DisplayName : "—");
        }

        Worker hero = selected >= 0 ? heroes[selected] : null;

        if (heroNameLabel != null)
            heroNameLabel.text = hero != null ? $"{hero.DisplayName} ({hero.Role})" : "—";

        bool canStep = available.Count > 1;
        prevButton.interactable = canStep;
        nextButton.interactable = canStep;

        foreach (var zb in zoneButtons)
        {
            if (hero == null)
            {
                zb.Refresh(false, 0, false);
                continue;
            }

            bool isSelected = pending[hero] == zb.Zone;
            int others = OccupiedByOthers(zb.Zone, hero);
            bool hasRoom = others < zb.Zone.MaxWorkers;

            zb.Refresh(isSelected, others + (isSelected ? 1 : 0), isSelected || hasRoom);
        }
    }
}