using UnityEngine;
using UnityEngine.UI;

public class OxygenBar : MonoBehaviour
{
    [SerializeField] private Image fill;
    [Range(0, 100)][SerializeField] private float oxygen = 100f;

    public void SetOxygen(float percent)
    {
        oxygen = Mathf.Clamp(percent, 0f, 100f);
        UpdateBar();
    }

    public void AddOxygen(float delta) => SetOxygen(oxygen + delta);

    private void OnValidate() { if (fill != null) UpdateBar(); }

    private void UpdateBar()
    {
        fill.fillAmount = Mathf.Ceil(oxygen / 25f) / 4f;
    }
}