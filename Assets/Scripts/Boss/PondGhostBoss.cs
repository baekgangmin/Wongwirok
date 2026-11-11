using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EnemyHealth))]
public class PondGhostBoss : MonoBehaviour
{
    [Header("Detection")]
    // 보스는 움직이지 않는 고정형 보스라, 이 범위 안에만 들어오면(호수 대부분을 커버) 어디서든
    // 패턴을 시도한다. 수면 위 물기둥/머리카락처럼 사거리가 먼 패턴으로 이동을 대신한다.
    [SerializeField] private float detectionRange = 30f;

    [Header("Pattern 1: Water Pillar")]
    [SerializeField] private float pillarTelegraphDuration = 1f;
    [SerializeField] private float pillarDamage = 15f;
    [SerializeField] private float pillarRadius = 1.5f;
    [Tooltip("갇혔을 때 떠오르는 높이 / 걸리는 시간 / 위에서 머무는 시간 / 들썩임 크기")]
    [SerializeField] private float pillarRiseHeight = 1.2f;
    [SerializeField] private float pillarRiseTime = 0.5f;
    [SerializeField] private float pillarHoldTime = 1.8f;
    [SerializeField] private float pillarBobAmplitude = 0.12f;
    [SerializeField] private WaterPillarVisual pillarEffectPrefab;
    [SerializeField] private Material pillarTelegraphMaterial;

    [Header("Pattern 2: 머리카락 순차 공격")]
    [Tooltip("1단계 / 2단계(체력 절반 이하) 머리카락 줄 수")]
    [SerializeField] private int hairCountPhase1 = 3;
    [SerializeField] private int hairCountPhase2 = 5;
    [Tooltip("다음 줄이 시작되기까지의 간격(초)")]
    [SerializeField] private float hairInterval = 0.35f;
    [SerializeField] private float hairIntervalPhase2 = 0.28f;
    [Tooltip("공격 동작을 시작하고 첫 줄 예고가 나오기까지")]
    [SerializeField] private float hairWindup = 0.4f;
    [Tooltip("바닥 예고선이 차오르는 시간")]
    [SerializeField] private float hairTelegraphDuration = 0.5f;
    [SerializeField] private float hairExtendTime = 0.12f;
    [Tooltip("머리카락이 뻗은 뒤 판정이 유지되는 시간 (이 사이에 패링 가능, 끝에 피해)")]
    [SerializeField] private float hairActiveDuration = 0.25f;
    [SerializeField] private float hairRetractTime = 0.25f;
    [SerializeField] private float hairDamage = 12f;
    [SerializeField] private float hairRange = 14f;
    [Tooltip("플레이어 위치보다 이만큼 더 길게 뻗음 (선 따라 도망쳐도 맞도록)")]
    [SerializeField] private float hairOvershoot = 1.5f;
    [Tooltip("판정 반폭 (예고선 폭의 절반)")]
    [SerializeField] private float hairHitHalfWidth = 0.6f;
    [SerializeField] private float hairArcHeight = 2f;
    [Tooltip("머리카락 뿌리: 가슴에서 등 뒤로 떨어진 거리 / 등 뒤로 솟구치는 높이 / 줄마다 좌우로 벌어지는 정도")]
    [SerializeField] private float hairBackOffset = 0.7f;
    [SerializeField] private float hairBackRise = 3f;
    [SerializeField] private float hairBackSpread = 0.6f;
    [Tooltip("등 뒤로 뻗어 나가는 거리 / 어깨 바깥쪽으로 돌아 나오는 폭")]
    [SerializeField] private float hairBackReach = 2.5f;
    [SerializeField] private float hairSideSwing = 2.6f;
    [Tooltip("3D 머리카락 촉수 모델 (곧게 뻗은 촉수). 비우면 선으로 그린 머리카락을 쓴다")]
    [SerializeField] private GameObject hairTentacleModel;
    [Tooltip("촉수 재질 (비우면 모델에 들어 있는 재질)")]
    [SerializeField] private Material hairTentacleMaterial;
    [Tooltip("촉수 뿌리 쪽 굵기 (반지름, m)")]
    [SerializeField] private float hairTentacleRadius = 0.45f;
    [SerializeField] private Material hairMaterial;
    [SerializeField] private Material hairTelegraphMaterial;

