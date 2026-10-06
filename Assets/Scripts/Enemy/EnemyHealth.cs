using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 50f;

    public event Action<float> OnDamaged;
    public event Action OnDeath;

    public bool IsDead { get; private set; }

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
