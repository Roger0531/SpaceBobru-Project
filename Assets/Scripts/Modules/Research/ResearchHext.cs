using System;
using UnityEngine;
using UnityEngine.UI;

public class ResearchHext: MonoBehaviour
{
    public enum State { Available, Locked, Done, InProgress, NoTime }

    public static event Action<ResearchHext> AnyClicked;

    [Header("Research")]
    [Min(0.1f)] public float hours = 8f;
    [Tooltip("Дослідження, які мають бути завершені першими (перетягніть соти)")]
    public ResearchHext[] requires;

    [Header("Effect")]
    public ResearchEffectKind effect;
    public float value;
    [Tooltip("Ефекти не сумуються: береться найбільше значення серед завершених")]
    public bool takeBest;

    [Header("Visuals (усе необов'язково)")]
    [SerializeField] Button button;
    [SerializeField] GameObject lockedOverlay;
    [Tooltip("Показується, коли обраний герой не встигне завершити дослідження до відбою")]
    [SerializeField] GameObject noTimeOverlay;
    [SerializeField] GameObject doneMark;
    [SerializeField] GameObject inProgressMark;
    [SerializeField] GameObject selectedFrame;
    [Tooltip("Портрет героя, який обрав це дослідження або виконує його")]
    [SerializeField] Image pickedByIcon;
    [Tooltip("Клік лише по непрозорій частині спрайта (у спрайта має бути Read/Write Enabled)")]
    [SerializeField] bool preciseHitTest;

    void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(() => AnyClicked?.Invoke(this));

        if (preciseHitTest && button != null && button.targetGraphic is Image img)
            img.alphaHitTestMinimumThreshold = 0.5f;
    }

    public void SetVisual(State state, bool selected, Sprite pickedBy, bool clickable)
    {
        if (lockedOverlay != null) lockedOverlay.SetActive(state == State.Locked);
        if (noTimeOverlay != null) noTimeOverlay.SetActive(state == State.NoTime);
        if (doneMark != null) doneMark.SetActive(state == State.Done);
        if (inProgressMark != null) inProgressMark.SetActive(state == State.InProgress);
        if (selectedFrame != null) selectedFrame.SetActive(selected);

        if (pickedByIcon != null)
        {
            bool show = pickedBy != null &&
                        state != State.Done && state != State.Locked && state != State.NoTime;
            pickedByIcon.gameObject.SetActive(show);
            if (show) pickedByIcon.sprite = pickedBy;
        }

        if (button != null) button.interactable = clickable;
    }
}
