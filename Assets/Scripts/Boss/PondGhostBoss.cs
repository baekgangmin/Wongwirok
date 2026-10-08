using System.Collections;
using UnityEngine;

[RequireComponent(typeof(EnemyHealth))]
public class PondGhostBoss : MonoBehaviour
{
    [Header("Detection")]
    // 보스는 움직이지 않는 고정형 보스라, 이 범위 안에만 들어오면(호수 대부분을 커버) 어디서든
    // 패턴을 시도한다. 수면 위 물기둥/촉수처럼 사거리가 먼 패턴으로 이동을 대신한다.
    [SerializeField] private float detectionRange = 30f;

    [Header("Pattern 1: Water Pillar")]
    [SerializeField] private float pillarTelegraphDuration = 1f;
    [SerializeField] private float pillarDamage = 15f;
    [SerializeField] private float pillarRadius = 1.5f;

    [Header("Pattern 2: Hair Grab")]
    [SerializeField] private float grabRange = 4f;
    [SerializeField] private float grabTelegraphDuration = 0.8f;
    [SerializeField] private float restrainDuration = 2.5f;

    [Header("Pattern 3: Tentacle Strike")]
    [SerializeField] private float tentacleRange = 14f;
    [SerializeField] private float tentacleTelegraphDuration = 0.9f;
    [SerializeField] private float tentacleDamage = 12f;
    [SerializeField] private int tentacleCount = 3;

    [Header("Phase 2")]
    [SerializeField] private float phase2HealthRatio = 0.5f;

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Renderer targetRenderer;

    private EnemyHealth health;
    private PlayerMovement playerMovement;
    private PlayerHealth playerHealth;
    private Color baseColor;
    private bool isBusy;
    private bool phase2Triggered;

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
        if (player != null)
        {
            playerMovement = player.GetComponent<PlayerMovement>();
            playerHealth = player.GetComponent<PlayerHealth>();
        }

        health.OnDamaged += HandleDamaged;
    }

    private void OnDestroy()
    {
        health.OnDamaged -= HandleDamaged;
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

    private IEnumerator EnterPhase2()
    {
        isBusy = true;
        SetColor(Color.red);
        yield return new WaitForSeconds(1f);

        SetColor(baseColor);
        isBusy = false;
    }

    private void Update()
    {
        if (health.IsDead || isBusy || player == null)
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

        float roll = Random.value;
        if (roll < 0.34f)
            yield return WaterPillarPattern();
        else if (roll < 0.67f)
            yield return HairGrabPattern();
        else
            yield return TentacleAttackPattern();

        isBusy = false;
    }

    // 보스 등 쪽에서 촉수 여러 개가 부채꼴로 뻗어나와 플레이어 방향을 후려치는 원거리 패턴.
    // 보스가 움직이지 않는 대신 사거리가 긴 공격으로 거리를 커버한다.
    private IEnumerator TentacleAttackPattern()
    {
        GameObject[] tentacles = new GameObject[tentacleCount];
        for (int i = 0; i < tentacleCount; i++)
            tentacles[i] = CreateTentacleIndicator(i, tentacleCount);

        yield return new WaitForSeconds(tentacleTelegraphDuration);

        for (int i = 0; i < tentacles.Length; i++)
        {
            if (tentacles[i] != null)
                Destroy(tentacles[i]);
        }

        float distance = Vector3.Distance(transform.position, player.position);
        bool playerInvulnerable = playerMovement != null && playerMovement.IsInvulnerable;

        if (distance <= tentacleRange && !playerInvulnerable)
            playerHealth?.TakeDamage(tentacleDamage);

        yield return new WaitForSeconds(0.6f);
    }

    private GameObject CreateTentacleIndicator(int index, int total)
    {
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        toPlayer = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : transform.forward;

        float spreadAngleDeg = (index - (total - 1) / 2f) * 20f;
        Vector3 direction = Quaternion.Euler(0f, spreadAngleDeg, 0f) * toPlayer;

        float length = tentacleRange;
        Vector3 start = transform.position + Vector3.up * 1f - toPlayer * 1.3f;
        Vector3 end = start + direction * length;

        GameObject tentacle = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        tentacle.name = "TentacleIndicator";
        Destroy(tentacle.GetComponent<Collider>());
        tentacle.transform.position = (start + end) * 0.5f;
        tentacle.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
        tentacle.transform.localScale = new Vector3(0.25f, length * 0.5f, 0.25f);

        Renderer tentacleRenderer = tentacle.GetComponent<Renderer>();
        if (tentacleRenderer != null)
            tentacleRenderer.material.color = new Color(0.12f, 0.3f, 0.2f, 1f);

        return tentacle;
    }

    private IEnumerator WaterPillarPattern()
    {
        Vector3 targetPoint = player.position;
        GameObject indicator = CreateGroundIndicator(targetPoint, pillarRadius);

        yield return new WaitForSeconds(pillarTelegraphDuration);
        Destroy(indicator);

        GameObject pillar = CreatePillarEffect(targetPoint);

        Vector2 playerFlat = new Vector2(player.position.x, player.position.z);
        Vector2 targetFlat = new Vector2(targetPoint.x, targetPoint.z);
        bool playerInvulnerable = playerMovement != null && playerMovement.IsInvulnerable;

        if (Vector2.Distance(playerFlat, targetFlat) <= pillarRadius && !playerInvulnerable)
            playerHealth?.TakeDamage(pillarDamage);

        Destroy(pillar, 0.3f);
        yield return new WaitForSeconds(0.5f);
    }

    private IEnumerator HairGrabPattern()
    {
        SetColor(new Color(0.35f, 0.1f, 0.4f));
        yield return new WaitForSeconds(grabTelegraphDuration);
        SetColor(baseColor);

        float distance = Vector3.Distance(transform.position, player.position);
        bool playerInvulnerable = playerMovement != null && playerMovement.IsInvulnerable;

        if (distance <= grabRange && !playerInvulnerable)
        {
            Debug.Log($"{name}: 머리카락으로 플레이어를 붙잡음 (공격 입력으로 풀 수 있음)");
            playerMovement?.ApplyRestrain(restrainDuration);
        }

        yield return new WaitForSeconds(0.5f);
    }

    private GameObject CreateGroundIndicator(Vector3 position, float radius)
    {
        GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        indicator.name = "PillarIndicator";
        Destroy(indicator.GetComponent<Collider>());
        indicator.transform.position = position + Vector3.up * 0.02f;
        indicator.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);

        Renderer indicatorRenderer = indicator.GetComponent<Renderer>();
        if (indicatorRenderer != null)
            indicatorRenderer.material.color = new Color(0.2f, 0.6f, 1f, 0.6f);

        return indicator;
    }

    private GameObject CreatePillarEffect(Vector3 position)
    {
        GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillar.name = "WaterPillarEffect";
        Destroy(pillar.GetComponent<Collider>());
        pillar.transform.position = position + Vector3.up * 1.5f;
        pillar.transform.localScale = new Vector3(pillarRadius * 1.2f, 1.5f, pillarRadius * 1.2f);

        Renderer pillarRenderer = pillar.GetComponent<Renderer>();
        if (pillarRenderer != null)
            pillarRenderer.material.color = new Color(0.3f, 0.7f, 1f, 0.9f);

        return pillar;
    }

    private void SetColor(Color color)
    {
        if (targetRenderer != null)
            targetRenderer.material.color = color;
    }
}
