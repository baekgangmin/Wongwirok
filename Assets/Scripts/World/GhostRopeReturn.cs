using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// 귀의 세계: 몸에 감은 밧줄이 들어온 자리 물속으로 이어져 있다.
// 보스를 쓰러뜨린 뒤 E 를 누르면 어디 있든 밧줄이 당겨져 연못으로 끌려가 빨려 들어가고,
// 인간 세계에서 밧줄을 묶었던 나무 밑으로 돌아온다.
public class GhostRopeReturn : MonoBehaviour
{
    [SerializeField] private Material ropeMaterial;
    [SerializeField] private EnemyHealth bossHealth;
    [SerializeField] private string humanScene = "HumanWorld_01";
    [Header("이 씬에서 바로 시작했을 때 쓸 기본값 (테스트용)")]
    [SerializeField] private Vector3 defaultAnchor = new Vector3(6f, -1.57f, 19f);
    [SerializeField] private Vector3 defaultReturnPoint = new Vector3(-1f, 0f, 9f);
    [SerializeField] private float defaultReturnYaw = 20f;
    [Header("끌려가기")]
    [SerializeField] private float dragStartSpeed = 3f;
    [SerializeField] private float dragMaxSpeed = 14f;
    [SerializeField] private float dragAccel = 9f;
    [SerializeField] private Color ghostWaterTint = new Color(0.32f, 0.03f, 0.03f, 1f);
    [SerializeField] private Color humanTint = new Color(0.55f, 0.72f, 0.8f, 1f);

    private PlayerMovement player;
    private Transform anchor;
    private RopeLine rope;
    private ScreenPrompt prompt;
    private bool bossDead;
    private bool pulling;

    private static readonly int SpeedId = Animator.StringToHash("Speed");

    private void Start()
    {
        player = FindAnyObjectByType<PlayerMovement>();
        if (!RopeSession.Active)
        {
            RopeSession.GhostAnchor = defaultAnchor;
            RopeSession.HumanReturnPoint = defaultReturnPoint;
            RopeSession.HumanReturnYaw = defaultReturnYaw;
        }
        anchor = new GameObject("RopeAnchor").transform;
        anchor.SetParent(transform, false);
        anchor.position = RopeSession.GhostAnchor;

        rope = RopeLine.Create(transform, ropeMaterial);
        rope.startPoint = anchor;
        rope.endPoint = player != null ? player.transform : null;
        rope.endOffset = Vector3.up * 0.05f;
        rope.slack = 0.35f;

        prompt = ScreenPrompt.Create(transform);
        if (bossHealth != null)
        {
            if (bossHealth.IsDead) bossDead = true;
            bossHealth.OnDeath += () => bossDead = true;
        }
    }

    private void Update()
    {
        if (player == null || pulling || WorldTransition.IsRunning || !bossDead)
        {
            prompt.Hide();
            return;
        }
        prompt.Show("E   밧줄을 당긴다");
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            StartCoroutine(Pull());
    }

    private IEnumerator Pull()
    {
        pulling = true;
        prompt.Hide();
        WorldTransition.SetPlayerControl(player, false);
        var cam = ThirdPersonCamera.Instance;

        // 밧줄이 팽팽해짐
        float s0 = rope.slack;
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            rope.slack = Mathf.Lerp(s0, 0f, t / 0.35f);
            yield return null;
        }
        rope.slack = 0f;
        if (cam != null) cam.Shake(0.25f, 0.3f);

        // 어디 있든 연못 쪽으로 끌려감 (등이 끌려가는 쪽)
        Transform p = player.transform;
        float speed = dragStartSpeed;
        for (float t = 0f; t < 20f; t += Time.deltaTime)
        {
            Vector3 to = anchor.position - p.position; to.y = 0f;
            float dist = to.magnitude;
            if (dist < 0.8f) break;
            speed = Mathf.Min(dragMaxSpeed, speed + dragAccel * Time.deltaTime);
            Vector3 dir = to / dist;
            Vector3 next = p.position + dir * Mathf.Min(dist, speed * Time.deltaTime);
            next.y = WorldTransition.GroundHeight(next) + 1.05f;
            p.SetPositionAndRotation(next, Quaternion.LookRotation(-dir, Vector3.up));
            yield return null;
        }

        // 물속으로 빨려 들어가 인간 세계 나무 밑으로
        GameProgress.Stage1Cleared = true;
        RopeSession.Active = false;
        WorldTransition.Begin(humanScene, RopeSession.HumanReturnPoint, RopeSession.HumanReturnYaw, ghostWaterTint, humanTint, false);
    }
}
