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

    [Header("On-Hit Effect")]
    [SerializeField] private bool slowsPlayerInsteadOfDamage = false;
    [SerializeField] private float slowMultiplier = 0.5f;
    [SerializeField] private float slowDuration = 2f;

    [Header("Colors")]
    [SerializeField] private Color telegraphColor = new Color(1f, 0.5f, 0f);
    [SerializeField] private Color parriedColor = Color.cyan;

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
    private Coroutine attackRoutine;
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
        if (player != null)
        {
            playerMovement = player.GetComponent<PlayerMovement>();
            playerHealth = player.GetComponent<PlayerHealth>();
        }
    }

    private void Update()
    {
        if (health.IsDead || isBusy || player == null)
            return;

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

        StartCoroutine(StaggerRoutine());
        return true;
    }

    private IEnumerator AttackRoutine()
    {
        SetColor(telegraphColor);
        yield return new WaitForSeconds(telegraphDuration);

        IsAttackActive = true;
        yield return new WaitForSeconds(activeDuration);
        IsAttackActive = false;

        bool playerInvulnerable = playerMovement != null && playerMovement.IsInvulnerable;
        if (playerInvulnerable)
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
    }

    private IEnumerator StaggerRoutine()
    {
        SetColor(parriedColor);
        Debug.Log($"{name}: 패링 당함!");
        yield return new WaitForSeconds(staggerDuration);

        SetColor(baseColor);
        isBusy = false;
    }

    private void SetColor(Color color)
    {
        if (targetRenderer != null)
            targetRenderer.material.color = color;
    }
}
