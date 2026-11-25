using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour, IHealth
{
    [SerializeField] private float maxHealth = 100f;

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
            OnDeath?.Invoke();
            var world = FindAnyObjectByType<WorldWeapon>();
            if (world != null && world.World == WorldKind.Ghost)
                StartCoroutine(ReturnToVillage());
        }
    }

    [SerializeField] private string humanScene = "HumanWorld_01";

    // 귀의 세계에서 쓰러지면 양기 절반을 잃고 밧줄 나무 밑에서 깨어난다
    private System.Collections.IEnumerator ReturnToVillage()
    {
        var move = GetComponent<PlayerMovement>();
        if (move != null) move.enabled = false;
        var attack = GetComponent<PlayerAttack>();
        if (attack != null) attack.enabled = false;
        yield return new WaitForSeconds(0.8f);
        GameProgress.AddYanggi(-GameProgress.Yanggi / 2);
        RopeSession.Active = false;   // GhostRopeReturn 이 씬 시작 때 돌아갈 자리를 채워 둔다
        WorldTransition.Begin(humanScene, RopeSession.HumanReturnPoint, RopeSession.HumanReturnYaw,
            new Color(0.25f, 0.02f, 0.02f, 1f), new Color(0.55f, 0.72f, 0.8f, 1f), false);
    }

    [ContextMenu("Test: Take 10 Damage")]
    private void TestTakeDamage()
    {
        TakeDamage(10f);
    }
}
