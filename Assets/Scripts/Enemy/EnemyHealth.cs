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
            SpawnDeathInk();
            OnDeath?.Invoke();
        }
    }

    // 쓰러질 때 몸 크기만큼 먹 연기가 피어오름 (적마다 넣지 않게 Resources 에서 한 장 공유)
    private static Texture2D deathInk;
    private void SpawnDeathInk()
    {
        if (deathInk == null) deathInk = Resources.Load<Texture2D>("FX/FX_GhostDeath");
        var r = GetComponentInChildren<Renderer>();
        if (deathInk == null || r == null)
            return;
        Bounds b = r.bounds;
        InkFxSprite.Spawn(deathInk, b.center, Mathf.Clamp(b.size.y * 1.3f, 1.2f, 6f), 0f, UnityEngine.Random.value < 0.5f, 0.9f, 0.3f, Color.white);
    }

    [ContextMenu("Test: Take 10 Damage")]
    private void TestTakeDamage()
    {
        TakeDamage(10f);
    }
}
