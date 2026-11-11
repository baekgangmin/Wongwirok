using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// 우클릭 패링: 방어 자세를 잠깐 취하고, 그 사이 적의 공격 판정과 겹치면 패링 성공.
// 성공하면 칼 위치에서 큰 불꽃 + 짧은 멈춤 + 화면 흔들림.
public class PlayerParry : MonoBehaviour
{
    [SerializeField] private float parryWindow = 0.2f;
    [SerializeField] private float parryRange = 2f;
    [SerializeField] private float parryCooldown = 0.5f;
    [SerializeField] private LayerMask enemyLayers = ~0;

    [Header("패링 동작")]
    [SerializeField] private Animator animator;
    [Tooltip("방어 자세를 유지하는 동안 이동을 막는 시간")]
    [SerializeField] private float poseLockTime = 0.4f;

    [Header("패링 성공 연출")]
    [SerializeField] private ParticleSystem successEffectPrefab;
    [SerializeField] private Color successEffectColor = new Color(1f, 0.85f, 0.45f, 1f);
    [SerializeField] private float successEffectScale = 2f;
    [SerializeField] private float successStopDuration = 0.12f;
    [SerializeField, Range(0f, 1f)] private float successStopTimeScale = 0.05f;
    [SerializeField] private float shakeAmplitude = 0.15f;
    [SerializeField] private float shakeDuration = 0.2f;

    private static readonly int ParryTrigger = Animator.StringToHash("Parry");

    private PlayerAttack playerAttack;
    private PlayerMovement playerMovement;
    private SwordSlashTrail slashTrail;
    private float parryTimer;
    private float cooldownTimer;
    private float poseTimer;
    private Coroutine stopRoutine;

    public bool IsParrying => poseTimer > 0f;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        playerAttack = GetComponent<PlayerAttack>();
        playerMovement = GetComponent<PlayerMovement>();
        slashTrail = GetComponentInChildren<SwordSlashTrail>(true);
    }

    private void Update()
    {
        cooldownTimer -= Time.deltaTime;
        parryTimer -= Time.deltaTime;
        poseTimer -= Time.deltaTime;

        bool pressed = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        bool busy = (playerAttack != null && playerAttack.IsAttacking)
                    || (playerMovement != null && (playerMovement.IsDodging || playerMovement.IsRestrained));

        if (pressed && cooldownTimer <= 0f && !busy)
        {
            parryTimer = parryWindow;
            cooldownTimer = parryCooldown;
            poseTimer = poseLockTime;
            if (animator != null)
                animator.SetTrigger(ParryTrigger);
        }

        if (parryTimer > 0f)
            TryParryNearbyEnemies();
    }

    private void TryParryNearbyEnemies()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, parryRange, enemyLayers);
        foreach (Collider hit in hits)
        {
            EnemyAttack enemyAttack = hit.GetComponentInParent<EnemyAttack>();
            if (enemyAttack != null && enemyAttack.AttemptParry())
            {
                parryTimer = 0f;
                PlaySuccess(hit);
                break;
            }
        }
    }

    private void PlaySuccess(Collider enemy)
    {
        // 불꽃: 칼날 가운데 (없으면 몸 앞 가슴 높이)
        Vector3 point = transform.position + transform.forward * 0.7f + Vector3.up * 1.2f;
        if (slashTrail != null && slashTrail.TryGetBladeMidPoint(out Vector3 blade))
            point = blade;

        if (successEffectPrefab != null)
        {
            Vector3 toEnemy = enemy.bounds.center - point;
            Quaternion rot = toEnemy.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(-toEnemy.normalized) : Quaternion.LookRotation(-transform.forward);
            ParticleSystem fx = Instantiate(successEffectPrefab, point, rot);
            fx.transform.localScale *= successEffectScale;
            foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>())
            {
                var main = ps.main;
                main.startColor = successEffectColor;
                var emission = ps.emission;
                if (emission.burstCount > 0)
                {
                    var burst = emission.GetBurst(0);
                    burst.count = new ParticleSystem.MinMaxCurve(burst.count.constant * successEffectScale);
                    emission.SetBurst(0, burst);
                }
            }
            fx.Play(true);
            Destroy(fx.gameObject, 1.5f);
        }

        if (ThirdPersonCamera.Instance != null)
            ThirdPersonCamera.Instance.Shake(shakeAmplitude, shakeDuration);

        // 짧은 멈춤 (이미 다른 이유로 시간이 느려져 있으면 건드리지 않음)
        if (successStopDuration > 0f && (stopRoutine != null || Time.timeScale >= 0.99f))
        {
            if (stopRoutine != null)
                StopCoroutine(stopRoutine);
            stopRoutine = StartCoroutine(StopRoutine());
        }
    }

    private IEnumerator StopRoutine()
    {
        Time.timeScale = successStopTimeScale;
        yield return new WaitForSecondsRealtime(successStopDuration);
        Time.timeScale = 1f;
        stopRoutine = null;
    }

    private void OnDisable()
    {
        if (stopRoutine != null)
        {
            StopCoroutine(stopRoutine);
            stopRoutine = null;
            Time.timeScale = 1f;
        }
    }
}
