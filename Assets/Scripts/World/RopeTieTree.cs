using UnityEngine;
using UnityEngine.InputSystem;

// 인간 세계: 연못가 나무에 밧줄을 묶고 몸에 감은 뒤, 연못 깊이 걸어 들어가면 귀의 세계로 넘어간다.
// 스테이지를 깬 뒤에는 다시 들어갈 수 없다.
public class RopeTieTree : MonoBehaviour
{
    [SerializeField] private Transform tiePoint;
    [SerializeField] private Material ropeMaterial;
    [SerializeField] private float radius = 2.8f;
    [Tooltip("연못 물 높이")]
    [SerializeField] private float waterLevel = -1.32f;
    [Tooltip("발이 물 아래 이만큼 잠기면 넘어간다")]
    [SerializeField] private float diveDepth = 1.1f;
    [SerializeField] private string ghostScene = "BossMap_01";
    [SerializeField] private Color waterTint = new Color(0.08f, 0.28f, 0.36f, 1f);
    [SerializeField] private Color worldTint = new Color(0.42f, 0.03f, 0.03f, 1f);

    private PlayerMovement player;
    private CharacterController controller;
    private ScreenPrompt prompt;
    private RopeLine rope;
    private bool tied;
    private bool diving;

    private void Start()
    {
        player = FindAnyObjectByType<PlayerMovement>();
        if (player != null) controller = player.GetComponent<CharacterController>();
        prompt = ScreenPrompt.Create(transform);
    }

    private void Update()
    {
        if (player == null || diving || WorldTransition.IsRunning)
        {
            prompt.Hide();
            return;
        }
        Vector3 d = player.transform.position - tiePoint.position;
        d.y = 0f;
        bool near = d.sqrMagnitude < radius * radius;
        bool pressed = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;

        if (!tied)
        {
            if (!near) { prompt.Hide(); return; }
            if (GameProgress.Stage1Cleared)
            {
                prompt.Show("연못은 고요하다. 더는 들어갈 수 없다");
                return;
            }
            prompt.Show("E   밧줄을 몸에 감는다");
            if (pressed) Tie();
        }
        else
        {
            if (near)
            {
                prompt.Show("E   밧줄을 푼다");
                if (pressed) { Untie(); return; }
            }
            else prompt.Hide();

            float feetY = player.transform.position.y + (controller != null ? controller.center.y - controller.height * 0.5f : -1f);
            if (waterLevel - feetY > diveDepth)
                Dive();
        }
    }

    private void Tie()
    {
        tied = true;
        rope = RopeLine.Create(null, ropeMaterial);
        rope.startPoint = tiePoint;
        rope.endPoint = player.transform;
        rope.endOffset = Vector3.up * 0.05f;
        rope.slack = 0.3f;
    }

    private void Untie()
    {
        tied = false;
        if (rope != null) Destroy(rope.gameObject);
    }

    private void Dive()
    {
        diving = true;
        prompt.Hide();
        Vector3 p = player.transform.position;
        // 돌아올 자리: 나무 앞, 연못을 바라보는 방향
        Vector3 toLake = p - tiePoint.position; toLake.y = 0f;
        if (toLake.sqrMagnitude < 0.01f) toLake = transform.forward;
        toLake.Normalize();
        RopeSession.Active = true;
        RopeSession.GhostAnchor = new Vector3(p.x, waterLevel - 0.25f, p.z);
        RopeSession.HumanReturnPoint = tiePoint.position + toLake * 3f; // 줄기가 굵어서 조금 떨어진 자리
        RopeSession.HumanReturnYaw = Quaternion.LookRotation(toLake).eulerAngles.y;
        WorldTransition.Begin(ghostScene, new Vector3(p.x, 0f, p.z), player.transform.eulerAngles.y, waterTint, worldTint, true);
    }
}
