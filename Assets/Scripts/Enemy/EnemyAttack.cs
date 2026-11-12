using System.Collections;
using UnityEngine;

[RequireComponent(typeof(EnemyHealth))]
public class EnemyAttack : MonoBehaviour, IEnemyAttack
{
    [Header("Attack Timing")]
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float telegraphDuration = 0.6f;
    [SerializeField] private float activeDuration = 0.2f;
    [SerializeField] private float recoveryDuration = 0.8f;
    [SerializeField] private float staggerDuration = 1.2f;
    [Header("할퀴는 범위 (피해가 들어가는 순간 이 안에 있어야 맞음)")]
    [Tooltip("할퀴는 거리 (공격을 시작하는 사거리보다 조금 길게)")]
    [SerializeField] private float hitRange = 2.3f;
    [Tooltip("앞쪽 부채꼴 각도 (전체, 도)")]
    [SerializeField] private float hitAngle = 100f;
    [Tooltip("공격이 끝난 뒤 다음 공격까지 쉬는 시간 (이 틈에 맞으면 움찔한다)")]
    [SerializeField] private float attackCooldown = 0.8f;

    [Header("On-Hit Effect")]
    [SerializeField] private bool slowsPlayerInsteadOfDamage = false;
    [SerializeField] private float slowMultiplier = 0.5f;
    [SerializeField] private float slowDuration = 2f;

    [Header("Colors")]
    [SerializeField] private Color telegraphColor = new Color(1f, 0.5f, 0f);
    [SerializeField] private Color parriedColor = Color.cyan;

    [Header("공격 예고 효과 (손에 기운이 모임)")]
    [Tooltip("비워두면 예전처럼 몸 색이 바뀌는 예고를 쓴다")]
    [SerializeField] private ParticleSystem telegraphEffectPrefab;
    [SerializeField] private Color telegraphEffectColor = new Color(1f, 0.1f, 0.05f, 1f);
    [SerializeField] private HumanBodyBones telegraphHand = HumanBodyBones.RightHand;

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Renderer targetRenderer;

    public bool IsAttackActive { get; private set; }
    public bool IsBusy => isBusy;
    public float AttackRange => attackRange;

    private EnemyHealth health;
    private PlayerMovement playerMovement;
    private PlayerHealth playerHealth;
    private Color baseColor;
    private RendererTint tint;
    private Coroutine attackRoutine;
    private bool isBusy;
    private float cooldownTimer;
    private EnemyAnimationDriver animationDriver;
    private Animator animator;
    private ParticleSystem activeTelegraph;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        animationDriver = GetComponent<EnemyAnimationDriver>();
        animator = GetComponentInChildren<Animator>();
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();
        if (targetRenderer != null)
            { tint = new RendererTint(targetRenderer); baseColor = tint.BaseColor; }

