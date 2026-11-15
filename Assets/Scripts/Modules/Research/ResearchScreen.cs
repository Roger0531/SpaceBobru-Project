using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResearchScreen:MonoBehaviour
{
    [Header("Screen")]
    [Tooltip("Усе вікно; вмикається й вимикається. Сам скрипт має висіти на об'єкті, що завжди активний.")]
    [SerializeField] GameObject root;
    [SerializeField] Button confirmButton;
    [Tooltip("Необов'язково: «Picked: 1/3»")]
    [SerializeField] TMP_Text progressLabel;

    [Header("Heroes")]
    [SerializeField] HeroSlot[] heroSlots;
    [SerializeField] Button prevButton;
    [SerializeField] Button nextButton;
    [SerializeField] TMP_Text heroNameLabel;

    readonly List<Worker> heroes = new();
    readonly Dictionary<Worker, ResearchHext> picks = new();   // кожен герой обирає одну соту

    Action<Dictionary<Worker, ResearchHext>> onConfirm;
    int selected = -1;

    public bool IsOpen => root != null && root.activeSelf;

    void Start()
    {
        confirmButton.onClick.AddListener(Confirm);
        prevButton.onClick.AddListener(() => Step(-1));
        nextButton.onClick.AddListener(() => Step(1));

        ResearchHext.AnyClicked += OnHexClicked;
        root.SetActive(false);
    }

    void OnDestroy()
    {
        ResearchHext.AnyClicked -= OnHexClicked;
    }

    // ---------- Відкриття / закриття ----------

    public void Open(IEnumerable<Worker> researchers,
                     Action<Dictionary<Worker, ResearchHext>> confirmed)
    {
        onConfirm = confirmed;

        heroes.Clear();
        picks.Clear();
        heroes.AddRange(researchers);
        heroes.Sort((a, b) => a.Id.CompareTo(b.Id));

        for (int i = 0; i < heroSlots.Length; i++)
        {
            if (i >= heroes.Count)
            {
                heroSlots[i].gameObject.SetActive(false);
                continue;
            }

            int index = i;
            heroSlots[i].gameObject.SetActive(true);
            heroSlots[i].Setup(heroes[i], true, () => Select(index));
        }

        root.SetActive(true);

        selected = heroes.Count > 0 ? 0 : -1;
        Refresh();
    }

    public void Close() => root.SetActive(false);

    void Confirm()
    {
        if (!confirmButton.interactable) return;

        var result = new Dictionary<Worker, ResearchHext>(picks);
        var callback = onConfirm;
        Close();
        callback?.Invoke(result);
    }

    // ---------- Вибір героя ----------

    void Select(int index)
    {
        selected = index;
        Refresh();
    }

    void Step(int dir)
    {
        if (heroes.Count == 0) return;
        selected = ((selected + dir) % heroes.Count + heroes.Count) % heroes.Count;
        Refresh();
    }

    // ---------- Клік по соті ----------

    void OnHexClicked(ResearchHext hex)
    {
        if (!IsOpen || selected < 0) return;

        var tree = ResearchTree.Instance;
        var hero = heroes[selected];

        // доступне й встигає до відбою саме для цього героя
        if (tree == null || !tree.IsAvailableFor(hero, hex)) return;

        // зайнято іншим героєм
        foreach (var kv in picks)
            if (kv.Key != hero && kv.Value == hex) return;

        // повторний клік скасовує вибір, клік по іншій соті замінює його
        if (picks.TryGetValue(hero, out var current) && current == hex)
            picks.Remove(hero);
        else
            picks[hero] = hex;

        Refresh();
    }

    // ---------- Скільки виборів реально можливо ----------

    /// <summary>Найбільша кількість героїв, яким можна підібрати різні досліджень, що вони встигають до відбою.</summary>
    int MaxMatching(ResearchTree tree)
    {
        var matchOfHex = new Dictionary<ResearchHext, int>();   // сота -> індекс героя
        int result = 0;

        for (int i = 0; i < heroes.Count; i++)
        {
            var visited = new HashSet<ResearchHext>();
            if (TryAugment(i, tree, matchOfHex, visited)) result++;
        }
        return result;
    }

    bool TryAugment(int hero, ResearchTree tree,
                    Dictionary<ResearchHext, int> matchOfHex, HashSet<ResearchHext> visited)
    {
        foreach (var hex in tree.Hexes)
        {
            if (!tree.IsAvailableFor(heroes[hero], hex) || !visited.Add(hex)) continue;

            if (!matchOfHex.TryGetValue(hex, out int other) ||
                TryAugment(other, tree, matchOfHex, visited))
            {
                matchOfHex[hex] = hero;
                return true;
            }
        }
        return false;
    }

    // ---------- Оновлення інтерфейсу ----------

    void Refresh()
    {
        Worker hero = selected >= 0 ? heroes[selected] : null;

        for (int i = 0; i < heroes.Count; i++)
        {
            heroSlots[i].SetSelected(i == selected);
            heroSlots[i].SetOrdered(picks.ContainsKey(heroes[i]));
        }

        if (heroNameLabel != null)
            heroNameLabel.text = hero != null ? $"{hero.DisplayName} ({hero.Role})" : "—";

        bool canStep = heroes.Count > 1;
        prevButton.interactable = canStep;
        nextButton.interactable = canStep;

        ResearchHext mine = null;
        if (hero != null) picks.TryGetValue(hero, out mine);

        var tree = ResearchTree.Instance;
        int required = heroes.Count;

        if (tree != null)
        {
            // стільки виборів реально можливо, щоб гравець не застряг на екрані без виходу
            required = MaxMatching(tree);

            foreach (var hex in tree.Hexes)
            {
                Worker owner = null;
                ResearchHext.State state;

                if (tree.IsDone(hex)) state = ResearchHext.State.Done;
                else if (tree.IsClaimed(hex))
                {
                    state = ResearchHext.State.InProgress;
                    owner = tree.ClaimOwner(hex);
                }
                else if (!tree.PrerequisitesDone(hex)) state = ResearchHext.State.Locked;
                else if (hero != null && !tree.FitsBeforeBedtime(hero, hex)) state = ResearchHext.State.NoTime;
                else state = ResearchHext.State.Available;

                // хто обрав цю соту в цьому вікні
                Worker picker = null;
                foreach (var kv in picks)
                {
                    if (kv.Value == hex) { picker = kv.Key; break; }
                }
                if (picker != null) owner = picker;

                bool clickable = hero != null &&
                                 state == ResearchHext.State.Available &&
                                 (picker == null || picker == hero);

                hex.SetVisual(state, mine == hex, owner != null ? owner.Icon : null, clickable);
            }
        }

        confirmButton.interactable = picks.Count >= required;
        if (progressLabel != null) progressLabel.text = $"Picked: {picks.Count}/{required}";
    }
}
