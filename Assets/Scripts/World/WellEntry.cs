using UnityEngine;
using UnityEngine.InputSystem;

// 인간 세계 대비전 뒤뜰 우물: 1스테이지를 깬 뒤 E 로 우물 안을 들여다보면, 물에 비친 불타는 궐 안으로 떨어져
// 귀의 세계 궁궐 복도(2스테이지)에서 깨어난다.
public class WellEntry : MonoBehaviour
{
    [SerializeField] private float radius = 2.5f;
    [SerializeField] private string ghostScene = "BossMap_02";
    [SerializeField] private Vector3 arrivalPoint = new Vector3(0f, 0f, 2f);
    [Tooltip("우물 물빛: 붉은 불빛이 비친 물 (1스테이지 연못의 푸른 물과 구분)")]
    [SerializeField] private Color waterTint = new Color(0.35f, 0.1f, 0.05f, 1f);
    [SerializeField] private Color worldTint = new Color(0.42f, 0.03f, 0.03f, 1f);

    private PlayerMovement player;
    private ScreenPrompt prompt;
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
        prompt.Show("E   우물 안을 들여다본다");
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            Fall();
    }

    private void Fall()
    {
        falling = true;
        prompt.Hide();
        // 돌아올 자리: 우물 앞 (주인공이 서 있던 쪽)
        Vector3 away = player.transform.position - transform.position; away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = -transform.forward;
        away.Normalize();
        RopeSession.HumanReturnPoint = transform.position + away * 2.5f;
        RopeSession.HumanReturnYaw = Quaternion.LookRotation(-away).eulerAngles.y;
        WorldTransition.Begin(ghostScene, arrivalPoint, 0f, waterTint, worldTint, false, drown: true);
    }
}