        if (player == null)
        {
            GameObject playerObject = GameObject.Find("Player");
            if (playerObject != null)
                player = playerObject.transform;
        }
        if (player != null)
        {
            playerMovement = player.GetComponent<PlayerMovement>();
            playerHealth = player.GetComponent<PlayerHealth>();
        }
    }

    private void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        if (health.IsDead || isBusy || player == null || cooldownTimer > 0f)
            return;

        if (animationDriver != null && animationDriver.IsReacting)
            return;   // 움찔하는 동안은 공격하지 않음

        if (Vector3.Distance(transform.position, player.position) <= attackRange)
        {
            isBusy = true;
            attackRoutine = StartCoroutine(AttackRoutine());
        }
    }

    public bool AttemptParry()
    {
        if (!IsAttackActive)
            return false;

        IsAttackActive = false;
        if (attackRoutine != null)
            StopCoroutine(attackRoutine);
        StopTelegraphEffect();

        StartCoroutine(StaggerRoutine());
        return true;
    }

    private IEnumerator AttackRoutine()
    {
        // 플레이어 쪽을 보고 공격 동작 시작
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
        if (animationDriver != null)
            animationDriver.PlayAttack();

        if (telegraphEffectPrefab != null)
            StartTelegraphEffect();
        else
            SetColor(telegraphColor);
        yield return new WaitForSeconds(telegraphDuration);
        StopTelegraphEffect();

        IsAttackActive = true;
        yield return new WaitForSeconds(activeDuration);
        IsAttackActive = false;

        if (health.IsDead)
        {
            // 공격 도중 죽으면 피해를 주지 않는다
            SetColor(baseColor);
            yield break;
        }

        bool playerInvulnerable = playerMovement != null && playerMovement.IsInvulnerable;
        if (!PlayerInHitArc())
        {
            // 예고를 보고 물러나거나 옆으로 비켜서 할퀴는 범위를 벗어남
        }
        else if (playerInvulnerable)
        {
            Debug.Log($"{name}: 공격이 회피로 빗나감");
        }
        else if (slowsPlayerInsteadOfDamage)
        {
            playerMovement?.ApplySlow(slowMultiplier, slowDuration);
        }
        else
        {
            playerHealth?.TakeDamage(attackDamage);
        }

        SetColor(baseColor);
        yield return new WaitForSeconds(recoveryDuration);

        isBusy = false;
        cooldownTimer = attackCooldown;
    }

    // 할퀴는 범위: 공격을 시작할 때 바라본 방향 기준 앞쪽 부채꼴
    private bool PlayerInHitArc()
    {
        if (player == null)
            return false;
        Vector3 to = player.position - transform.position;
        to.y = 0f;
        if (to.magnitude > hitRange)
            return false;
        if (to.sqrMagnitude < 0.01f)
            return true;
        return Vector3.Angle(transform.forward, to) <= hitAngle * 0.5f;
    }

    private void OnDrawGizmosSelected()
    {
        // 씬 화면에서 잡몹을 선택하면 할퀴는 범위가 보임 (게임 화면에는 안 나옴)
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.9f);
        Vector3 c = transform.position + Vector3.up * 0.05f;
        int seg = 20;
        Vector3 prev = c;
        for (int i = 0; i <= seg; i++)
        {
            float a = -hitAngle * 0.5f + hitAngle * i / seg;
            Vector3 p = c + Quaternion.Euler(0f, a, 0f) * transform.forward * hitRange;
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
        Gizmos.DrawLine(prev, c);
    }

    private void StartTelegraphEffect()
    {
        Transform hand = animator != null && animator.isHuman ? animator.GetBoneTransform(telegraphHand) : null;
        Transform parent = hand != null ? hand : transform;
        activeTelegraph = Instantiate(telegraphEffectPrefab, parent);
        activeTelegraph.transform.localPosition = Vector3.zero;
        activeTelegraph.transform.localRotation = Quaternion.identity;
        // 손 뼈대의 크기에 영향 받지 않도록 월드 크기 1로 맞춤
        Vector3 ls = parent.lossyScale;
        activeTelegraph.transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, ls.x), 1f / Mathf.Max(0.0001f, ls.y), 1f / Mathf.Max(0.0001f, ls.z));
        foreach (ParticleSystem ps in activeTelegraph.GetComponentsInChildren<ParticleSystem>())
        {
            var main = ps.main;
            main.startColor = telegraphEffectColor;
        }
        activeTelegraph.Play(true);
    }

    private void StopTelegraphEffect()
    {
        if (activeTelegraph == null)
            return;
        activeTelegraph.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Destroy(activeTelegraph.gameObject, 0.6f);
        activeTelegraph = null;
    }

    private void OnDisable()
    {
        StopTelegraphEffect();
    }

    private IEnumerator StaggerRoutine()
    {
        SetColor(parriedColor);
        if (animationDriver != null)
            animationDriver.PlayHit(true);
        Debug.Log($"{name}: 패링 당함!");
        yield return new WaitForSeconds(staggerDuration);

        SetColor(baseColor);
        isBusy = false;
        cooldownTimer = attackCooldown;
    }

    private void SetColor(Color color)
    {
        if (targetRenderer != null)
            tint?.Set(color);
    }
}
