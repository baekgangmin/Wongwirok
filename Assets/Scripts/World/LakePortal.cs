using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 연못 들여다보기: 가까이 가면 안내가 뜨고, E 를 누르면 다른 세계로 넘어간다.
// 인간 세계 선착장 끝 → 귀의 세계 같은 자리.
public class LakePortal : MonoBehaviour
{
    [SerializeField] private string targetScene = "BossMap_01";
    [Tooltip("도착 위치 (다음 씬의 같은 좌표). 비워두면 이 오브젝트 위치")]
    [SerializeField] private bool useOwnPositionAsArrival = true;
    [SerializeField] private Vector3 arrivalPoint;
    [SerializeField] private float arrivalYaw = 0f;
    [SerializeField] private float radius = 2.6f;
    [SerializeField] private string promptText = "E   연못을 들여다본다";
    [SerializeField] private Color waterTint = new Color(0.08f, 0.28f, 0.36f, 1f);
    [SerializeField] private Color worldTint = new Color(0.42f, 0.03f, 0.03f, 1f);

    private Transform player;
    private GameObject promptRoot;

    private void Start()
    {
        var pm = FindAnyObjectByType<PlayerMovement>();
        if (pm != null) player = pm.transform;
        BuildPrompt();
        promptRoot.SetActive(false);
    }

    private void Update()
    {
        if (player == null || WorldTransition.IsRunning)
        {
            if (promptRoot != null) promptRoot.SetActive(false);
            return;
        }
        Vector3 d = player.position - transform.position;
        d.y = 0f;
        bool near = d.sqrMagnitude < radius * radius;
        promptRoot.SetActive(near);
        if (near && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            promptRoot.SetActive(false);
            Vector3 arrival = useOwnPositionAsArrival ? transform.position : arrivalPoint;
            WorldTransition.Begin(targetScene, arrival, arrivalYaw, waterTint, worldTint);
        }
    }

    // 임시 안내창 (조선풍 UI 는 7번 작업에서 교체)
    private void BuildPrompt()
    {
        promptRoot = new GameObject("PortalPrompt", typeof(Canvas), typeof(CanvasScaler));
        promptRoot.transform.SetParent(transform, false);
        var canvas = promptRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = promptRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(promptRoot.transform, false);
        var prt = (RectTransform)panel.transform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.22f);
        prt.sizeDelta = new Vector2(420, 64);
        var img = panel.GetComponent<Image>();
        img.color = new Color(0.93f, 0.88f, 0.76f, 0.88f); // 한지 빛

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(panel.transform, false);
        var trt = (RectTransform)textGo.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var text = textGo.GetComponent<Text>();
        text.text = promptText;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 28;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.18f, 0.1f, 0.06f, 1f); // 먹색
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
