using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private MonoBehaviour healthSource;

    private IHealth health;

    private void Awake()
    {
        if (healthSource is IHealth target)
            Subscribe(target);
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
            return;

        fillImage.fillAmount = Mathf.Clamp01(health.CurrentHealth / health.MaxHealth);
    }
}
