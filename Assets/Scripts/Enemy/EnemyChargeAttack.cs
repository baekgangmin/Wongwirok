using System.Collections;
using UnityEngine;

// 돌진 (2스테이지 소금 덩이 귀): 주인공이 적당히 멀면 몸을 떨며 힘을 모았다가 그 방향으로 곧게 돌진한다.
// 부딪히면 큰 피해, 벽에 박으면 잠깐 멍하니 서 있다 (반격 기회). 가까우면 EnemyAttack(연타)이 맡는다.
[RequireComponent(typeof(EnemyHealth))]
public class EnemyChargeAttack : MonoBehaviour
{
    [SerializeField] private float minRange = 4f;
    [SerializeField] private float maxRange = 11f;
    [SerializeField] private float windup = 0.9f;
    [SerializeField] private float speed = 11f;
    [SerializeField] private float maxDistance = 12f;
    [SerializeField] private float damage = 22f;
    [SerializeField] private float hitRadius = 1.1f;
    [SerializeField] private float wallStun = 1.6f;
    [SerializeField] private float cooldown = 5f;
    [SerializeField] private Color windupColor = new Color(1f, 0.55f, 0.3f, 1f);
    [SerializeField] private Color stunColor = new Color(0.55f, 0.55f, 0.6f, 1f);

    private EnemyHealth health;
    private EnemyAttack melee;
    private EnemyChase chase;
    private Transform player;
    private PlayerMovement playerMovement;
    private PlayerHealth playerHealth;
    private RendererTint tint;
    private float cooldownTimer = 2f;
    private bool charging;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        melee = GetComponent<EnemyAttack>();
        chase = GetComponent<EnemyChase>();
        var p = GameObject.Find("Player");
        if (p != null)
        {
            player = p.transform;
            playerMovement = p.GetComponent<PlayerMovement>();
            playerHealth = p.GetComponent<PlayerHealth>();
        }
        tint = new RendererTint(GetComponentInChildren<Renderer>());
    }

    private void Update()
    {
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;
        if (charging || player == null || health.IsDead || cooldownTimer > 0f) return;
        if (melee != null && melee.IsBusy) return;
        Vector3 to = player.position - transform.position; to.y = 0f;
        float d = to.magnitude;
        if (d < minRange || d > maxRange) return;
        StartCoroutine(Charge());
    }

    private IEnumerator Charge()
    {
        charging = true;
        SetOthers(false);
        Vector3 dir = player.position - transform.position; dir.y = 0f; dir.Normalize();
        transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

        // 힘 모으기: 몸이 떨리며 달아오름
        Transform vis = transform.childCount > 0 ? FindVisual() : null;
        Vector3 visPos = vis != null ? vis.localPosition : Vector3.zero;
        for (float t = 0f; t < windup; t += Time.deltaTime)
        {
            if (health.IsDead) { End(vis, visPos); yield break; }
            if (vis != null) vis.localPosition = visPos + (Vector3)(Random.insideUnitCircle * 0.06f * (t / windup));
            if (tint.IsValid) tint.Set(Color.Lerp(tint.BaseColor, windupColor, t / windup));
            yield return null;
        }
        if (vis != null) vis.localPosition = visPos;

        bool hitPlayer = false, hitWall = false;
        for (float moved = 0f; moved < maxDistance && !health.IsDead;)
        {
            float step = speed * Time.deltaTime;
            Vector3 origin = transform.position + Vector3.up * 0.2f;
            if (Physics.SphereCast(origin, 0.5f, dir, out RaycastHit h, step + 0.3f, ~0, QueryTriggerInteraction.Ignore)
                && !h.collider.transform.IsChildOf(transform) && !h.collider.transform.IsChildOf(player)
                && h.collider.GetComponentInParent<EnemyHealth>() == null)
            { hitWall = true; break; }
            transform.position += dir * step;
            moved += step;
            Vector3 toP = player.position - transform.position; toP.y = 0f;
            if (!hitPlayer && toP.magnitude < hitRadius)
            {
                hitPlayer = true;
                if (playerMovement == null || !playerMovement.IsInvulnerable)
                    playerHealth?.TakeDamage(damage);
            }
            yield return null;
        }

        if (hitWall && !health.IsDead)
        {
            ThirdPersonCamera.Instance?.Shake(0.2f, 0.25f);
            if (tint.IsValid) tint.Set(stunColor);
            yield return new WaitForSeconds(wallStun);   // 벽에 박아 멍함 → 반격 기회
        }
        End(vis, visPos);
    }

    private void End(Transform vis, Vector3 visPos)
    {
        if (vis != null) vis.localPosition = visPos;
        if (tint.IsValid) tint.Restore();
        SetOthers(true);
        cooldownTimer = cooldown;
        charging = false;
    }

    private void SetOthers(bool on)
    {
        if (melee != null) melee.enabled = on;
        if (chase != null) chase.enabled = on;
    }

    private Transform FindVisual()
    {
        foreach (Transform c in transform)
            if (c.name.StartsWith("Visual")) return c;
        return null;
    }
}
