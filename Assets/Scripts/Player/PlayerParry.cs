using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerParry : MonoBehaviour
{
    [SerializeField] private float parryWindow = 0.2f;
    [SerializeField] private float parryRange = 2f;
    [SerializeField] private float parryCooldown = 0.5f;
    [SerializeField] private LayerMask enemyLayers = ~0;

    [Header("Visual")]
    [SerializeField] private WeaponSwing weaponSwing;

    private float parryTimer;
    private float cooldownTimer;

    private void Update()
    {
        cooldownTimer -= Time.deltaTime;
        parryTimer -= Time.deltaTime;

        if (cooldownTimer <= 0f && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            parryTimer = parryWindow;
            cooldownTimer = parryCooldown;

            if (weaponSwing != null)
                weaponSwing.PlayBlock(parryWindow);
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
                break;
            }
        }
    }
}
