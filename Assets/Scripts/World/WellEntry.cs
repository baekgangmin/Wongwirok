using UnityEngine;
using UnityEngine.InputSystem;

// 인간 세계 대비전 뒤뜰 우물: 1스테이지를 깬 뒤 E 로 대비전 기둥에 밧줄을 묶어 몸에 감고, 다시 E 로 우물에 뛰어들면
// 물에 비친 불타는 궐 안으로 떨어져 귀의 세계 궁궐 복도(2스테이지)에서 깨어난다.
public class WellEntry : MonoBehaviour
{
    [SerializeField] private float radius = 2.5f;
    [SerializeField] private Transform tiePoint;   // 대비전 기둥의 밧줄 묶는 자리
    [SerializeField] private Material ropeMaterial;
    [SerializeField] private string ghostScene = "BossMap_02";
    [SerializeField] private Vector3 arrivalPoint = new Vector3(0f, 0f, 6f);
    [Tooltip("우물 물빛: 붉은 불빛이 비친 물 (1스테이지 연못의 푸른 물과 구분)")]
    [SerializeField] private Color waterTint = new Color(0.35f, 0.1f, 0.05f, 1f);
    [SerializeField] private Color worldTint = new Color(0.42f, 0.03f, 0.03f, 1f);

    private PlayerMovement player;
    private ScreenPrompt prompt;
    private RopeLine rope;
    private bool falling;

    private void Start()
    {
        player = FindAnyObjectByType<PlayerMovement>();
        prompt = ScreenPrompt.Create(transform);
    }

    private void Update()
    {
        if (player == null || falling || WorldTransition.IsRunning)
        {
            prompt.Hide();
            return;
        }
        Vector3 d = player.transform.position - transform.position; d.y = 0f;
        if (d.sqrMagnitude > radius * radius) { prompt.Hide(); return; }
        if (!GameProgress.Stage1Cleared) { prompt.Show("깊은 우물이다. 바닥이 보이지 않는다"); return; }
        if (GameProgress.Stage2Cleared) { prompt.Show("우물 물이 맑다. 더는 아무것도 비치지 않는다"); return; }
        bool pressed = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        if (rope == null)
        {
            prompt.Show("E   대비전 기둥에 밧줄을 묶는다");
            if (pressed) Tie();
        }
        else
        {
            prompt.Show("E   우물로 뛰어든다");
            if (pressed) Fall();
        }
    }

    private void Tie()
    {
        rope = RopeLine.Create(null, ropeMaterial);
        rope.startPoint = tiePoint;
        rope.endPoint = RopeLine.Waist(player.transform);
        rope.slack = 0.3f;
    }

    private void Fall()
    {
        falling = true;
        prompt.Hide();
        // 돌아올 자리: 우물 앞 (주인공이 서 있던 쪽)
        Vector3 away = player.transform.position - transform.position; away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = -transform.forward;
        away.Normalize();
        RopeSession.Active = true;
        RopeSession.HumanReturnPoint = transform.position + away * 2.5f;
        RopeSession.HumanReturnYaw = Quaternion.LookRotation(-away).eulerAngles.y;
        WorldTransition.Begin(ghostScene, arrivalPoint, 0f, waterTint, worldTint, false, drown: true);
    }
}
