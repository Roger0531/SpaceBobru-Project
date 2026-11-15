using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DefaultExecutionOrder(100)]
public class EventManager : MonoBehaviour
{
    public static EventManager Instance { get; private set; }

    [Header("Дані")]
    [SerializeField] private List<EventInfo> events;

    [Header("Налаштування")]
    [SerializeField] private int eventIntervalHours = 4;
    [SerializeField, Range(0f, 1f)] private float blackoutChance = 0.1f;
    [SerializeField] private float panelDelay = 2f;

    [Header("Панель 1 (опис події)")]
    [SerializeField] private GameObject panel1;
    [SerializeField] private TextMeshProUGUI txtEventNamePanel1;
    [SerializeField] private TextMeshProUGUI txtEventDescriptionPanel1;
    [SerializeField] private Button btnOk;

    [Header("Панель 2 (вибір)")]
    [SerializeField] private GameObject panel2;
    [SerializeField] private TextMeshProUGUI txtEventNamePanel2;
    [SerializeField] private Button btnChoice1;
    [SerializeField] private TextMeshProUGUI txtChoice1;
    [SerializeField] private TextMeshProUGUI txtChoiceDescription1;
    [SerializeField] private Button btnChoice2;
    [SerializeField] private TextMeshProUGUI txtChoice2;
    [SerializeField] private TextMeshProUGUI txtChoiceDescription2;

    [Header("Перемикачі сторінок")]
    [SerializeField] private Button btnPageOne;
    [SerializeField] private Button btnPageTwo;

    [Header("Анімації")]
    [SerializeField] private Animator panel1Animator;   // аніматор panel1
    [SerializeField] private Animator[] buttonAnimators;   // аніматор panel2
    //[SerializeField] private Animator pageOneAnimator;  // аніматор кнопки сторінки 1
    //[SerializeField] private Animator pageTwoAnimator;

    static readonly int ClosableHash = Animator.StringToHash("closable");
    static readonly int PageHash = Animator.StringToHash("CurrentPage");
    private int currentPage = 1;

    // Підписуйтесь, щоб застосувати наслідки вибору (енергія, кисень тощо)
    public event System.Action<EventInfo, EventChoice> ChoiceSelected;

    // Колишній PanelsAreDead: активний ефект "база втрачає енергію"
    public bool PowerDrainActive { get; private set; }

    private bool busy;
    private int lastHandledTick = -1;
    private bool panel1Closable = true;

    // Чи ми самі поставили час на паузу під час вибору
    private bool pausedByChoice;

    public event System.Action<EventInfo> EventShown;
    public bool IsBusy => busy;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        panel1.SetActive(false);
        panel2.SetActive(false);
        btnOk.gameObject.SetActive(false);
        SetPageButtons(false);

