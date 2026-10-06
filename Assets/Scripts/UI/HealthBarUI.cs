using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private MonoBehaviour healthSource;

    private IHealth health;

    private void Awake()
    {
        Debug.Log($"Wongwirok [진단]: {name}/HealthBarUI.Awake() healthSource={healthSource}, fillImage={fillImage}");
        if (healthSource is IHealth target)
            Subscribe(target);
        else
            Debug.LogWarning($"Wongwirok [진단]: {name}/HealthBarUI - healthSource가 비어있거나 IHealth가 아님");
    }

    private void Start()
    {
        Refresh();
    }

    public void Initialize(IHealth target)
    {
        Subscribe(target);
        Refresh();
    }

    private void Subscribe(IHealth target)
    {
        if (health != null)
        {
            health.OnDamaged -= HandleHealthChanged;
            health.OnDeath -= HandleHealthChanged;
        }

        health = target;

        if (health != null)
        {
            health.OnDamaged += HandleHealthChanged;
            health.OnDeath += HandleHealthChanged;
        }
    }

    private void HandleHealthChanged(float amount) => Refresh();
    private void HandleHealthChanged() => Refresh();

    private void Refresh()
    {
        if (fillImage == null || health == null || health.MaxHealth <= 0f)
        {
            Debug.LogWarning($"Wongwirok [진단]: {name}/HealthBarUI.Refresh() 중단 - fillImage={fillImage}, health={health}");
            return;
        }

        float ratio = Mathf.Clamp01(health.CurrentHealth / health.MaxHealth);
        Debug.Log($"Wongwirok [진단]: {name}/HealthBarUI.Refresh() fillAmount={ratio}");
        fillImage.fillAmount = ratio;
    }
}
