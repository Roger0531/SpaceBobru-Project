using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OrderPanel : MonoBehaviour
{
    // власний список варіантів героя: вміст, кнопки та збережена позиція прокрутки
    class HeroList
    {
        public RectTransform content;
        public readonly List<ZoneButton> buttons = new();
        public float scroll = 1f;   // 1 = верх списку
    }

    [Header("Panel")]
    [SerializeField] GameObject panel;
    [SerializeField] Button confirmButton;
    [Tooltip("Необов'язково: «Накази: 2/3»")]
    [SerializeField] TMP_Text progressLabel;

    [Header("Heroes")]
    [Tooltip("Три іконки героїв")]
    [SerializeField] HeroSlot[] heroSlots;
    [SerializeField] Button prevButton;
    [SerializeField] Button nextButton;
    [SerializeField] TMP_Text heroNameLabel;

    [Header("Zones (scroll list, own list per hero)")]
    [SerializeField] ScrollRect zonesScroll;
    [SerializeField] ZoneButton zoneButtonPrefab;
    [SerializeField] float zoneSpacing = 6f;
    [SerializeField] float zonePadding = 8f;

    readonly List<Worker> heroes = new();
    readonly HashSet<Worker> available = new();
    readonly HashSet<Worker> ordered = new();                    // кому вже віддано наказ
    readonly Dictionary<Worker, ZoneOption> pending = new();     // обраний варіант
    readonly Dictionary<Worker, HeroList> lists = new();
    readonly List<ZoneOption> options = new();

    HeroList shown;
    int selected = -1;
    WorkManager wm;

    void Start()
    {
        wm = WorkManager.Instance;
        if (wm == null) { Debug.LogWarning("OrderPanel: WorkManager not found"); return; }

        wm.OnOrdersOpened += Open;
        wm.OnOrdersClosed += Hide;

        confirmButton.onClick.AddListener(Confirm);
        prevButton.onClick.AddListener(() => Step(-1));
        nextButton.onClick.AddListener(() => Step(1));

        // стандартний Content у Scroll View не потрібен: списки створюються для кожного героя
        if (zonesScroll != null && zonesScroll.content != null)
            zonesScroll.content.gameObject.SetActive(false);

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
        ordered.Clear();
        pending.Clear();
        ClearLists();

        heroes.AddRange(Worker.All);
        heroes.Sort((a, b) => a.Id.CompareTo(b.Id));
        if (heroes.Count > heroSlots.Length)
            heroes.RemoveRange(heroSlots.Length, heroes.Count - heroSlots.Length);

        foreach (var w in availableWorkers) available.Add(w);

        BuildOptions();
        BuildLists();

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
        ClearLists();
    }

    void Confirm()
    {
        if (!AllOrdered()) return;

        foreach (var hero in heroes)
        {
            if (!available.Contains(hero) || !ordered.Contains(hero)) continue;

            var o = pending[hero];
            if (o.zone != hero.CurrentZone || o.Preferred != hero.PreferredWork)
                wm.TryGiveOrder(hero, o.zone.Id, o.Preferred);
        }

        wm.CloseOrders();
    }

    /// <summary>Усі доступні герої отримали накази. Герої, яким нікуди йти, не блокують підтвердження.</summary>
    bool AllOrdered()
    {
        foreach (var h in heroes)
        {
            if (!available.Contains(h) || ordered.Contains(h)) continue;
            if (HasSelectableOption(h)) return false;
        }
        return true;
    }

    bool HasSelectableOption(Worker hero)
    {
        foreach (var o in options)
        {
            if (o.zone.GetOrderBlockReason(hero, o.work) == null &&
                OccupiedByOthers(o.zone, hero) < o.zone.MaxWorkers)
                return true;
        }
        return false;
    }

    // ---------- Вибір героя ----------

    void Select(int index)
    {
        // -1 = нікого не вибрано; недоступного героя вибрати не можна
        if (index >= 0 && (index >= heroes.Count || !available.Contains(heroes[index]))) return;

        selected = index;
        ShowList(index >= 0 ? heroes[index] : null);
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

    // ---------- Варіанти наказів ----------

    /// <summary>Варіанти: пошкоджена зона дає один варіант «Ремонт», зона без роботи дає недоступний варіант,
    /// науковий блок дає два (дослідження й лікування).</summary>
    void BuildOptions()
    {
        options.Clear();

        var zones = new List<WorkZone>(WorkZone.All);
        zones.Sort((a, b) => a.Id.CompareTo(b.Id));

        var works = new List<WorkType>();
        foreach (var z in zones)
        {
            if (z.IsDamaged)
            {
                options.Add(new ZoneOption(z, WorkType.Repair, false));
                continue;
            }

            z.GetOrderWorks(works);
            if (works.Count == 0)
            {
                options.Add(new ZoneOption(z, WorkType.None, false));
                continue;
            }

            foreach (var w in works)
                options.Add(new ZoneOption(z, w, works.Count > 1));
        }
    }

    /// <summary>Тут можна відфільтрувати варіанти для конкретного героя.</summary>
    bool IsOptionListed(Worker hero, ZoneOption option) => true;

    // ---------- Списки (власний у кожного героя) ----------

    void BuildLists()
    {
        foreach (var hero in heroes)
        {
            var list = new HeroList { content = CreateContent(hero.DisplayName) };

            foreach (var o in options)
            {
                if (!IsOptionListed(hero, o)) continue;

                var zb = Instantiate(zoneButtonPrefab, list.content);
                zb.Setup(o, OnZoneClicked);
                list.buttons.Add(zb);
            }

            list.content.gameObject.SetActive(false);
            lists[hero] = list;
        }
    }

    RectTransform CreateContent(string heroName)
    {
        var parent = zonesScroll.viewport != null
            ? zonesScroll.viewport
            : (RectTransform)zonesScroll.transform;

        var go = new GameObject($"Zones_{heroName}", typeof(RectTransform),
                                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        go.layer = parent.gameObject.layer;

        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        var layout = go.GetComponent<VerticalLayoutGroup>();
        layout.spacing = zoneSpacing;
        int pad = Mathf.RoundToInt(zonePadding);
        layout.padding = new RectOffset(pad, pad, pad, pad);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        go.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rt;
    }

    void ClearLists()
    {
        shown = null;
        if (zonesScroll != null) zonesScroll.content = null;

        foreach (var l in lists.Values)
            if (l.content != null) Destroy(l.content.gameObject);
        lists.Clear();
    }

    void ShowList(Worker hero)
    {
        HeroList target = null;
        if (hero != null) lists.TryGetValue(hero, out target);
        if (target == shown) return;

        // запам'ятовуємо позицію прокрутки попереднього списку
        if (shown != null && shown.content != null)
        {
            shown.scroll = zonesScroll.verticalNormalizedPosition;
            shown.content.gameObject.SetActive(false);
        }

        shown = target;
        if (target == null) return;

        target.content.gameObject.SetActive(true);
        zonesScroll.StopMovement();
        zonesScroll.content = target.content;

        LayoutRebuilder.ForceRebuildLayoutImmediate(target.content);
        Canvas.ForceUpdateCanvases();
        zonesScroll.verticalNormalizedPosition = target.scroll;
    }

    void OnZoneClicked(ZoneOption option)
    {
        if (selected < 0) return;

        var hero = heroes[selected];
        pending[hero] = option;
        ordered.Add(hero);
        RefreshAll();
    }

    /// <summary>Де герой буде після підтвердження: обрана зона або поточна, якщо наказу ще немає.</summary>
    WorkZone EffectiveZone(Worker w) =>
        ordered.Contains(w) && pending.TryGetValue(w, out var o) ? o.zone : w.CurrentZone;

    int OccupiedByOthers(WorkZone zone, Worker hero)
    {
        int n = 0;
        foreach (var w in Worker.All)
        {
            if (w == hero) continue;
            if (EffectiveZone(w) == zone) n++;
        }
        return n;
    }

    // ---------- Оновлення інтерфейсу ----------

    void RefreshAll()
    {
        int doneCount = 0, totalCount = 0;

        for (int i = 0; i < heroes.Count; i++)
        {
            var h = heroes[i];
            bool done = ordered.Contains(h);

            if (available.Contains(h))
            {
                totalCount++;
                if (done || !HasSelectableOption(h)) doneCount++;
            }

            heroSlots[i].SetSelected(i == selected);
            heroSlots[i].SetOrdered(done);
            heroSlots[i].ShowHp(h);
            heroSlots[i].SetZoneLabel(done && pending.TryGetValue(h, out var o) ? o.ShortLabel : "—");
        }

        Worker hero = selected >= 0 ? heroes[selected] : null;

        if (heroNameLabel != null)
            heroNameLabel.text = hero != null
                ? $"{hero.DisplayName} ({hero.Role})   HP {hero.Hp:0.#}/{hero.MaxHp:0.#}"
                : "—";

        bool canStep = available.Count > 1;
        prevButton.interactable = canStep;
        nextButton.interactable = canStep;

        if (hero != null && lists.TryGetValue(hero, out var list))
        {
            pending.TryGetValue(hero, out var chosen);
            bool heroOrdered = ordered.Contains(hero);

            foreach (var zb in list.buttons)
            {
                var opt = zb.Option;
                bool isSelected = heroOrdered && chosen == opt;
                int others = OccupiedByOthers(opt.zone, hero);
                bool hasRoom = others < opt.zone.MaxWorkers;

                string reason = opt.zone.GetOrderBlockReason(hero, opt.work);
                string note = reason;
                if (note == null && opt.work == WorkType.Heal)
                    note = $"needs {opt.zone.HealWorkersRequired} people";

                zb.Refresh(isSelected,
                           others + (isSelected ? 1 : 0),
                           isSelected || (reason == null && hasRoom),
                           hero.CurrentZone == opt.zone && hero.PreferredWork == opt.Preferred,
                           note);
            }
        }

        confirmButton.interactable = AllOrdered();
        if (progressLabel != null) progressLabel.text = $"Orders: {doneCount}/{totalCount}";
    }
}