using UnityEngine;
using UnityEngine.InputSystem;

// 플레이어 공격.
// - 짧게 좌클릭: 3단 연속 베기 (공격 중 다시 클릭하면 다음 베기로 이어짐)
// - 서서 좌클릭을 길게 누름: 차지 → 놓으면 강공격 (오래 모을수록 강함)
// - 패링 성공 직후 좌클릭: 패링 반격 (뛰어올라 내리꽂기)
// - 회피 중/직후 좌클릭: 회피 공격 (몸을 돌리며 주변을 벰)
// - 달리는 중(Shift) 좌클릭: 달리기 공격 (앞으로 파고들며 찌르기)
// 피해는 클릭 순간이 아니라 칼이 가장 빠르게 지나가는 순간(hitTime)에 들어간다.
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

    [System.Serializable]
    private struct SpecialAttack
    {
        public string stateName;
        public float damageMultiplier;
        public float hitTime;
        public float trailFrom;
        public float trailTo;
        [Tooltip("앞으로 이만큼 떨어진 곳을 중심으로 판정 (0이면 몸 둘레)")]
        public float range;
        public float radius;
        [Tooltip("공격하며 앞으로 파고드는 거리(m)와 그 시간대")]
        public float lungeDistance;
        public float lungeFrom;
        public float lungeTo;
        public float hitStop;
        public float shake;
        public float effectScale;
    }

    private const int Riposte = 0, ChargeRelease = 1, DodgeAttack = 2, RunAttack = 3;

    [Header("Attack")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackRadius = 1f;
    [SerializeField] private float comboCooldown = 0.3f;
    [Tooltip("물기둥에 갇혔을 때 공격 버튼 한 번에 줄어드는 시간(초)")]
    [SerializeField] private float trapStruggleSeconds = 0.25f;
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

    [Header("특수 공격 (0 패링 반격 / 1 차지 강공격 / 2 회피 공격 / 3 달리기 공격)")]
    [SerializeField] private SpecialAttack[] specialAttacks =
    {
        new SpecialAttack { stateName = "Riposte", damageMultiplier = 3f, hitTime = 0.48f, trailFrom = 0.25f, trailTo = 0.62f,
            range = 1.4f, radius = 1.5f, lungeDistance = 1.5f, lungeFrom = 0.1f, lungeTo = 0.45f, hitStop = 0.16f, shake = 0.22f, effectScale = 2.2f },
        new SpecialAttack { stateName = "ChargeRelease", damageMultiplier = 1.5f, hitTime = 0.42f, trailFrom = 0.32f, trailTo = 0.58f,
            range = 1.6f, radius = 1.6f, lungeDistance = 0.6f, lungeFrom = 0.3f, lungeTo = 0.45f, hitStop = 0.12f, shake = 0.15f, effectScale = 1.8f },
        new SpecialAttack { stateName = "DodgeAttack", damageMultiplier = 1.5f, hitTime = 0.35f, trailFrom = 0.12f, trailTo = 0.6f,
            range = 0f, radius = 2.3f, lungeDistance = 0f, hitStop = 0.08f, shake = 0.08f, effectScale = 1.5f },
        new SpecialAttack { stateName = "RunAttack", damageMultiplier = 1.8f, hitTime = 0.56f, trailFrom = 0.45f, trailTo = 0.66f,
            range = 1.8f, radius = 1.0f, lungeDistance = 2.5f, lungeFrom = 0.35f, lungeTo = 0.6f, hitStop = 0.1f, shake = 0.1f, effectScale = 1.6f },
    };

    [Header("입력 판정")]
    [Tooltip("이 시간보다 오래 누르면 차지 시작 (짧으면 일반 베기)")]
    [SerializeField] private float chargeStartTime = 0.22f;
    [Tooltip("차지를 끝까지 모으는 데 걸리는 시간 (차지 시작 후)")]
    [SerializeField] private float chargeFullTime = 0.8f;
    [Tooltip("차지 강공격 배율: 최소 ~ 최대 (끝까지 모으면 최대)")]
    [SerializeField] private float chargeMinMultiplier = 1.5f;
    [SerializeField] private float chargeMaxMultiplier = 3f;
    [Tooltip("패링 성공 후 이 시간 안에 좌클릭하면 패링 반격")]
    [SerializeField] private float riposteWindow = 0.8f;
    [Tooltip("회피가 끝난 뒤 이 시간 안에 좌클릭하면 회피 공격")]
    [SerializeField] private float dodgeAttackWindow = 0.3f;

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
    [Tooltip("차지하는 동안 칼에 모이는 기운 효과")]
    [SerializeField] private ParticleSystem chargeEffectPrefab;
    [SerializeField] private Color riposteColor = new Color(1f, 0.85f, 0.45f, 1f);

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
    private static readonly int ChargingId = Animator.StringToHash("Charging");
    private static readonly int[] SpecialTriggers =
    {
        Animator.StringToHash("Riposte"), Animator.StringToHash("ChargeRelease"),
        Animator.StringToHash("DodgeAttack"), Animator.StringToHash("RunAttack"),
    };
    private static readonly int ChargeHoldHash = Animator.StringToHash("ChargeHold");

    private PlayerParry playerParry;
    private CharacterController controller;

    private int[] stateHashes;          // 콤보 3개 + 특수 4개
    private int currentStep = -1;      // 지금 재생 중인 공격 순번 (없으면 -1). 0~2 콤보, 3~ 특수
    private bool hitDone;
    private bool startRequested;       // 공격을 요청했지만 애니메이터가 아직 넘어가지 않음
    private float startRequestTime;
    private float cooldownTimer;

    // 차지
    private bool pressPending;          // 서서 눌렀고 아직 짧은 클릭인지 차지인지 모름
    private float pressTime;
    private bool charging;
    private float chargeLevel;
    private bool chargeFullNotified;
    private float releaseMultiplier = 1f;
    private ParticleSystem activeChargeFx;
    private bool dodgeAttackQueued;
    private float lungeDone;

    public bool IsAttacking => currentStep >= 0 || startRequested || charging || pressPending;
    public bool IsCharging => charging;

    private int ComboCount => comboHits.Length;
    private bool IsSpecialStep(int step) => step >= ComboCount;

    private void Awake()
    {
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (slashTrail == null)
            slashTrail = GetComponentInChildren<SwordSlashTrail>(true);
        playerParry = GetComponent<PlayerParry>();
        controller = GetComponent<CharacterController>();

        stateHashes = new int[comboHits.Length + specialAttacks.Length];
        for (int i = 0; i < comboHits.Length; i++)
            stateHashes[i] = Animator.StringToHash(comboHits[i].stateName);
        for (int i = 0; i < specialAttacks.Length; i++)
            stateHashes[comboHits.Length + i] = Animator.StringToHash(specialAttacks[i].stateName);

        Equip(equippedSword);
    }

    private void Update()
    {
        cooldownTimer -= Time.deltaTime;

        Mouse mouse = Mouse.current;
        bool attackPressed = mouse != null && mouse.leftButton.wasPressedThisFrame;
        bool attackHeld = mouse != null && mouse.leftButton.isPressed;

        if (playerMovement != null && playerMovement.IsRestrained)
        {
            CancelCharge();
            if (attackPressed)
                playerMovement.BreakRestrain();
            return;
        }

        // 물기둥에 갇힌 동안: 공격 버튼을 누를 때마다 빨리 빠져나옴
        if (playerMovement != null && playerMovement.IsTrapped)
        {
            CancelCharge();
            if (attackPressed)
                playerMovement.StruggleTrap(trapStruggleSeconds);
            if (animator != null)
                UpdateCurrentStep();   // 공격 중에 갇혔으면 공격 상태를 정리
            startRequested = false;
            if (slashTrail != null)
                slashTrail.SetEmitting(false, false);
            return;
        }

        if (animator == null)
            return;

        UpdateCurrentStep();
        UpdateTrail();
        UpdateLunge();

        bool isDodging = playerMovement != null && playerMovement.IsDodging;

        // 회피 중에 누른 공격은 회피가 끝나는 순간 회피 공격으로
        if (dodgeAttackQueued && !isDodging)
        {
            dodgeAttackQueued = false;
            StartSpecial(DodgeAttack);
            return;
        }

        if (charging || pressPending)
        {
            if (isDodging)
            {
                CancelCharge();
                return;
            }
            UpdateCharge(attackHeld);
            return;
        }

        if (currentStep < 0)
        {
            if (startRequested && Time.time - startRequestTime > 0.3f)
            {
                // 애니메이터가 공격으로 넘어가지 못한 경우 요청을 취소한다.
                startRequested = false;
                ResetAllTriggers();
            }

            if (!attackPressed || startRequested)
                return;

            // 1) 패링 성공 직후 → 패링 반격 (패링 자세 중에도 가능)
            if (playerParry != null && Time.time - playerParry.LastParrySuccessTime <= riposteWindow)
            {
                playerParry.EndPose();
                StartSpecial(Riposte);
                return;
            }

            bool isParrying = playerParry != null && playerParry.IsParrying;
            if (isParrying || cooldownTimer > 0f)
                return;

            // 2) 회피 중/직후 → 회피 공격
            if (isDodging)
            {
                dodgeAttackQueued = true;
                return;
            }
            if (playerMovement != null && Time.time - playerMovement.LastDodgeEndTime <= dodgeAttackWindow)
            {
                StartSpecial(DodgeAttack);
                return;
            }

            // 3) 달리는 중 → 달리기 공격
            if (playerMovement != null && playerMovement.IsRunning)
            {
                StartSpecial(RunAttack);
                return;
            }

            // 4) 서서 누름 → 짧게 떼면 일반 베기, 길게 누르면 차지
            pressPending = true;
            pressTime = Time.time;
            return;
        }

        if (IsSpecialStep(currentStep))
        {
            ResetAllTriggers();   // 특수 공격 중 입력은 무시
        }
        else
        {
            bool isLastHit = currentStep >= comboHits.Length - 1;
            if (attackPressed && !isLastHit)
                animator.SetTrigger(AttackTrigger);   // 다음 베기 예약 (애니메이터가 알맞은 때에 이어 줌)
            else if (isLastHit)
                animator.ResetTrigger(AttackTrigger);  // 마지막 타 중 입력은 무시 (끝나고 저절로 다시 시작되지 않게)
        }

        if (!hitDone && CurrentStateTime() >= HitTimeOf(currentStep))
        {
            hitDone = true;
            if (IsSpecialStep(currentStep))
                DealSpecialDamage(currentStep - ComboCount);
            else
                DealDamage(AttackPower * comboHits[currentStep].damageMultiplier, currentStep == comboHits.Length - 1);
        }
    }

    // ───────────── 차지 ─────────────
    private void UpdateCharge(bool held)
    {
        float heldTime = Time.time - pressTime;
        if (pressPending)
        {
            if (!held)
            {
                // 짧은 클릭 → 일반 3단 베기
                pressPending = false;
                animator.SetTrigger(AttackTrigger);
                startRequested = true;
                startRequestTime = Time.time;
                return;
            }
            if (heldTime >= chargeStartTime)
            {
                pressPending = false;
                charging = true;
                chargeLevel = 0f;
                chargeFullNotified = false;
                animator.SetBool(ChargingId, true);
                SpawnChargeEffect();
            }
            return;
        }

        chargeLevel = Mathf.Clamp01((heldTime - chargeStartTime) / Mathf.Max(0.01f, chargeFullTime));
        if (activeChargeFx != null)
            activeChargeFx.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.6f, chargeLevel) / Mathf.Max(0.0001f, activeChargeFx.transform.parent != null ? activeChargeFx.transform.parent.lossyScale.x : 1f);
        if (chargeLevel >= 1f && !chargeFullNotified)
        {
            // 끝까지 모임: 번쩍 + 살짝 흔들림
            chargeFullNotified = true;
            if (ThirdPersonCamera.Instance != null)
                ThirdPersonCamera.Instance.Shake(0.05f, 0.12f);
            SpawnFlash(SwordPoint(), SwordColor(), 1.2f);
        }

        if (!held)
        {
            charging = false;
            releaseMultiplier = Mathf.Lerp(chargeMinMultiplier, chargeMaxMultiplier, chargeLevel);
            animator.SetBool(ChargingId, false);
            StopChargeEffect();
            StartSpecial(ChargeRelease);
        }
    }

    private void CancelCharge()
    {
        if (!charging && !pressPending)
            return;
        charging = false;
        pressPending = false;
        if (animator != null)
            animator.SetBool(ChargingId, false);
        StopChargeEffect();
    }

    private void SpawnChargeEffect()
    {
        if (chargeEffectPrefab == null)
            return;
        Transform hand = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : transform;
        activeChargeFx = Instantiate(chargeEffectPrefab, hand);
        activeChargeFx.transform.localPosition = Vector3.zero;
        Color c = SwordColor();
        foreach (ParticleSystem ps in activeChargeFx.GetComponentsInChildren<ParticleSystem>())
        {
            var main = ps.main;
            main.startColor = c;
        }
        activeChargeFx.Play(true);
    }

    private void StopChargeEffect()
    {
        if (activeChargeFx == null)
            return;
        activeChargeFx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Destroy(activeChargeFx.gameObject, 0.6f);
        activeChargeFx = null;
    }

    // ───────────── 특수 공격 시작 ─────────────
    private void StartSpecial(int index)
    {
        if (index != ChargeRelease)
            releaseMultiplier = 1f;
        ResetAllTriggers();
        animator.SetTrigger(SpecialTriggers[index]);
        startRequested = true;
        startRequestTime = Time.time;

        // 반격은 가장 가까운 적 쪽으로 몸을 돌려서 나간다
        if (index == Riposte || index == DodgeAttack)
            FaceNearestEnemy(4f);
        if (index == Riposte)
            SpawnFlash(SwordPoint(), riposteColor, 1.5f);
    }

    private void ResetAllTriggers()
    {
        animator.ResetTrigger(AttackTrigger);
        foreach (int t in SpecialTriggers)
            animator.ResetTrigger(t);
    }

    private void FaceNearestEnemy(float maxDistance)
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, maxDistance, enemyLayers);
        float best = float.MaxValue;
        Vector3 bestDir = Vector3.zero;
        foreach (Collider c in cols)
        {
            EnemyHealth eh = c.GetComponentInParent<EnemyHealth>();
            if (eh == null || eh.IsDead)
                continue;
            Vector3 d = c.bounds.center - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < best)
            {
                best = d.sqrMagnitude;
                bestDir = d;
            }
        }
        if (bestDir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(bestDir.normalized, Vector3.up);
    }

    // 동작에 맞춰 앞으로 파고들기 (달리기 공격, 패링 반격)
    private void UpdateLunge()
    {
        if (currentStep < 0 || !IsSpecialStep(currentStep) || controller == null)
            return;
        SpecialAttack s = specialAttacks[currentStep - ComboCount];
        if (s.lungeDistance <= 0f || s.lungeTo <= s.lungeFrom)
            return;
        float t = CurrentStateTime();
        float k = Mathf.Clamp01((t - s.lungeFrom) / (s.lungeTo - s.lungeFrom));
        float target = s.lungeDistance * Mathf.SmoothStep(0f, 1f, k);
        float step = target - lungeDone;
        if (step > 0f)
        {
            controller.Move(transform.forward * step);
            lungeDone = target;
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
            lungeDone = 0f;
            if (weaponSwing != null)
                weaponSwing.PlaySwing();
        }
        else if (currentStep >= 0)
        {
            // 공격이 끝남
            cooldownTimer = comboCooldown;
            ResetAllTriggers();
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
        float from, to;
        bool strong;
        if (IsSpecialStep(currentStep))
        {
            SpecialAttack s = specialAttacks[currentStep - ComboCount];
            from = s.trailFrom;
            to = s.trailTo;
            strong = true;
        }
        else
        {
            ComboHit hit = comboHits[currentStep];
            from = hit.trailFrom;
            to = hit.trailTo;
            strong = currentStep == comboHits.Length - 1;
        }
        slashTrail.SetEmitting(t >= from && t <= to, strong);
    }

    private float HitTimeOf(int step)
    {
        return IsSpecialStep(step) ? specialAttacks[step - ComboCount].hitTime : comboHits[step].hitTime;
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
        if (HitEnemies(attackPoint, attackRadius, damage, strongHit ? strongHitEffectScale : 1f, null) > 0)
            StartHitStop(strongHit ? strongHitStopDuration : hitStopDuration);
    }

    private void DealSpecialDamage(int index)
    {
        SpecialAttack s = specialAttacks[index];
        Vector3 attackPoint = transform.position + transform.forward * s.range;
        float damage = AttackPower * s.damageMultiplier * (index == ChargeRelease ? releaseMultiplier / Mathf.Max(0.01f, s.damageMultiplier) : 1f);
        Color? color = index == Riposte ? riposteColor : (Color?)null;
        float scale = s.effectScale * (index == ChargeRelease ? Mathf.Lerp(0.8f, 1.3f, chargeLevel) : 1f);
        int count = HitEnemies(attackPoint, s.radius, damage, scale, color);
        if (count > 0)
        {
            StartHitStop(s.hitStop);
            if (s.shake > 0f && ThirdPersonCamera.Instance != null)
                ThirdPersonCamera.Instance.Shake(s.shake, 0.22f);
        }
        // 회피 공격은 몸 둘레로 물결처럼 번지는 빛
        if (index == DodgeAttack)
            SpawnFlash(transform.position + Vector3.up * 1f, SwordColor(), 2f);
    }

    private int HitEnemies(Vector3 attackPoint, float radius, float damage, float effectScale, Color? colorOverride)
    {
        Collider[] hits = Physics.OverlapSphere(attackPoint, radius, enemyLayers);
        hitThisSwing.Clear();
        foreach (Collider hit in hits)
        {
            EnemyHealth enemyHealth = hit.GetComponentInParent<EnemyHealth>();
            if (enemyHealth == null || enemyHealth.IsDead || hitThisSwing.Contains(enemyHealth))
                continue;   // 한 번 휘두를 때 적 하나당 한 번만

            hitThisSwing.Add(enemyHealth);
            enemyHealth.TakeDamage(damage);
            SpawnHitEffect(hit, attackPoint, effectScale, colorOverride);
        }
        return hitThisSwing.Count;
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
        StopChargeEffect();
    }

    private Color SwordColor()
    {
        Color c = equippedSword != null ? equippedSword.trailColor.Evaluate(0f) : Color.white;
        c.a = 1f;
        return c;
    }

    private Vector3 SwordPoint()
    {
        if (slashTrail != null && slashTrail.TryGetBladeMidPoint(out Vector3 blade))
            return blade;
        return transform.position + transform.forward * 0.6f + Vector3.up * 1.2f;
    }

    // 맞지 않아도 터지는 번쩍임 (차지 완료, 반격 시작, 회피 공격)
    private void SpawnFlash(Vector3 point, Color color, float scale)
    {
        if (hitEffectPrefab == null)
            return;
        ParticleSystem fx = Instantiate(hitEffectPrefab, point, Quaternion.LookRotation(transform.forward));
        fx.transform.localScale *= scale;
        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>())
        {
            var main = ps.main;
            main.startColor = color;
        }
        fx.Play(true);
        Destroy(fx.gameObject, 1.5f);
    }

    private void SpawnHitEffect(Collider target, Vector3 attackPoint, float scale, Color? colorOverride)
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
        fx.transform.localScale *= scale;

        Color c = colorOverride ?? SwordColor();
        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>())
        {
            var main = ps.main;
            main.startColor = c;
            if (scale > 1.01f)
            {
                var emission = ps.emission;
                if (emission.burstCount > 0)
                {
                    var burst = emission.GetBurst(0);
                    burst.count = new ParticleSystem.MinMaxCurve(burst.count.constant * scale);
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
