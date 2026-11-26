using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// 잡기 (2스테이지 궁녀 두 번째 무리): 팔을 뻗어 예고한 뒤 앞의 주인공을 붙잡는다.
// 잡히면 움직일 수 없고 조금씩 피해를 입는다. 우클릭(패링)을 연타하면 뿌리치고 궁녀가 휘청인다.
[RequireComponent(typeof(EnemyHealth))]
public class EnemyGrabAttack : MonoBehaviour, IEnemyAttack
{
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float grabRange = 2.2f;
    [SerializeField] private float grabAngle = 90f;
    [SerializeField] private float telegraph = 0.7f;
    [SerializeField] private float holdTime = 1.8f;
    [SerializeField] private float tickDamage = 5f;
    [SerializeField] private float tickInterval = 0.5f;
    [SerializeField] private int escapePresses = 4;
    [SerializeField] private float staggerTime = 1.2f;
    [SerializeField] private float cooldown = 2.5f;
    [SerializeField] private Color telegraphColor = new Color(1f, 0.5f, 0.2f, 1f);

    public bool IsBusy { get; private set; }
    public float AttackRange => attackRange;

    private EnemyHealth health;
    private Transform player;
    private PlayerMovement playerMovement;
    private PlayerHealth playerHealth;
    private RendererTint tint;
    private ScreenPrompt prompt;
    private float cooldownTimer = 1f;
    private bool holding;

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

    private void Start() => prompt = ScreenPrompt.Create(transform);

    private void Update()
    {
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;
        if (IsBusy || player == null || health.IsDead || cooldownTimer > 0f) return;
        Vector3 to = player.position - transform.position; to.y = 0f;
        if (to.magnitude <= attackRange) StartCoroutine(Grab());
    }

    private IEnumerator Grab()
    {
        IsBusy = true;
        Face();
        for (float t = 0f; t < telegraph; t += Time.deltaTime)   // 팔을 뻗으며 달아오름
        {
            if (health.IsDead) { Finish(); yield break; }
            if (tint.IsValid) tint.Set(Color.Lerp(tint.BaseColor, telegraphColor, t / telegraph));
            yield return null;
        }
        if (tint.IsValid) tint.Restore();

        Vector3 to = player.position - transform.position; to.y = 0f;
        bool inFront = to.magnitude <= grabRange && Vector3.Angle(transform.forward, to) <= grabAngle * 0.5f;
        if (!inFront || (playerMovement != null && playerMovement.IsInvulnerable) || (playerHealth != null && playerHealth.IsDead))
        { Finish(); yield break; }

        // 붙잡음: 주인공을 궁녀 앞에 붙들어 둠
        holding = true;
        WorldTransition.SetPlayerControl(playerMovement, false);
        Vector3 hold = transform.position + transform.forward * 0.9f; hold.y = player.position.y;
        int presses = 0;
        float tick = tickInterval;
        bool escaped = false;
        for (float t = 0f; t < holdTime; t += Time.deltaTime)
        {
            if (health.IsDead || (playerHealth != null && playerHealth.IsDead)) break;
            player.position = Vector3.Lerp(player.position, hold, Time.deltaTime * 12f);
            prompt.Show("우클릭 연타로 뿌리치기  " + new string('●', presses) + new string('○', escapePresses - presses));
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame && ++presses >= escapePresses)
            { escaped = true; break; }
            tick -= Time.deltaTime;
            if (tick <= 0f) { tick = tickInterval; playerHealth?.TakeDamage(tickDamage); }
            yield return null;
        }
        Release();

        if (escaped && !health.IsDead)
        {
            ThirdPersonCamera.Instance?.Shake(0.15f, 0.2f);
            transform.position -= transform.forward * 0.8f;   // 뿌리쳐져 뒤로 밀림
            if (tint.IsValid) tint.Set(Color.cyan);
            yield return new WaitForSeconds(staggerTime);
            if (tint.IsValid) tint.Restore();
        }
        Finish();
    }

    private void Release()
    {
        if (!holding) return;
        holding = false;
        prompt.Hide();
        if (playerHealth == null || !playerHealth.IsDead)
            WorldTransition.SetPlayerControl(playerMovement, true);
    }

    private void Finish()
    {
        if (tint.IsValid) tint.Restore();
        cooldownTimer = cooldown;
        IsBusy = false;
    }

    private void Face()
    {
        Vector3 to = player.position - transform.position; to.y = 0f;
        if (to.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
    }

    private void OnDisable() => Release();   // 붙잡은 채 사라져도 주인공이 묶여 있지 않게
}
