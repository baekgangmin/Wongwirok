using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour, IHealth
{
    [SerializeField] private float maxHealth = 50f;

    public event Action<float> OnDamaged;
    public event Action OnDeath;

    public bool IsDead { get; private set; }
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    private float currentHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead)
            return;

        currentHealth -= amount;
        Debug.Log($"Wongwirok [진단]: {name}.TakeDamage({amount}) 호출됨, 남은 체력 {currentHealth}/{maxHealth}, OnDamaged 구독자 수 {OnDamaged?.GetInvocationList().Length ?? 0}");
        OnDamaged?.Invoke(amount);

        if (currentHealth <= 0f)
        {
            IsDead = true;
            OnDeath?.Invoke();
        }
    }

    [ContextMenu("Test: Take 10 Damage")]
    private void TestTakeDamage()
    {
        TakeDamage(10f);
    }
}
