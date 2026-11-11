using UnityEngine;
using UnityEngine.InputSystem;

// 3단 연속 베기.
// - 공격 중에 다시 클릭하면 애니메이터가 현재 베기가 거의 끝날 때 다음 베기로 이어 준다.
// - 피해는 클릭 순간이 아니라 칼이 가장 빠르게 지나가는 순간(hitTime)에 들어간다.
public class PlayerAttack : MonoBehaviour
{
    [System.Serializable]
    private struct ComboHit
    {
        public string stateName;   // 애니메이터 상태 이름
        public float damageMultiplier;  // 검 공격력에 곱하는 배율
        public float hitTime;      // 동작 시작 후 피해가 들어가는 시간(초)
        public float trailFrom;    // 칼날 궤적이 나오기 시작하는 시간(초)
        public float trailTo;      // 칼날 궤적이 멈추는 시간(초)
    }

    [Header("Attack")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackRadius = 1f;
    [SerializeField] private float comboCooldown = 0.3f;
    [SerializeField] private LayerMask enemyLayers = ~0;

    [Header("장착한 검 (공격력, 궤적 색)")]
    [SerializeField] private SwordData equippedSword;
    [SerializeField] private float fallbackAttackPower = 10f;

    [Header("Combo (3단 연속 베기)")]
    [SerializeField] private ComboHit[] comboHits =
    {
        new ComboHit { stateName = "Attack1", damageMultiplier = 1f, hitTime = 0.41f },
        new ComboHit { stateName = "Attack2", damageMultiplier = 1f, hitTime = 0.22f },
        new ComboHit { stateName = "Attack3", damageMultiplier = 2f, hitTime = 0.38f },
    };

    public SwordData EquippedSword => equippedSword;
    private float AttackPower => equippedSword != null ? equippedSword.attackPower : fallbackAttackPower;

    // 다른 검을 장착한다 (마을 상점 등에서 호출)
    public void Equip(SwordData sword)
    {
        equippedSword = sword;
        if (slashTrail != null && sword != null)
            slashTrail.SetColor(sword.trailColor);
    }

    [Header("Visual")]
    [SerializeField] private WeaponSwing weaponSwing;
    [SerializeField] private SwordSlashTrail slashTrail;
    [Tooltip("적중 시 맞은 자리에서 튀는 불꽃 효과 (색은 장착한 검의 궤적 색을 따름)")]
    [SerializeField] private ParticleSystem hitEffectPrefab;
    [SerializeField] private float strongHitEffectScale = 1.5f;

    [Header("타격 멈춤 (맞는 순간 게임이 잠깐 멈칫)")]
    [SerializeField] private bool useHitStop = true;
    [SerializeField] private float hitStopDuration = 0.05f;
    [SerializeField] private float strongHitStopDuration = 0.09f;
    [SerializeField, Range(0f, 1f)] private float hitStopTimeScale = 0.05f;

    private Coroutine hitStopRoutine;

    [Header("References")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Animator animator;

    private static readonly int AttackTrigger = Animator.StringToHash("Attack");

    private PlayerParry playerParry;

    private int[] stateHashes;
    private int currentStep = -1;      // 지금 재생 중인 베기 순번 (없으면 -1)
    private bool hitDone;
    private bool startRequested;       // 대기/이동 상태에서 첫 베기를 요청한 직후
    private float startRequestTime;
    private float cooldownTimer;

    public bool IsAttacking => currentStep >= 0 || startRequested;

    private void Awake()
    {
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (slashTrail == null)
            slashTrail = GetComponentInChildren<SwordSlashTrail>(true);
        playerParry = GetComponent<PlayerParry>();

        stateHashes = new int[comboHits.Length];
        for (int i = 0; i < comboHits.Length; i++)
            stateHashes[i] = Animator.StringToHash(comboHits[i].stateName);

        Equip(equippedSword);
    }

    private void Update()
    {
        cooldownTimer -= Time.deltaTime;

        bool attackPressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        if (playerMovement != null && playerMovement.IsRestrained)
        {
            if (attackPressed)
                playerMovement.BreakRestrain();
            return;
        }

        if (animator == null)
            return;

        UpdateCurrentStep();
        UpdateTrail();

        if (currentStep < 0)
        {
            if (startRequested && Time.time - startRequestTime > 0.3f)
            {
                // 애니메이터가 공격으로 넘어가지 못한 경우(회피 중 등) 요청을 취소한다.
                startRequested = false;
                animator.ResetTrigger(AttackTrigger);
            }

            bool isDodging = playerMovement != null && playerMovement.IsDodging;
            bool isParrying = playerParry != null && playerParry.IsParrying;
            if (attackPressed && !startRequested && !isDodging && !isParrying && cooldownTimer <= 0f)
            {
                animator.SetTrigger(AttackTrigger);
                startRequested = true;
                startRequestTime = Time.time;
            }
            return;
        }

        bool isLastHit = currentStep >= comboHits.Length - 1;
        if (attackPressed && !isLastHit)
            animator.SetTrigger(AttackTrigger);   // 다음 베기 예약 (애니메이터가 알맞은 때에 이어 줌)
        else if (isLastHit)
            animator.ResetTrigger(AttackTrigger);  // 마지막 타 중 입력은 무시 (끝나고 저절로 다시 시작되지 않게)

        if (!hitDone && CurrentStateTime() >= comboHits[currentStep].hitTime)
        {
            hitDone = true;
            DealDamage(AttackPower * comboHits[currentStep].damageMultiplier, currentStep == comboHits.Length - 1);
        }
    }

    private void UpdateCurrentStep()
    {
        int step = -1;
        if (animator.IsInTransition(0))
            step = StepOf(animator.GetNextAnimatorStateInfo(0).shortNameHash);
        if (step < 0)
            step = StepOf(animator.GetCurrentAnimatorStateInfo(0).shortNameHash);

        if (step == currentStep)
            return;

        if (step >= 0)
        {
            startRequested = false;
            hitDone = false;
            if (weaponSwing != null)
                weaponSwing.PlaySwing();
        }
        else if (currentStep >= 0)
        {
            // 연속 공격이 끝남
            cooldownTimer = comboCooldown;
            animator.ResetTrigger(AttackTrigger);
        }

        currentStep = step;
    }

    private void UpdateTrail()
    {
        if (slashTrail == null)
            return;
        if (currentStep < 0)
        {
            slashTrail.SetEmitting(false, false);
            return;
        }

        float t = CurrentStateTime();
        ComboHit hit = comboHits[currentStep];
        bool on = t >= hit.trailFrom && t <= hit.trailTo;
        slashTrail.SetEmitting(on, currentStep == comboHits.Length - 1);
    }

    private float CurrentStateTime()
    {
        AnimatorStateInfo info = animator.IsInTransition(0) && StepOf(animator.GetNextAnimatorStateInfo(0).shortNameHash) == currentStep
            ? animator.GetNextAnimatorStateInfo(0)
            : animator.GetCurrentAnimatorStateInfo(0);
        return info.normalizedTime * info.length;
    }

    private int StepOf(int shortNameHash)
    {
        for (int i = 0; i < stateHashes.Length; i++)
            if (stateHashes[i] == shortNameHash)
                return i;
        return -1;
    }

    private readonly System.Collections.Generic.List<EnemyHealth> hitThisSwing = new System.Collections.Generic.List<EnemyHealth>();

    private void DealDamage(float damage, bool strongHit)
    {
        Vector3 attackPoint = transform.position + transform.forward * attackRange;
        Collider[] hits = Physics.OverlapSphere(attackPoint, attackRadius, enemyLayers);

        hitThisSwing.Clear();
        foreach (Collider hit in hits)
        {
            EnemyHealth enemyHealth = hit.GetComponentInParent<EnemyHealth>();
            if (enemyHealth == null || enemyHealth.IsDead || hitThisSwing.Contains(enemyHealth))
                continue;   // 한 번 휘두를 때 적 하나당 한 번만

            hitThisSwing.Add(enemyHealth);
            enemyHealth.TakeDamage(damage);
            SpawnHitEffect(hit, attackPoint, strongHit);
        }

        if (hitThisSwing.Count > 0)
            StartHitStop(strongHit ? strongHitStopDuration : hitStopDuration);
    }

    private void StartHitStop(float duration)
    {
        if (!useHitStop || duration <= 0f)
            return;
        // 일시정지 등 다른 이유로 이미 시간이 멈춰 있으면 건드리지 않는다.
        if (hitStopRoutine == null && Time.timeScale < 0.99f)
            return;

        if (hitStopRoutine != null)
            StopCoroutine(hitStopRoutine);
        hitStopRoutine = StartCoroutine(HitStopRoutine(duration));
    }

    private System.Collections.IEnumerator HitStopRoutine(float duration)
    {
        Time.timeScale = hitStopTimeScale;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
        hitStopRoutine = null;
    }

    private void OnDisable()
    {
        // 멈춘 상태로 오브젝트가 꺼지면 시간이 계속 느려지지 않도록 복구
        if (hitStopRoutine != null)
        {
            StopCoroutine(hitStopRoutine);
            hitStopRoutine = null;
            Time.timeScale = 1f;
        }
    }

    private void SpawnHitEffect(Collider target, Vector3 attackPoint, bool strongHit)
    {
        if (hitEffectPrefab == null)
            return;

        // 칼날 가운데에서 가장 가까운 적 몸 표면을 맞은 자리로 본다.
        Vector3 from = attackPoint;
        if (slashTrail != null && slashTrail.TryGetBladeMidPoint(out Vector3 blade))
            from = blade;
        bool canClosest = !(target is MeshCollider mc && !mc.convex);
        Vector3 point = canClosest ? target.ClosestPoint(from) : target.bounds.ClosestPoint(from);
        if ((point - from).sqrMagnitude < 0.0001f)
            point = target.bounds.center;

        ParticleSystem fx = Instantiate(hitEffectPrefab, point, Quaternion.LookRotation(from - point == Vector3.zero ? -transform.forward : (from - point).normalized));
        if (strongHit)
            fx.transform.localScale *= strongHitEffectScale;

        Color c = equippedSword != null ? equippedSword.trailColor.Evaluate(0f) : Color.white;
        c.a = 1f;
        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>())
        {
            var main = ps.main;
            main.startColor = c;
            if (strongHit)
            {
                var emission = ps.emission;
                if (emission.burstCount > 0)
                {
                    var burst = emission.GetBurst(0);
                    burst.count = new ParticleSystem.MinMaxCurve(burst.count.constant * strongHitEffectScale);
                    emission.SetBurst(0, burst);
                }
            }
        }
        fx.Play(true);
        Destroy(fx.gameObject, 1.5f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 attackPoint = transform.position + transform.forward * attackRange;
        Gizmos.DrawWireSphere(attackPoint, attackRadius);
    }
}