        btnPageOne.onClick.AddListener(() => ShowPage(1));
        btnPageTwo.onClick.AddListener(() => ShowPage(2));
    }

    private void OnDestroy()
    {
        // Якщо менеджер знищили посеред вибору, не лишаємо гру на паузі
        ResumeTimeAfterChoice();
    }

    private void Update()
    {
        var time = TimeManager.Instance;
        if (time == null || busy) return;

        int tick = time.Day * 24 + time.Hour;
        if (time.Hour % eventIntervalHours != 0 || tick == lastHandledTick) return;

        lastHandledTick = tick;
        StartCoroutine(ProcessEvents(time.Day, time.Hour));
    }

    // ---------- Основний цикл ----------

    private IEnumerator ProcessEvents(int day, int hour)
    {
        busy = true;

        yield return TryBlackout();

        for (int i = 0; i < events.Count; i++)
        {
            EventInfo e = events[i];
            if (e.isCompleted || e.dayOccurs != day || e.hourOccurs != hour) continue;

            if (e.choices != null && e.choices.Count > 0)
                yield return ShowChoiceEvent(e);
            else
                yield return ShowSimpleEvent(e);
        }

        busy = false;
    }

    private IEnumerator TryBlackout()
    {
        if (PowerDrainActive || events.Count == 0) yield break;
        if (Random.value >= blackoutChance) yield break;

        PowerDrainActive = true;
        OpenPanel1(
            events[0].eventName,
            events[0].eventDescription +
            "<b>!!!Base takes 10% of energy every 4 hours and energy does not produce!!!</b>",
            closable: true);

        yield return new WaitForSeconds(panelDelay);
        yield return WaitForOk();
    }

    private IEnumerator ShowSimpleEvent(EventInfo e)
    {
        // Панель з кнопкою OK: можна закрити
        OpenPanel1(e.eventName, e.eventDescription, closable: true);
        EventShown?.Invoke(e);

        yield return new WaitForSeconds(panelDelay);
        yield return WaitForOk();
        e.isCompleted = true;
    }

    private IEnumerator ShowChoiceEvent(EventInfo e)
    {
        // Ігровий час стоїть, поки гравець не зробить вибір
        PauseTimeForChoice();
        EventShown?.Invoke(e);

        txtEventNamePanel2.text = e.eventName;

        EventChoice selected = null;
        SetupChoice(btnChoice1, txtChoice1, txtChoiceDescription1, GetChoice(e, 0), c => selected = c);
        SetupChoice(btnChoice2, txtChoice2, txtChoiceDescription2, GetChoice(e, 1), c => selected = c);

        // Панель без кнопки OK: закриється лише після вибору
        OpenPanel1(e.eventName, e.eventDescription, closable: false);
        ShowPage(1);

        // Затримка рахується в реальному часі, на паузу гри вона не впливає
        yield return new WaitForSeconds(panelDelay);
        SetPageButtons(true);

        yield return new WaitUntil(() => selected != null);

        e.isCompleted = true;
        Debug.Log($"Обрано: {selected.ChoiceText}");
        ChoiceSelected?.Invoke(e, selected);

        panel1.SetActive(false);
        panel2.SetActive(false);
        SetPageButtons(false);

        ResumeTimeAfterChoice();
    }

    // ---------- Пауза часу ----------

    private void PauseTimeForChoice()
    {
        var time = TimeManager.Instance;
        if (time == null || time.IsPaused) return; // вже на паузі (гравцем), нічого не чіпаємо

        time.Pause();
        pausedByChoice = true;
    }

    private void ResumeTimeAfterChoice()
    {
        if (!pausedByChoice) return;
        pausedByChoice = false;

        var time = TimeManager.Instance;
        if (time != null) time.Resume();
    }

    // ---------- Допоміжні методи ----------

    // closable = true  -> панель з кнопкою OK, яку можна закрити
    // closable = false -> без кнопки OK, інша анімація панелі
    private void OpenPanel1(string title, string description, bool closable)
    {
        txtEventNamePanel1.text = title;
        txtEventDescriptionPanel1.text = description;
        btnOk.gameObject.SetActive(false); // для closable її увімкне WaitForOk

        panel1Closable = closable;
        panel1.SetActive(true);
        ApplyPanelAnimation();
    }

    private IEnumerator WaitForOk()
    {
        bool clicked = false;
        UnityAction handler = () => clicked = true;

        btnOk.onClick.AddListener(handler);
        btnOk.gameObject.SetActive(true);

        yield return new WaitUntil(() => clicked);

        btnOk.onClick.RemoveListener(handler);
        btnOk.gameObject.SetActive(false);
        panel1.SetActive(false);
    }

    private void SetupChoice(Button button, TextMeshProUGUI title, TextMeshProUGUI desc,
                             EventChoice choice, System.Action<EventChoice> onSelected)
    {
        button.onClick.RemoveAllListeners();
        button.gameObject.SetActive(choice != null);
        if (choice == null) return;

        title.text = choice.ChoiceText;
        desc.text = BuildChoiceDescription(choice);
        button.onClick.AddListener(() => onSelected(choice));
    }

    private static EventChoice GetChoice(EventInfo e, int index) =>
        index < e.choices.Count ? e.choices[index] : null;

    private static string BuildChoiceDescription(EventChoice c)
    {
        var sb = new StringBuilder(c.ChoiceDescription);
        if (sb.Length > 0) sb.AppendLine();

        AppendCost(sb, c.energyCost, "% of energy");
        AppendCost(sb, c.oxygenCost, "% of oxygen");
        AppendCost(sb, c.foodCost, " food");
        AppendCost(sb, c.hpCost, " health of chosen team member");

        return sb.ToString();
    }

    private static void AppendCost(StringBuilder sb, double value, string suffix)
    {
        if (value != 0) sb.AppendLine($"-{value}{suffix}");
    }

    // ---------- Сторінки та анімації ----------

    private void ShowPage(int page)
    {
        currentPage = page;
        panel1.SetActive(page == 1);
        panel2.SetActive(page == 2);

        // Вимкнена і знову ввімкнена панель скидає аніматор, тому виставляємо параметри заново
        ApplyPanelAnimation();
        ApplyPageAnimation();
    }

    private void SetPageButtons(bool active)
    {
        btnPageOne.gameObject.SetActive(active);
        btnPageTwo.gameObject.SetActive(active);
        if (active) ApplyPageAnimation();
    }

    private void ApplyPanelAnimation()
    {
        if (panel1Animator != null && panel1Animator.isActiveAndEnabled)
            panel1Animator.SetBool(ClosableHash, panel1Closable);
    }

    private void ApplyPageAnimation()
    {
        if (buttonAnimators == null) return;

        foreach (var a in buttonAnimators)
            if (a != null && a.isActiveAndEnabled)
                a.SetInteger(PageHash, currentPage);
    }
}