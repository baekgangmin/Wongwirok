using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour, IHealth
{
    [SerializeField] private float maxHealth = 50f;
    [Tooltip("쓰러지면 주는 양기 (최소~최대)")]
    [SerializeField] private Vector2Int yanggiDrop = new Vector2Int(3, 30);

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
        OnDamaged?.Invoke(amount);

        if (currentHealth <= 0f)
        {
            IsDead = true;
            if (yanggiDrop.y > 0)
                GameProgress.AddYanggi(UnityEngine.Random.Range(yanggiDrop.x, yanggiDrop.y + 1));
            OnDeath?.Invoke();
        }
    }

    [ContextMenu("Test: Take 10 Damage")]
    private void TestTakeDamage()
    {
        TakeDamage(10f);
    }
}
