using System.Collections;
using UnityEngine;

[RequireComponent(typeof(EnemyHealth))]
public class EnemyRangedAttack : MonoBehaviour, IEnemyAttack
{
    [Header("Attack Timing")]
    [SerializeField] private float attackRange = 8f;
    [SerializeField] private float telegraphDuration = 0.5f;
    [SerializeField] private float cooldownDuration = 1.5f;

    [Header("Projectile")]
    [SerializeField] private float projectileSpeed = 8f;
    [SerializeField] private float projectileDamage = 8f;

    [Header("Colors")]
    [SerializeField] private Color telegraphColor = new Color(1f, 0.3f, 0f);

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Renderer targetRenderer;

    public bool IsBusy => isBusy;
    public float AttackRange => attackRange;

    private EnemyHealth health;
    private Color baseColor;
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
    }

    private void Update()
    {
        if (health.IsDead || isBusy || player == null)
            return;

        if (Vector3.Distance(transform.position, player.position) <= attackRange)
        {
            isBusy = true;
            StartCoroutine(AttackRoutine());
        }
    }

    private IEnumerator AttackRoutine()
    {
        SetColor(telegraphColor);
        yield return new WaitForSeconds(telegraphDuration);

        FireProjectile();

        SetColor(baseColor);
        yield return new WaitForSeconds(cooldownDuration);

        isBusy = false;
    }

    private void FireProjectile()
    {
        if (player == null || health.IsDead)
            return;

        Vector3 spawnPosition = transform.position + Vector3.up * 1f;
        Vector3 direction = (player.position - spawnPosition).normalized;

        GameObject projectileObject = new GameObject("EnemyProjectile");
        projectileObject.transform.position = spawnPosition;

        EnemyProjectile projectile = projectileObject.AddComponent<EnemyProjectile>();
        projectile.Launch(direction, projectileSpeed, projectileDamage);
    }

    private void SetColor(Color color)
    {
        if (targetRenderer != null)
            targetRenderer.material.color = color;
    }
}
