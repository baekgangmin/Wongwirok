using UnityEngine;
using UnityEngine.UI;

// 화면 아래 안내창: 먹색 띠 + 검붉은 글씨 (기랑해랑체)
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
        panel.GetComponent<Image>().color = new Color(0.03f, 0.02f, 0.02f, 0.72f);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(panel.transform, false);
        var trt = (RectTransform)textGo.transform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var p = root.AddComponent<ScreenPrompt>();
        p.text = textGo.GetComponent<Text>();
        p.text.font = Resources.Load<Font>("Fonts/KirangHaerang-Regular");
        p.text.fontSize = 34;
        p.text.alignment = TextAnchor.MiddleCenter;
        p.text.color = new Color(0.62f, 0.08f, 0.07f, 1f);
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
