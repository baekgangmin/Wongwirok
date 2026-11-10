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
    private ParticleSystem impactPrefab;
    private Color impactColor = new Color(1f, 0.45f, 0.1f, 1f);
    private Transform owner;

    public void Launch(Vector3 launchDirection, float launchSpeed, float launchDamage)
    {
        Launch(launchDirection, launchSpeed, launchDamage, null, null, impactColor, null);
    }

    // visualPrefab: 날아가는 불덩이 모양 (없으면 기본 공), impact: 터지는 효과
    public void Launch(Vector3 launchDirection, float launchSpeed, float launchDamage,
                       GameObject visualPrefab, ParticleSystem impact, Color color, Transform shooter)
    {
        direction = launchDirection;
        speed = launchSpeed;
        damage = launchDamage;
        impactPrefab = impact;
        impactColor = color;
        owner = shooter;

        GameObject playerObject = GameObject.Find("Player");
        if (playerObject != null)
        {
            target = playerObject.transform;
            playerMovement = playerObject.GetComponent<PlayerMovement>();
            playerHealth = playerObject.GetComponent<PlayerHealth>();
        }

        if (visualPrefab != null)
        {
            GameObject v = Instantiate(visualPrefab, transform);
            v.transform.localPosition = Vector3.zero;
            v.transform.localRotation = Quaternion.identity;
        }
        else
            BuildFallbackVisual();
    }

    private void BuildFallbackVisual()
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
        Vector3 from = transform.position;
        Vector3 step = direction * speed * Time.deltaTime;

        // 땅이나 벽에 닿으면 터진다 (플레이어·적 자신은 제외)
        if (Physics.Raycast(from, direction, out RaycastHit hit, step.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            bool isPlayer = target != null && hit.collider.transform.IsChildOf(target);
            bool isOwner = owner != null && hit.collider.transform.IsChildOf(owner);
            bool isEnemy = hit.collider.GetComponentInParent<EnemyHealth>() != null;
            if (!isPlayer && !isOwner && !isEnemy)
            {
                transform.position = hit.point;
                Explode();
                return;
            }
        }

        transform.position = from + step;

        remainingLifetime -= Time.deltaTime;
        if (remainingLifetime <= 0f)
        {
            Explode();
            return;
        }

        if (target == null)
            return;

        if (Vector3.Distance(transform.position, target.position) <= HitDistance)
        {
            bool playerInvulnerable = playerMovement != null && playerMovement.IsInvulnerable;
            if (!playerInvulnerable)
                playerHealth?.TakeDamage(damage);

            Explode();
        }
    }

    private void Explode()
    {
        if (impactPrefab != null)
        {
            ParticleSystem fx = Instantiate(impactPrefab, transform.position, Quaternion.LookRotation(-direction));
            fx.transform.localScale *= 1.3f;
            foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>())
            {
                var main = ps.main;
                main.startColor = impactColor;
            }
            fx.Play(true);
            Destroy(fx.gameObject, 1.5f);
        }

        // 꼬리(트레일·연기)가 바로 끊기지 않도록 떼어 내서 잠깐 남긴다
        foreach (var ps in GetComponentsInChildren<ParticleSystem>())
        {
            ps.transform.SetParent(null, true);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(ps.gameObject, 1f);
        }
        foreach (var tr in GetComponentsInChildren<TrailRenderer>())
        {
            tr.transform.SetParent(null, true);
            tr.emitting = false;
            Destroy(tr.gameObject, tr.time + 0.1f);
        }
        Destroy(gameObject);
    }
}
