using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    private const float HitDistance = 0.6f;
    private const float Lifetime = 5f;

    private Vector3 direction;
    private float speed;
    private float damage;
    private float remainingLifetime = Lifetime;
    private Transform target;
    private PlayerMovement playerMovement;
    private PlayerHealth playerHealth;

    public void Launch(Vector3 launchDirection, float launchSpeed, float launchDamage)
    {
        direction = launchDirection;
        speed = launchSpeed;
        damage = launchDamage;

        GameObject playerObject = GameObject.Find("Player");
        if (playerObject != null)
        {
            target = playerObject.transform;
            playerMovement = playerObject.GetComponent<PlayerMovement>();
            playerHealth = playerObject.GetComponent<PlayerHealth>();
        }

        BuildVisual();
    }

    private void BuildVisual()
    {
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.transform.SetParent(transform, false);
        visual.transform.localScale = Vector3.one * 0.3f;
        Destroy(visual.GetComponent<Collider>());

        Renderer visualRenderer = visual.GetComponent<Renderer>();
        if (visualRenderer != null)
            visualRenderer.material.color = new Color(1f, 0.4f, 0f);
    }

    private void Update()
    {
        transform.position += direction * speed * Time.deltaTime;

        remainingLifetime -= Time.deltaTime;
        if (remainingLifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        if (target == null)
            return;

        if (Vector3.Distance(transform.position, target.position) <= HitDistance)
        {
            bool playerInvulnerable = playerMovement != null && playerMovement.IsInvulnerable;
            if (!playerInvulnerable)
                playerHealth?.TakeDamage(damage);

            Destroy(gameObject);
        }
    }
}
