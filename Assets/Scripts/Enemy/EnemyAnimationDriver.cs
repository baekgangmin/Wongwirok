using UnityEngine;

// 잡몹 애니메이션 연결: 이동 속도 → 걷기, 공격 → 공격 동작, 맞음 → 움찔(무작위) + 살짝 밀려남, 죽음 → 죽는 동작.
[RequireComponent(typeof(EnemyHealth))]
public class EnemyAnimationDriver : MonoBehaviour
{
    [SerializeField] private Animator animator;

    [Header("이동")]
    [Tooltip("이 속도(초당 m)로 움직일 때 걷기 동작이 100%")]
    [SerializeField] private float fullWalkSpeed = 1.5f;

    [Header("맞을 때 움찔")]
    [SerializeField] private int hitVariants = 2;
    [Tooltip("움찔하는 동안 추적·공격을 멈추는 시간")]
    [SerializeField] private float hitReactTime = 0.6f;
    [SerializeField] private float knockbackDistance = 0.35f;
    [SerializeField] private float knockbackTime = 0.15f;

    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int AttackId = Animator.StringToHash("Attack");
    private static readonly int HitId = Animator.StringToHash("Hit");
    private static readonly int HitIndexId = Animator.StringToHash("HitIndex");
    private static readonly int DeadId = Animator.StringToHash("Dead");

    private EnemyHealth health;
    private IEnemyAttack enemyAttack;
    private Transform player;
    private Vector3 lastPosition;
    private float reactTimer;
    private float knockbackTimer;
    private Vector3 knockbackVelocity;

    public bool IsReacting => reactTimer > 0f;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        enemyAttack = GetComponent<IEnemyAttack>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        GameObject p = GameObject.Find("Player");
        if (p != null)
            player = p.transform;
        lastPosition = transform.position;

        health.OnDamaged += HandleDamaged;
        health.OnDeath += HandleDeath;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
            health.OnDeath -= HandleDeath;
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        if (knockbackTimer > 0f)
        {
            knockbackTimer -= dt;
            transform.position += knockbackVelocity * dt;
        }
        if (reactTimer > 0f)
            reactTimer -= dt;

        Vector3 delta = transform.position - lastPosition;
        delta.y = 0f;
        lastPosition = transform.position;
        float speed = knockbackTimer > 0f ? 0f : delta.magnitude / dt;   // 밀려나는 건 걷기로 치지 않음

        if (animator != null)
            animator.SetFloat(SpeedId, fullWalkSpeed > 0f ? speed / fullWalkSpeed : 0f, 0.1f, dt);
    }

    public void PlayAttack()
    {
        if (animator != null && !health.IsDead)
            animator.SetTrigger(AttackId);
    }

    // 패링 당했을 때 등 외부에서 움찔을 강제로 재생
    public void PlayHit(bool knockback)
    {
        if (animator == null || health.IsDead)
            return;
        animator.SetInteger(HitIndexId, Random.Range(0, Mathf.Max(1, hitVariants)));
        animator.SetTrigger(HitId);
        reactTimer = hitReactTime;

        if (knockback && player != null && knockbackTime > 0f)
        {
            Vector3 away = transform.position - player.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.0001f)
            {
                knockbackVelocity = away.normalized * (knockbackDistance / knockbackTime);
                knockbackTimer = knockbackTime;
            }
        }
    }

    private void HandleDamaged(float amount)
    {
        if (health.IsDead)
            return;
        // 공격하는 중에는 움찔하지 않고 공격을 끝까지 한다 (맞은 번쩍임·불꽃은 그대로)
        if (enemyAttack != null && enemyAttack.IsBusy)
            return;
        PlayHit(true);
    }

    private void HandleDeath()
    {
        reactTimer = 0f;
        knockbackTimer = 0f;
        if (animator != null)
        {
            animator.SetFloat(SpeedId, 0f);
            animator.SetTrigger(DeadId);
        }
    }
}
