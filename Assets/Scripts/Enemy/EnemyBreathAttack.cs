using System.Collections;
using UnityEngine;

// 불 뱉기 (2스테이지 궁녀 세 번째 무리): 고개를 젖히며 예고한 뒤 앞쪽 부채꼴로 잠깐 불을 뿜는다.
// 불길은 붓 그림 불(플립북) 덩이를 입에서 연달아 내보내 그린다. 불 안에 있으면 조금씩 피해.
[RequireComponent(typeof(EnemyHealth))]
public class EnemyBreathAttack : MonoBehaviour, IEnemyAttack
{
    [SerializeField] private float attackRange = 3.5f;
    [SerializeField] private float reach = 4.5f;
    [SerializeField] private float coneAngle = 50f;
    [SerializeField] private float telegraph = 0.8f;
    [SerializeField] private float breathTime = 1.2f;
    [SerializeField] private float tickDamage = 5f;
    [SerializeField] private float tickInterval = 0.25f;
    [SerializeField] private float cooldown = 3f;
    [Tooltip("불길 한 덩이 (FireFlipbook 이 붙은 프리팹)")]
    [SerializeField] private GameObject puffPrefab;
    [SerializeField] private float puffSpeed = 7f;
    [SerializeField] private float mouthHeight = 0.6f;
    [SerializeField] private Color telegraphColor = new Color(1f, 0.45f, 0.15f, 1f);

    public bool IsBusy { get; private set; }
    public float AttackRange => attackRange;

    private EnemyHealth health;
    private Transform player;
    private PlayerMovement playerMovement;
    private PlayerHealth playerHealth;
    private RendererTint tint;
    private float cooldownTimer = 1.5f;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
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
        if (IsBusy || player == null || health.IsDead || cooldownTimer > 0f) return;
        Vector3 to = player.position - transform.position; to.y = 0f;
        if (to.magnitude <= attackRange) StartCoroutine(Breathe());
    }

    private IEnumerator Breathe()
    {
        IsBusy = true;
        Vector3 to = player.position - transform.position; to.y = 0f;
        if (to.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
        for (float t = 0f; t < telegraph; t += Time.deltaTime)   // 입 안에 불이 차오름
        {
            if (health.IsDead) { Finish(); yield break; }
            if (tint.IsValid) tint.Set(Color.Lerp(tint.BaseColor, telegraphColor, t / telegraph));
            yield return null;
        }

        float spawn = 0f, tick = 0f;
        for (float t = 0f; t < breathTime && !health.IsDead; t += Time.deltaTime)
        {
            spawn -= Time.deltaTime;
            if (spawn <= 0f && puffPrefab != null) { spawn = 0.06f; StartCoroutine(Puff()); }
            tick -= Time.deltaTime;
            if (tick <= 0f)
            {
                tick = tickInterval;
                Vector3 d = player.position - transform.position; d.y = 0f;
                bool inCone = d.magnitude <= reach && Vector3.Angle(transform.forward, d) <= coneAngle * 0.5f;
                if (inCone && (playerMovement == null || !playerMovement.IsInvulnerable))
                    playerHealth?.TakeDamage(tickDamage);
            }
            yield return null;
        }
        Finish();
    }

    // 입에서 나간 불 한 덩이: 부채꼴 안 무작위 방향으로 날아가며 커졌다 사라짐
    private IEnumerator Puff()
    {
        Vector3 start = transform.position + Vector3.up * mouthHeight + transform.forward * 0.4f;
        Vector3 dir = Quaternion.AngleAxis(Random.Range(-coneAngle, coneAngle) * 0.4f, Vector3.up) * transform.forward;
        var go = Instantiate(puffPrefab, start, Quaternion.identity);
        float life = reach / puffSpeed;
        Destroy(go, life + 0.1f);   // 궁녀가 사라져 코루틴이 멈춰도 불덩이가 남지 않게
        for (float t = 0f; t < life && go != null; t += Time.deltaTime)
        {
            float k = t / life;
            go.transform.position = start + dir * puffSpeed * t + Vector3.down * 0.5f * k;   // 바닥 쪽으로 살짝 처짐
            go.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.3f, k);
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    private void Finish()
    {
        if (tint.IsValid) tint.Restore();
        cooldownTimer = cooldown;
        IsBusy = false;
    }
}
