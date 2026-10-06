using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    [Header("Attack (현재: 1단계 버드나무 가지)")]
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackRadius = 1f;
    [SerializeField] private float attackCooldown = 0.5f;
    [SerializeField] private LayerMask enemyLayers = ~0;

    [Header("Visual")]
    [SerializeField] private WeaponSwing weaponSwing;

    [Header("References")]
    [SerializeField] private PlayerMovement playerMovement;

    private float cooldownTimer;

    private void Awake()
    {
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        cooldownTimer -= Time.deltaTime;

        bool isDodging = playerMovement != null && playerMovement.IsDodging;

        if (!isDodging && cooldownTimer <= 0f && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Attack();
            cooldownTimer = attackCooldown;
        }
    }

    private void Attack()
    {
        if (weaponSwing != null)
            weaponSwing.PlaySwing();

        Vector3 attackPoint = transform.position + transform.forward * attackRange;
        Collider[] hits = Physics.OverlapSphere(attackPoint, attackRadius, enemyLayers);

        foreach (Collider hit in hits)
        {
            EnemyHealth enemyHealth = hit.GetComponentInParent<EnemyHealth>();
            if (enemyHealth != null)
                enemyHealth.TakeDamage(attackDamage);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 attackPoint = transform.position + transform.forward * attackRange;
        Gizmos.DrawWireSphere(attackPoint, attackRadius);
    }
}
