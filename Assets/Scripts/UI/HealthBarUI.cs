using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private Image fillImage;

    private IHealth health;

    public void Initialize(IHealth target)
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

        Refresh();
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
