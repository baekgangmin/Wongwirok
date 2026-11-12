using UnityEngine;
using UnityEngine.UI;

// 화면 아래 안내창 (임시 한지색. 조선풍 UI 는 7번 작업에서 교체)
public class ScreenPrompt : MonoBehaviour
{
    private Text text;

    public static ScreenPrompt Create(Transform parent)
    {
        var root = new GameObject("ScreenPrompt", typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(parent, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        var prt = (RectTransform)panel.transform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.22f);
        prt.sizeDelta = new Vector2(460, 64);
        panel.GetComponent<Image>().color = new Color(0.93f, 0.88f, 0.76f, 0.88f);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(panel.transform, false);
        var trt = (RectTransform)textGo.transform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var p = root.AddComponent<ScreenPrompt>();
        p.text = textGo.GetComponent<Text>();
        p.text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        p.text.fontSize = 28;
        p.text.alignment = TextAnchor.MiddleCenter;
        p.text.color = new Color(0.18f, 0.1f, 0.06f, 1f);
        root.SetActive(false);
        return p;
    }

    public void Show(string message)
    {
        text.text = message;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }
}