    [Header("머리카락 마지막 줄: 패링 가능 / 2단계에선 붙잡기")]
    [SerializeField] private bool lastHairParryable = true;
    [SerializeField] private float hairParryStagger = 2f;
    [SerializeField] private Color parriedColor = Color.cyan;
    [SerializeField] private float restrainDuration = 2.5f;

    [Header("Phase 2")]
    [SerializeField] private float phase2HealthRatio = 0.5f;
    [Tooltip("포효 동작 시작 후 정점(물기둥이 솟고 화면이 흔들리는 순간)까지")]
    [SerializeField] private float phase2RoarPeak = 1.0f;
    [SerializeField] private float phase2ShakeAmplitude = 0.35f;
    [SerializeField] private float phase2ShakeDuration = 0.8f;
    [SerializeField] private int phase2PillarCount = 6;
    [SerializeField] private float phase2PillarRadius = 5.5f;
    [SerializeField] private float phase2PillarHold = 1.0f;
    [SerializeField] private float phase2RecoverTime = 0.8f;
    [Tooltip("2단계 동안 몸 색에 곱하는 값 (붉게 물듦)")]
    [SerializeField] private Color phase2Tint = new Color(1.4f, 0.7f, 0.7f, 1f);

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Animator animator;

    private EnemyHealth health;
    private PlayerMovement playerMovement;
    private PlayerHealth playerHealth;
    private PlayerParry playerParry;
    private Color baseColor;
    private RendererTint tint;
    private bool isBusy;
    private bool phase2Triggered;
    private bool hairParried;
    private bool phase2Pending;
    private EnemyHitReaction hitReaction;
    private readonly HashSet<string> triggerNames = new HashSet<string>();
    private readonly List<GameObject> spawnedLines = new List<GameObject>();

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        hitReaction = GetComponent<EnemyHitReaction>();
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();
        if (targetRenderer != null)
            { tint = new RendererTint(targetRenderer); baseColor = tint.BaseColor; }
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter p in animator.parameters)
                if (p.type == AnimatorControllerParameterType.Trigger)
                    triggerNames.Add(p.name);
        }

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
            playerParry = player.GetComponent<PlayerParry>();
        }

        health.OnDamaged += HandleDamaged;
        health.OnDeath += HandleDeath;
    }

    private void OnDestroy()
    {
        health.OnDamaged -= HandleDamaged;
        health.OnDeath -= HandleDeath;
    }

    // 애니메이터에 해당 신호가 있을 때만 보낸다 (동작이 아직 없는 동안 경고 방지)
    private void TryTrigger(string triggerName)
    {
        if (animator != null && triggerNames.Contains(triggerName))
            animator.SetTrigger(triggerName);
    }

    private void HandleDamaged(float amount)
    {
        if (phase2Triggered || health.MaxHealth <= 0f)
            return;

        if (health.CurrentHealth <= health.MaxHealth * phase2HealthRatio)
        {
            phase2Triggered = true;
            StartCoroutine(EnterPhase2());
        }
    }

    private void HandleDeath()
    {
        StopAllCoroutines();
        foreach (GameObject go in spawnedLines)
            if (go != null)
                Destroy(go);
        spawnedLines.Clear();
        TryTrigger("Dead");
    }

    // 2단계 진입: 포효 → 포효 정점에 화면이 크게 흔들리고 보스 둘레로 물기둥이 한꺼번에 솟음 → 이후 몸이 붉게 물듦
    private IEnumerator EnterPhase2()
    {
        // 지금 하던 패턴이 끝나길 기다렸다가 단계 전환 (그동안 새 패턴은 시작하지 않음)
        phase2Pending = true;
        while (isBusy)
            yield return null;
        isBusy = true;
        phase2Pending = false;

        FacePlayer();
        TryTrigger("Phase2");
        yield return new WaitForSeconds(phase2RoarPeak);

        if (ThirdPersonCamera.Instance != null)
            ThirdPersonCamera.Instance.Shake(phase2ShakeAmplitude, phase2ShakeDuration);
        var ring = new List<WaterPillarVisual>();
        float groundY = player != null ? player.position.y : transform.position.y;
        for (int i = 0; i < phase2PillarCount; i++)
        {
            float a = (360f / phase2PillarCount) * i * Mathf.Deg2Rad;
            Vector3 p = new Vector3(transform.position.x + Mathf.Cos(a) * phase2PillarRadius, groundY, transform.position.z + Mathf.Sin(a) * phase2PillarRadius);
            WaterPillarVisual pillar = CreatePillarEffect(p, 6f);
            if (pillar != null)
                ring.Add(pillar);
        }

        // 몸이 서서히 붉게 물듦 (2단계 동안 유지, 맞을 때 번쩍임이 끝나도 이 색으로 돌아감)
        Color target = tint != null ? tint.BaseColor * phase2Tint : baseColor;
        Color from = baseColor;
        for (float t = 0f; t < 0.6f; t += Time.deltaTime)
        {
            SetColor(Color.Lerp(from, target, t / 0.6f));
            yield return null;
        }
        baseColor = target;
        SetColor(baseColor);
        if (hitReaction != null)
            hitReaction.SetRestColor(baseColor);

        yield return new WaitForSeconds(phase2PillarHold);
        foreach (WaterPillarVisual pillar in ring)
            CollapsePillar(pillar);
        yield return new WaitForSeconds(phase2RecoverTime);

        isBusy = false;
    }

    private void Update()
    {
        if (health.IsDead || isBusy || phase2Pending || player == null)
            return;

        // 보스는 제자리에서 움직이지 않는다 - 플레이어 방향으로 몸만 돌려서 패턴이 자연스럽게 보이게 함.
        FacePlayer();

        float distance = Vector3.Distance(transform.position, player.position);
        if (distance > detectionRange)
            return;

        StartCoroutine(RunRandomPattern());
    }

    private void FacePlayer()
    {
        Vector3 direction = player.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f)
            return;

        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private IEnumerator RunRandomPattern()
    {
        isBusy = true;

        if (Random.value < 0.5f)
            yield return WaterPillarPattern();
        else
            yield return HairBarragePattern();

        isBusy = false;
    }

    // ───────────────────────── 머리카락 순차 공격 ─────────────────────────
    // 머리카락이 한 줄씩, 그 순간의 플레이어 위치를 노려 차례로 내리꽂힌다. 멈춰 있으면 맞는다.
    // 마지막 줄은 패링 가능(성공 시 보스가 휘청이며 때릴 틈). 2단계에서는 마지막 줄에 맞으면 붙잡힌다.
    private IEnumerator HairBarragePattern()
    {
        int count = phase2Triggered ? hairCountPhase2 : hairCountPhase1;
        float interval = phase2Triggered ? hairIntervalPhase2 : hairInterval;
        hairParried = false;

        TryTrigger("HairAttack");
        yield return new WaitForSeconds(hairWindup);

        int running = 0;
        for (int i = 0; i < count; i++)
        {
            if (health.IsDead)
                yield break;
            bool last = i == count - 1;
            running++;
            StartCoroutine(HairStrandRoutine(i, last && lastHairParryable, last && phase2Triggered, () => running--));
            if (!last)
                yield return new WaitForSeconds(interval);
        }
        while (running > 0)
            yield return null;

        if (hairParried)
        {
            TryTrigger("Hit");
            SetColor(parriedColor);
            yield return new WaitForSeconds(hairParryStagger);
            SetColor(baseColor);
        }
        else
        {
            yield return new WaitForSeconds(0.6f);
        }
    }

    private IEnumerator HairStrandRoutine(int strandIndex, bool parryable, bool grabs, System.Action onDone)
    {
        FacePlayer();

        float groundY = player.position.y + 0.05f;
        Vector3 bossFlat = new Vector3(transform.position.x, groundY, transform.position.z);
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        float dist = dir.magnitude;
        dir = dist > 0.01f ? dir / dist : transform.forward;
        float length = Mathf.Clamp(dist + hairOvershoot, 3f, hairRange);
        Vector3 lineStart = bossFlat + dir * 1.5f;
        Vector3 lineEnd = bossFlat + dir * length;

        // 1) 바닥 예고선
        LineRenderer tele = CreateLine("HairTelegraph", hairTelegraphMaterial, hairHitHalfWidth * 2f, hairHitHalfWidth * 2f, 2);
        tele.alignment = LineAlignment.TransformZ;
        tele.transform.rotation = Quaternion.LookRotation(Vector3.up, dir);
        tele.SetPosition(0, lineStart);
        tele.SetPosition(1, lineEnd);
        Material teleMat = tele.material;
        for (float t = 0f; t < hairTelegraphDuration; t += Time.deltaTime)
        {
            if (teleMat != null)
                teleMat.SetFloat("_Fill", t / hairTelegraphDuration);
            yield return null;
        }

        // 2) 머리카락 다발 (가는 띠 3개를 살짝 어긋나게)
        // 보스 등 뒤에서 돋아나 머리 위로 크게 넘어온 뒤 바닥에 내리꽂힌다 (전갈 꼬리처럼).
        // 줄마다 등 왼쪽·가운데·오른쪽으로 뿌리 위치를 바꿔 여러 가닥이 돋아나는 느낌.
        Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        Transform chest = null;
        if (animator != null && animator.isHuman)
        {
            chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (chest == null)
                chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        }
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        // 왼쪽·오른쪽 어깨 너머로 번갈아 돌아 나옴 (정면에서도 등 뒤에서 나오는 게 보이도록)
        float sideSign = (strandIndex % 2 == 0) ? -1f : 1f;
        float rootSide = sideSign * hairBackSpread;
        // 3D 촉수 모델이 있으면 그걸 곡선에 입히고, 없으면 예전처럼 선 3개로 그린다
        BossHairTentacle tentacle = CreateTentacle();
        LineRenderer[] strands = new LineRenderer[tentacle != null ? 0 : 3];
        for (int s = 0; s < strands.Length; s++)
            strands[s] = CreateLine("HairStrand", hairMaterial, 0.55f, 0.08f, 28);

        float[] offsets = { -0.25f, 0f, 0.25f };
        float wobblePhase = strandIndex * 1.7f;
        System.Action<float, Vector3> draw = (reveal, tip) =>
        {
            Vector3 back = -transform.forward;
            Vector3 chestPos = chest != null ? chest.position : transform.position + Vector3.up * 2.5f;
            float headY = head != null ? head.position.y : chestPos.y + 1f;
            Vector3 root = chestPos + back * hairBackOffset + transform.right * rootSide;
            // 등 뒤로 크게 솟구쳤다가(1) 어깨 바깥쪽 위로 돌아 나와(2) 바닥에 꽂힘
            Vector3 c1 = root + back * hairBackReach + transform.right * (sideSign * hairSideSwing * 0.5f) + Vector3.up * hairBackRise;
            Vector3 c2 = new Vector3(transform.position.x, headY + hairArcHeight, transform.position.z)
                         + transform.right * (sideSign * hairSideSwing) + dir * 2f;
            if (tentacle != null)
            {
                // 끝으로 갈수록 살짝 꿈틀거림
                tentacle.Apply(u => Bezier3(root, c1, c2, tip, u)
                    + side * (Mathf.Sin(Time.time * 9f + u * 8f + wobblePhase) * 0.18f * u * (1f - u) * 4f), reveal);
                return;
            }
            for (int s = 0; s < strands.Length; s++)
            {
                LineRenderer lr = strands[s];
                int n = lr.positionCount;
                for (int k = 0; k < n; k++)
                {
                    float u = reveal * k / (n - 1);
                    Vector3 p = Bezier3(root, c1, c2, tip, u);
                    float wobble = Mathf.Sin(Time.time * 18f + k * 0.7f + s * 2.1f) * 0.08f * u;
                    p += side * (offsets[s] * u + wobble);
                    lr.SetPosition(k, p);
                }
            }
        };

        Vector3 tipPos = lineEnd + Vector3.up * 0.15f;
        for (float t = 0f; t < hairExtendTime; t += Time.deltaTime)
        {
            draw(Mathf.SmoothStep(0f, 1f, t / hairExtendTime), tipPos);
            yield return null;
        }

        // 3) 판정 유지 (패링 기회) → 끝에 피해
        bool parried = false;
        for (float t = 0f; t < hairActiveDuration; t += Time.deltaTime)
        {
            draw(1f, tipPos);
            if (parryable && !parried && PlayerInHairZone(lineStart, lineEnd) && playerParry != null
                && playerParry.TryParryExternal(transform.position + Vector3.up * 2f))
            {
                parried = true;
                hairParried = true;
            }
            yield return null;
        }
        if (tele != null)
            Destroy(tele.gameObject);

        bool grabbed = false;
        if (!parried && PlayerInHairZone(lineStart, lineEnd) && !(playerMovement != null && playerMovement.IsInvulnerable))
        {
            if (grabs && playerMovement != null)
            {
                playerMovement.ApplyRestrain(restrainDuration);
                grabbed = true;
            }
            playerHealth?.TakeDamage(grabs ? hairDamage * 0.5f : hairDamage);
        }

        // 붙잡았으면 풀릴 때까지 머리카락이 플레이어를 감고 있음
        while (grabbed && playerMovement != null && playerMovement.IsRestrained && !health.IsDead)
        {
            draw(1f, player.position + Vector3.up * 1f);
            yield return null;
        }
        if (grabbed)
            tipPos = player.position + Vector3.up * 1f;

        // 4) 거둬들이기
        for (float t = 0f; t < hairRetractTime; t += Time.deltaTime)
        {
            draw(1f - t / hairRetractTime, tipPos);
            yield return null;
        }

        foreach (LineRenderer lr in strands)
            if (lr != null)
                Destroy(lr.gameObject);
        if (tentacle != null)
            Destroy(tentacle.gameObject);
        onDone();
    }

    private BossHairTentacle CreateTentacle()
    {
        if (hairTentacleModel == null)
            return null;
        MeshFilter srcFilter = hairTentacleModel.GetComponentInChildren<MeshFilter>();
        MeshRenderer srcRenderer = hairTentacleModel.GetComponentInChildren<MeshRenderer>();
        if (srcFilter == null || srcRenderer == null || srcFilter.sharedMesh == null)
            return null;
        GameObject go = new GameObject("HairTentacle", typeof(MeshFilter), typeof(MeshRenderer));
        spawnedLines.Add(go);
        BossHairTentacle t = go.AddComponent<BossHairTentacle>();
        Material[] mats = hairTentacleMaterial != null ? new[] { hairTentacleMaterial } : srcRenderer.sharedMaterials;
        t.Init(srcFilter.sharedMesh, mats, hairTentacleRadius);
        return t;
    }

    private bool PlayerInHairZone(Vector3 a, Vector3 b)
    {
        Vector2 p = new Vector2(player.position.x, player.position.z);
        Vector2 a2 = new Vector2(a.x, a.z), b2 = new Vector2(b.x, b.z);
        Vector2 ab = b2 - a2;
        float t = Mathf.Clamp01(Vector2.Dot(p - a2, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
        float d = Vector2.Distance(p, a2 + ab * t);
        return d <= hairHitHalfWidth + 0.3f;   // 0.3 = 플레이어 몸 반경
    }

    private static Vector3 Bezier3(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        float u = 1f - t;
        return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
    }

    private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    private LineRenderer CreateLine(string objName, Material mat, float startWidth, float endWidth, int points)
    {
        GameObject go = new GameObject(objName);
        spawnedLines.RemoveAll(x => x == null);
        spawnedLines.Add(go);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = points;
        lr.startWidth = startWidth;
        lr.endWidth = endWidth;
        lr.textureMode = LineTextureMode.Stretch;
        lr.numCapVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        if (mat != null)
            lr.material = new Material(mat);
        for (int i = 0; i < points; i++)
            lr.SetPosition(i, transform.position);
        return lr;
    }

    // ───────────────────────── 물기둥 ─────────────────────────
    // 발밑 예고 → 물기둥이 솟음. 못 피하면 갇혀서 떠올랐다가(위에서 들썩임) 기둥이 무너지며 떨어지고, 착지할 때 피해.
    private IEnumerator WaterPillarPattern()
    {
        TryTrigger("PillarAttack");
        Vector3 targetPoint = player.position;
        GameObject indicator = CreateGroundIndicator(targetPoint, pillarRadius);
        Material ringMat = indicator.GetComponent<Renderer>().material;
        for (float t = 0f; t < pillarTelegraphDuration; t += Time.deltaTime)
        {
            ringMat.SetFloat("_Fill", t / pillarTelegraphDuration);
            yield return null;
        }
        Destroy(indicator);

        Vector2 playerFlat = new Vector2(player.position.x, player.position.z);
        Vector2 targetFlat = new Vector2(targetPoint.x, targetPoint.z);
        bool playerInvulnerable = playerMovement != null && playerMovement.IsInvulnerable;
        bool trapped = Vector2.Distance(playerFlat, targetFlat) <= pillarRadius && !playerInvulnerable && playerMovement != null;

        WaterPillarVisual pillar = CreatePillarEffect(targetPoint, trapped ? pillarRiseHeight + 3f : 3.5f);

        if (!trapped)
        {
            yield return new WaitForSeconds(0.5f);
            CollapsePillar(pillar);
            yield return new WaitForSeconds(0.3f);
            yield break;
        }

        bool landed = false;
        System.Action onLanded = () => landed = true;
        playerMovement.OnTrapLanded += onLanded;
        playerMovement.ApplyWaterTrap(targetPoint, pillarRiseHeight, pillarRiseTime, pillarHoldTime, pillarBobAmplitude);

        // 떨어지기 시작하면 기둥도 무너짐
        while (playerMovement.IsTrapped && !playerMovement.IsTrapFalling && !landed && !health.IsDead)
            yield return null;
        CollapsePillar(pillar);

        float wait = 0f;
        while (!landed && wait < 3f)
        {
            wait += Time.deltaTime;
            yield return null;
        }
        playerMovement.OnTrapLanded -= onLanded;
        playerHealth?.TakeDamage(pillarDamage);

        yield return new WaitForSeconds(0.6f);
    }

    private GameObject CreateGroundIndicator(Vector3 position, float radius)
    {
        GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Quad);
        indicator.name = "PillarIndicator";
        Destroy(indicator.GetComponent<Collider>());
        spawnedLines.Add(indicator);
        indicator.transform.position = position + Vector3.up * 0.04f;
        indicator.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        indicator.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);

        Renderer indicatorRenderer = indicator.GetComponent<Renderer>();
        indicatorRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (pillarTelegraphMaterial != null)
            indicatorRenderer.material = new Material(pillarTelegraphMaterial);

        return indicator;
    }

    private WaterPillarVisual CreatePillarEffect(Vector3 position, float height)
    {
        if (pillarEffectPrefab == null)
            return null;
        WaterPillarVisual pillar = Instantiate(pillarEffectPrefab, position, Quaternion.identity);
        spawnedLines.Add(pillar.gameObject);
        pillar.Play(height, pillarRadius);
        return pillar;
    }

    private static void CollapsePillar(WaterPillarVisual pillar)
    {
        if (pillar != null)
            pillar.Collapse();
    }

    private void SetColor(Color color)
    {
        if (targetRenderer != null)
            tint?.Set(color);
    }
}
