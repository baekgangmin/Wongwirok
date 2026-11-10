using System.Collections;
using UnityEngine;

[RequireComponent(typeof(EnemyHealth))]
public class EnemyRangedAttack : MonoBehaviour, IEnemyAttack
{
    [Header("Attack Timing")]
    [SerializeField] private float attackRange = 8f;
    [SerializeField] private float telegraphDuration = 0.5f;
    [SerializeField] private float cooldownDuration = 1.5f;

    [Header("Projectile")]
    [SerializeField] private float projectileSpeed = 8f;
    [SerializeField] private float projectileDamage = 8f;

    [Header("Projectile Visual")]
    [Tooltip("날아가는 불덩이 모양 (비워두면 기본 공)")]
    [SerializeField] private GameObject projectileVisualPrefab;
    [Tooltip("맞거나 땅에 닿았을 때 터지는 효과")]
    [SerializeField] private ParticleSystem impactEffectPrefab;
    [SerializeField] private Color projectileColor = new Color(1f, 0.45f, 0.1f, 1f);

    [Header("Colors")]
    [SerializeField] private Color telegraphColor = new Color(1f, 0.3f, 0f);

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Renderer targetRenderer;

    public bool IsBusy => isBusy;
    public float AttackRange => attackRange;

    private EnemyHealth health;
    private Color baseColor;
    private bool isBusy;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();
        if (targetRenderer != null)
            baseColor = targetRenderer.material.color;

        if (player == null)
        {
            GameObject playerObject = GameObject.Find("Player");
            if (playerObject != null)
                player = playerObject.transform;
        }
    }

    private void Update()
    {
        if (health.IsDead || isBusy || player == null)
            return;

        if (Vector3.Distance(transform.position, player.position) <= attackRange)
        {
            isBusy = true;
            StartCoroutine(AttackRoutine());
        }
    }

    private IEnumerator AttackRoutine()
    {
        // 등불처럼 떠다니는 효과가 있으면: 몸을 부풀리고 불빛을 키우는 연출 / 없으면 기존처럼 색 변경
        WillOWispFloatEffect floatEffect = GetComponent<WillOWispFloatEffect>();
        if (floatEffect != null)
        {
            float t = 0f;
            while (t < telegraphDuration)
            {
                if (health.IsDead)
                {
                    floatEffect.Charge = 0f;
                    yield break;
                }
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / telegraphDuration);
                floatEffect.Charge = k * k;   // 점점 빠르게 차오름
                yield return null;
            }
        }
        else
        {
            SetColor(telegraphColor);
            yield return new WaitForSeconds(telegraphDuration);
        }

        FireProjectile();

        if (floatEffect != null)
            floatEffect.Recoil();
        else
            SetColor(baseColor);
        yield return new WaitForSeconds(cooldownDuration);

        isBusy = false;
    }

    private void FireProjectile()
    {
        if (player == null || health.IsDead)
            return;

        Vector3 spawnPosition = transform.position + Vector3.up * 1f;
        Vector3 direction = (player.position - spawnPosition).normalized;

        GameObject projectileObject = new GameObject("EnemyProjectile");
        projectileObject.transform.position = spawnPosition;

        EnemyProjectile projectile = projectileObject.AddComponent<EnemyProjectile>();
        projectile.Launch(direction, projectileSpeed, projectileDamage, projectileVisualPrefab, impactEffectPrefab, projectileColor, transform);
    }

    private void SetColor(Color color)
    {
        if (targetRenderer != null)
            targetRenderer.material.color = color;
    }
}
