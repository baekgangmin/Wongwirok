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

    [Header("할퀴기 그림 (2D 이펙트)")]
    [Tooltip("할퀴는 순간 앞에 잠깐 띄우는 그림 재질 (비우면 없음)")]
    [SerializeField] private Material slashMaterial;
    [SerializeField] private float slashSize = 2.2f;
    [SerializeField] private float slashLifetime = 0.35f;
    [Tooltip("공격 판정 시작 뒤 그림이 뜨기까지 (팔을 휘두르는 순간에 맞춤)")]
    [SerializeField] private float slashDelay = 0.1f;
    [Tooltip("먹 방울·얼룩 색 (어두운 배경에서도 보이게)")]
    [SerializeField] private Color inkColor = new Color(0.32f, 0.03f, 0.04f, 1f);

    [Header("주인공이 맞았을 때 (이 잡몹 모양)")]
    [Tooltip("먹과 섞여 튀는 두 번째 색 (물귀신은 탁한 연못물)")]
    [SerializeField] private Color hitSecondColor = new Color(0.28f, 0.32f, 0.14f, 1f);
    [Tooltip("주인공 몸에 그어지는 상처 그림 (비우면 기본 할퀸 자국)")]
    [SerializeField] private Material hitMarkMaterial;
    [Tooltip("맞은 순간 화면 가장자리에 번쩍이는 테두리 그림 (비우면 색만)")]
    [SerializeField] private Texture hitFrameTexture;
    [Tooltip("할퀼 때 튀는 먹 방울 (비우면 없음)")]
    [SerializeField] private ParticleSystem slashSplatterPrefab;
    [Tooltip("바닥에 잠깐 남는 먹 얼룩 재질 (비우면 없음)")]
    [SerializeField] private Material inkStainMaterial;
    [Tooltip("공격이 맞은 순간 화면이 멈칫하는 시간 (초, 0이면 없음)")]
    [SerializeField] private float hitStopDuration = 0.06f;

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
    private PlayerHitFeedback playerHitFeedback;

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
            playerHitFeedback = player.GetComponent<PlayerHitFeedback>();
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
        if (slashMaterial != null)
            StartCoroutine(SlashRoutine());
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
        else
        {
            // 피해든 느려짐이든 맞은 연출은 이 잡몹 모양으로
            playerHitFeedback?.SetNextHit(inkColor, hitSecondColor, hitMarkMaterial, slashSplatterPrefab, hitFrameTexture);
            if (slowsPlayerInsteadOfDamage)
                playerMovement?.ApplySlow(slowMultiplier, slowDuration);
            else
                playerHealth?.TakeDamage(attackDamage);
            if (hitStopDuration > 0f && Time.timeScale >= 0.99f)
                StartCoroutine(HitStop());
        }

        SetColor(baseColor);
        yield return new WaitForSeconds(recoveryDuration);

        isBusy = false;
        cooldownTimer = attackCooldown;
    }

    // 할퀴는 순간 몸 앞에 그림 한 장: 카메라를 보며 붓으로 긋듯 한쪽부터 펼쳐졌다가 사라지고, 먹이 튐
    private IEnumerator SlashRoutine()
    {
        if (slashDelay > 0f)
            yield return new WaitForSeconds(slashDelay);
        Vector3 pos = transform.position + transform.forward * hitRange * 0.5f + Vector3.up * 0.6f;
        if (slashSplatterPrefab != null)
        {
            var fx = Instantiate(slashSplatterPrefab, pos, Quaternion.LookRotation(transform.forward));
            foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>())
            {
                var main = ps.main;
                main.startColor = inkColor;
            }
            fx.Play(true);
        }
        if (inkStainMaterial != null)
            StartCoroutine(InkStainRoutine(pos));
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(go.GetComponent<Collider>());
        Destroy(go, slashLifetime);   // 잡몹이 사라져 코루틴이 멈춰도 그림은 남지 않게
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = slashMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        Texture tex = slashMaterial.mainTexture;
        float aspect = tex != null ? (float)tex.width / tex.height : 1f;
        var mpb = new MaterialPropertyBlock();
        float t = 0f, nextBoil = 0f, boilRot = 0f, boilScale = 1f;
        while (t < slashLifetime && go != null)
        {
            t += Time.deltaTime;
            // 손그림 떨림: 0.05초마다 각도·크기를 살짝 바꿈
            if (t >= nextBoil)
            {
                nextBoil = t + 0.05f;
                boilRot = Random.Range(-5f, 5f);
                boilScale = Random.Range(0.95f, 1.05f);
            }
            Camera cam = Camera.main;
            if (cam != null)
                go.transform.rotation = Quaternion.LookRotation(pos - cam.transform.position) * Quaternion.Euler(0f, 0f, 180f + boilRot);   // 그림을 화면에서 180도 돌림
            float draw = Mathf.Clamp01(t / SlashDrawTime);   // 그어지는 정도 (그림 왼쪽부터 보임)
            float fade = 1f - Mathf.Clamp01((t - SlashDrawTime) / (slashLifetime - SlashDrawTime));
            float pop = draw < 1f ? 1.12f : Mathf.Lerp(1.12f, 1f, (t - SlashDrawTime) / 0.1f);   // 그은 직후 살짝 컸다가 제자리
            float size = slashSize * pop * boilScale;
            float width = aspect * size;
            go.transform.position = pos + go.transform.right * width * (draw - 1f) * 0.5f;   // 왼쪽 끝은 고정
            go.transform.localScale = new Vector3(width * Mathf.Max(draw, 0.001f), size, 1f);
            mpb.SetVector(BaseMapStId, new Vector4(draw, 1f, 0f, 0f));
            mpb.SetColor(BaseColorId, new Color(1f, 1f, 1f, fade));
            mr.SetPropertyBlock(mpb);
            yield return null;
        }
    }

    // 떨어진 먹이 바닥에 얼룩으로 잠깐 남음
    private IEnumerator InkStainRoutine(Vector3 pos)
    {
        yield return new WaitForSeconds(0.25f);   // 방울이 떨어질 즈음
        if (!Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Ignore))
            yield break;
        int n = Random.Range(3, 6);
        var stains = new MeshRenderer[n];
        for (int i = 0; i < n; i++)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            Destroy(q, 2.2f);
            Vector3 off = transform.forward * Random.Range(0f, 1.2f) + transform.right * Random.Range(-0.6f, 0.6f);
            q.transform.SetPositionAndRotation(hit.point + off + Vector3.up * 0.02f, Quaternion.Euler(90f, Random.Range(0f, 360f), 0f));
            q.transform.localScale = Vector3.one * Random.Range(0.25f, 0.7f);
            stains[i] = q.GetComponent<MeshRenderer>();
            stains[i].sharedMaterial = inkStainMaterial;
            stains[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        var mpb = new MaterialPropertyBlock();
        for (float t = 0f; t < 1.9f; t += Time.deltaTime)
        {
            mpb.SetColor(BaseColorId, new Color(inkColor.r, inkColor.g, inkColor.b, 0.85f * (1f - Mathf.Clamp01((t - 0.8f) / 1.1f))));
            foreach (var s in stains)
                if (s != null)
                    s.SetPropertyBlock(mpb);
            yield return null;
        }
    }

    // 공격이 맞은 순간 아주 잠깐 멈칫
    private IEnumerator HitStop()
    {
        hitStopping = true;
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(hitStopDuration);
        Time.timeScale = 1f;
        hitStopping = false;
    }

    private bool hitStopping;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
    private const float SlashDrawTime = 0.08f;

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
        if (hitStopping)
        {
            Time.timeScale = 1f;   // 멈칫 도중 꺼져도 게임이 느린 채로 남지 않게
            hitStopping = false;
        }
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
