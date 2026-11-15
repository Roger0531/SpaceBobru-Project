using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OrderPanel : MonoBehaviour
{
    class HeroList
    {
        public RectTransform content;
        public readonly List<ZoneButton> buttons = new();
        public float scroll = 1f;   // 1 = верх списку
    }

    [Header("Panel")]
    [SerializeField] GameObject panel;
    [SerializeField] Button confirmButton;
    [Tooltip("Необов'язково: «Orders: 2/3»")]
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

    [Header("Research")]
    [SerializeField] ResearchScreen researchScreen;

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
        if (researchScreen != null && researchScreen.IsOpen) researchScreen.Close();
    }

    void Confirm()
    {
        if (!AllOrdered()) return;

        // герої, які їдуть досліджувати, спершу обирають дослідження
        var researchers = new List<Worker>();
        foreach (var hero in heroes)
        {
            if (!available.Contains(hero) || !ordered.Contains(hero)) continue;
            if (pending[hero].work == WorkType.Research) researchers.Add(hero);
        }

        if (researchers.Count > 0 && researchScreen != null && ResearchTree.Instance != null)
        {
            panel.SetActive(false);
            researchScreen.Open(researchers, picks => FinishOrders(picks));
            return;
        }

        FinishOrders(null);
    }

    /// <summary>Застосовує накази й відправляє обраних героїв на дослідження. picks = null, якщо досліджень немає.</summary>
    void FinishOrders(Dictionary<Worker, ResearchHext> picks)
    {
        var tree = ResearchTree.Instance;

        foreach (var hero in heroes)
        {
            if (!available.Contains(hero) || !ordered.Contains(hero)) continue;

            var o = pending[hero];

            // дослідник їде в науковий блок і лишається там на весь час дослідження
            if (o.work == WorkType.Research)
            {
                if (picks != null && tree != null &&
                    picks.TryGetValue(hero, out var hex) && tree.IsAvailable(hex))
                {
                    wm.TryGiveOrder(hero, o.zone);   // телепорт у науковий блок (до запуску дослідження)
                    tree.StartResearch(hex, hero);
                }
                continue;
            }

            if (o.zone != hero.CurrentZone || o.Preferred != hero.PreferredWork)
                wm.TryGiveOrder(hero, o.zone, o.Preferred);
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
            if (BlockReason(hero, o) != null) continue;
            if (o.work == WorkType.Research ||
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

    /// <summary>Пошкоджена зона дає один варіант «Repair», зона без роботи дає недоступний варіант,
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

    /// <summary>null, якщо варіант доступний; інакше причина блокування.</summary>
    string BlockReason(Worker hero, ZoneOption o)
    {
        string reason = o.zone.GetOrderBlockReason(hero, o.work);
        if (reason != null) return reason;

        if (o.work == WorkType.Research && ResearchTree.Instance != null)
        {
            var tree = ResearchTree.Instance;

            // досліджень не лишилось для всіх, хто вже обрав «Research»
            if (tree.AvailableCount() - OthersResearching(hero) <= 0)
                return "no research left";

            // жодного з доступних цей герой не встигне до відбою
            if (tree.AvailableCountFor(hero) == 0)
                return "not enough time before bedtime";
        }

        return null;
    }

    int OthersResearching(Worker hero)
    {
        int n = 0;
        foreach (var h in heroes)
        {
            if (h == hero || !ordered.Contains(h)) continue;
            if (pending.TryGetValue(h, out var o) && o.work == WorkType.Research) n++;
        }
        return n;
    }

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

    /// <summary>Де герой буде після підтвердження: обрана зона або поточна, якщо наказу ще немає.
    /// Дослідник від'їжджає й не займає місце в зоні.</summary>
    WorkZone EffectiveZone(Worker w)
    {
        if (ordered.Contains(w) && pending.TryGetValue(w, out var o))
            return o.work == WorkType.Research ? null : o.zone;
        return w.CurrentZone;
    }

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
            heroSlots[i].SetZoneLabel(done && pending.TryGetValue(h, out var o)
                ? o.ShortLabel
                : (ResearchTree.Instance != null && ResearchTree.Instance.HasClaim(h) ? "Researching"
                   : (wm.IsLockedOnRepair(h) ? "Repairing" : "—")));
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
                bool isResearch = opt.work == WorkType.Research;

                int others = OccupiedByOthers(opt.zone, hero);
                bool hasRoom = isResearch || others < opt.zone.MaxWorkers;

                string reason = BlockReason(hero, opt);
                string note = reason;

                if (note == null && opt.work == WorkType.Heal)
                    note = $"needs {opt.zone.HealWorkersRequired} people";
                if (note == null && isResearch && !isSelected)
                    note = "pick research next";

                zb.Refresh(isSelected,
                           isResearch ? -1 : others + (isSelected ? 1 : 0),
                           isSelected || (reason == null && hasRoom),
                           !isResearch && hero.CurrentZone == opt.zone && hero.PreferredWork == opt.Preferred,
                           note);
            }
        }

        confirmButton.interactable = AllOrdered();
        if (progressLabel != null) progressLabel.text = $"Orders: {doneCount}/{totalCount}";
    }
}