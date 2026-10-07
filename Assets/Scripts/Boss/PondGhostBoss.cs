using System.Collections;
using UnityEngine;

[RequireComponent(typeof(EnemyHealth))]
public class PondGhostBoss : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float detectionRange = 20f;
    [SerializeField] private float patternRange = 6f;

    [Header("Pattern 1: Water Pillar")]
    [SerializeField] private float pillarTelegraphDuration = 1f;
    [SerializeField] private float pillarDamage = 15f;
    [SerializeField] private float pillarRadius = 1.5f;

    [Header("Pattern 2: Hair Grab")]
    [SerializeField] private float grabRange = 3f;
    [SerializeField] private float grabTelegraphDuration = 0.8f;
    [SerializeField] private float restrainDuration = 2.5f;

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

        float distance = Vector3.Distance(transform.position, player.position);
        if (distance > detectionRange)
            return;

        if (distance > patternRange)
        {
            MoveTowardPlayer();
            return;
        }

        StartCoroutine(RunRandomPattern());
    }

    private void MoveTowardPlayer()
    {
        Vector3 direction = player.position - transform.position;
        direction.y = 0f;
        direction.Normalize();

        transform.position += direction * moveSpeed * Time.deltaTime;
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    private IEnumerator RunRandomPattern()
    {
        isBusy = true;

        if (Random.value < 0.5f)
            yield return WaterPillarPattern();
        else
            yield return HairGrabPattern();

        isBusy = false;
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
