using UnityEngine;
using UnityEngine.UI;

// 화면 오른쪽 위 양기 수치. 얻거나 잃으면 "+12" / "-40" 이 떠올랐다 사라진다.
// 씬에 따로 두지 않아도 게임이 시작되면 스스로 하나 만들어 계속 유지된다.
public class YanggiHUD : MonoBehaviour
{
    private Text valueText;
    private Text popText;
    private float popTimer;
    private const float PopTime = 1.2f;
    private static readonly Color Ink = new Color(0.12f, 0.08f, 0.06f, 1f);
    private static readonly Color Gain = new Color(0.85f, 0.55f, 0.12f, 1f);
    private static readonly Color Loss = new Color(0.62f, 0.08f, 0.07f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        var root = new GameObject("YanggiHUD", typeof(Canvas), typeof(CanvasScaler));
        DontDestroyOnLoad(root);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        var hud = root.AddComponent<YanggiHUD>();
        var font = Resources.Load<Font>("Fonts/KirangHaerang-Regular");
        hud.valueText = MakeText(root.transform, font, 46, new Vector2(-40, -36), TextAnchor.UpperRight);
        hud.popText = MakeText(root.transform, font, 36, new Vector2(-40, -100), TextAnchor.UpperRight);
        hud.popText.text = "";
        hud.Refresh(GameProgress.Yanggi);
    }

    private static Text MakeText(Transform parent, Font font, int size, Vector2 pos, TextAnchor align)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Outline));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(420, 60);
        var t = go.GetComponent<Text>();
        t.font = font; t.fontSize = size; t.alignment = align; t.color = Ink; t.raycastTarget = false;
        go.GetComponent<Outline>().effectColor = new Color(0.95f, 0.92f, 0.85f, 0.8f);   // 종이색 테두리: 어두운 배경에서도 읽히게
        return t;
    }

    private void OnEnable() => GameProgress.YanggiChanged += OnChanged;
    private void OnDisable() => GameProgress.YanggiChanged -= OnChanged;

    private void OnChanged(int now, int delta)
    {
        Refresh(now);
        if (delta == 0) return;
        popText.text = delta > 0 ? "+" + delta : delta.ToString();
        popText.color = delta > 0 ? Gain : Loss;
        popTimer = PopTime;
    }

    private void Refresh(int now) => valueText.text = "양기  " + now;

    private void Update()
    {
        if (popTimer <= 0f) return;
        popTimer -= Time.unscaledDeltaTime;
        float k = 1f - popTimer / PopTime;
        popText.rectTransform.anchoredPosition = new Vector2(-40, -100 - k * 20f);
        var c = popText.color; c.a = 1f - k * k; popText.color = c;
        if (popTimer <= 0f) popText.text = "";
    }
}
