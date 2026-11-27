using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 시작 화면: 메인 그림 위에 제목 「원귀록」·부제 「문턱 너머」와 메뉴(처음부터 / 이어하기 / 끝내기).
// 위·아래(W·S·방향키)로 고르고 Enter·Space·E 로 정하거나, 마우스로 가리켜 누른다.
public class TitleMenu : MonoBehaviour
{
    [SerializeField] private Texture2D keyArt;
    [SerializeField] private string firstScene = "HumanWorld_01";
    [SerializeField] private float fadeTime = 0.8f;

    private static readonly string[] Items = { "처음부터", "이어하기", "끝내기" };
    private static readonly Color Ink = new Color(0.12f, 0.08f, 0.06f, 1f);
    private static readonly Color Blood = new Color(0.55f, 0.06f, 0.05f, 1f);
    private static readonly Color Paper = new Color(0.92f, 0.88f, 0.78f, 1f);

    private readonly RectTransform[] rows = new RectTransform[3];
    private readonly Image[] strokes = new Image[3];
    private readonly Text[] labels = new Text[3];
    private Image fade;
    private int selected;
    private bool leaving;

    private void Start()
    {
        var root = new GameObject("TitleCanvas", typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, false);
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f;

        // 메인 그림: 화면을 가득 채움 (비율이 달라도 빈틈 없이 잘라냄)
        var art = new GameObject("KeyArt", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        art.transform.SetParent(root.transform, false);
        art.GetComponent<RawImage>().texture = keyArt;
        var fit = art.GetComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = keyArt != null ? (float)keyArt.width / keyArt.height : 16f / 9f;

        var brush = Resources.Load<Font>("Fonts/EastSeaDokdo-Regular");
        var hand = Resources.Load<Font>("Fonts/KirangHaerang-Regular");
        var title = MakeText(root.transform, brush, 260, "원귀록", new Vector2(0.5f, 1f), new Vector2(0, -60), Ink);
        title.GetComponent<Outline>().effectDistance = new Vector2(4, -4);
        MakeText(root.transform, brush, 80, "문턱 너머", new Vector2(0.5f, 1f), new Vector2(0, -330), Ink);

        var stroke = Resources.Load<Sprite>("UI/BrushStroke_Back");
        for (int i = 0; i < Items.Length; i++)
        {
            var row = new GameObject("Menu_" + Items[i], typeof(RectTransform), typeof(Image));
            row.transform.SetParent(root.transform, false);
            var rt = (RectTransform)row.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.78f, 0.62f);
            rt.anchoredPosition = new Vector2(0, -i * 105);
            rt.sizeDelta = new Vector2(380, 90);
            strokes[i] = row.GetComponent<Image>();
            strokes[i].sprite = stroke;
            rows[i] = rt;
            labels[i] = MakeText(row.transform, hand, 54, Items[i], new Vector2(0.5f, 0.5f), Vector2.zero, Ink);
            labels[i].rectTransform.sizeDelta = rt.sizeDelta;
        }

        fade = new GameObject("Fade", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        fade.transform.SetParent(root.transform, false);
        var frt = fade.rectTransform; frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.sizeDelta = Vector2.zero;
        fade.color = new Color(0, 0, 0, 0);
        fade.raycastTarget = false;
        Paint();
    }

    private static Text MakeText(Transform parent, Font font, int size, string s, Vector2 anchor, Vector2 pos, Color color)
    {
        var go = new GameObject("Text_" + s, typeof(RectTransform), typeof(Text), typeof(Outline));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, anchor.y);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(1200, size * 1.3f);
        var t = go.GetComponent<Text>();
        t.font = font; t.fontSize = size; t.text = s; t.color = color;
        t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
        go.GetComponent<Outline>().effectColor = new Color(Paper.r, Paper.g, Paper.b, 0.85f);   // 붉은 하늘 위에서도 읽히게
        return t;
    }

    private void Update()
    {
        if (leaving || fade == null) return;
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) { selected = (selected + 1) % Items.Length; Paint(); }
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) { selected = (selected + Items.Length - 1) % Items.Length; Paint(); }
            if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame) Choose(selected);
        }
        var mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 p = mouse.position.ReadValue();
        for (int i = 0; i < rows.Length; i++)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(rows[i], p, null)) continue;
            if (selected != i) { selected = i; Paint(); }
            if (mouse.leftButton.wasPressedThisFrame) Choose(i);
        }
    }

    // 고른 줄은 종이색 붓 획이 진해지고 글자가 핏빛
    private void Paint()
    {
        for (int i = 0; i < rows.Length; i++)
        {
            bool on = i == selected;
            strokes[i].color = new Color(Paper.r, Paper.g, Paper.b, on ? 0.95f : 0.55f);
            labels[i].color = on ? Blood : Ink;
            rows[i].localScale = Vector3.one * (on ? 1.08f : 1f);
        }
    }

    private void Choose(int i)
    {
        if (i == 2)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
            return;
        }
        if (i == 0)
        {
            GameProgress.ResetAll();
            GameProgress.ResetYanggiAndSword();
        }
        StartCoroutine(Leave());
    }

    private IEnumerator Leave()
    {
        leaving = true;
        for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            fade.color = new Color(0, 0, 0, t / fadeTime);
            yield return null;
        }
        SceneManager.LoadScene(firstScene);
    }
}
