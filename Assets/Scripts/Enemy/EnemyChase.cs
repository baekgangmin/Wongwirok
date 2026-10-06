using UnityEngine;

public class EnemyChase : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float detectionRange = 8f;

    [SerializeField] private Transform player;

    private EnemyHealth health;
    private EnemyAttack enemyAttack;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        enemyAttack = GetComponent<EnemyAttack>();

        if (player == null)
        {
            GameObject playerObject = GameObject.Find("Player");
            if (playerObject != null)
                player = playerObject.transform;
        }
    }

    private void Update()
    {
        if (player == null || (health != null && health.IsDead))
            return;

        if (enemyAttack != null && enemyAttack.IsBusy)
            return;

        float stoppingDistance = enemyAttack != null ? enemyAttack.AttackRange : 2f;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;

        if (distance > detectionRange || distance <= stoppingDistance)
            return;

        Vector3 direction = toPlayer.normalized;
        float moveDistance = Mathf.Min(moveSpeed * Time.deltaTime, distance - stoppingDistance);
        transform.position += direction * moveDistance;
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }
}
