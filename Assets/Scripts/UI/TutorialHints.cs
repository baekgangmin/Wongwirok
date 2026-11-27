using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 조작 안내: 양기귀의 첫 이야기가 끝나고 대화를 닫으면 화면 왼쪽에 한 줄씩 나오고, 직접 해 보면 다음으로 넘어간다.
// 한 번 끝내면 다시 안 나옴 (시작 화면 「처음부터」 때 초기화).
public class TutorialHints : MonoBehaviour
{
    private const string DoneKey = "wongwirok_tutorial_done";
    public static void ResetDone() { PlayerPrefs.DeleteKey(DoneKey); PlayerPrefs.Save(); }

    private static readonly string[] Lines =
    {
        "W A S D   걷기      Shift   달리기      마우스   둘러보기",
        "좌클릭   베기  (이어 누르면 세 번 연달아 벤다)",
        "우클릭   막아 튕겨 내기  (적의 공격이 닿는 순간)",
        "C   구르기  (구르는 동안은 맞지 않는다)",
        "E   가까이 가서 말 걸기 · 밧줄 묶기 · 들여다보기\n연못가 나무로 가 보자",
    };

    private Text text;
    private Image back;
    private RectTransform panel;
    private float progress;

    public static void Begin()
    {
        if (PlayerPrefs.GetInt(DoneKey, 0) == 1 || GameProgress.StoryHeard < 1 || FindAnyObjectByType<TutorialHints>() != null) return;
        new GameObject("TutorialHints").AddComponent<TutorialHints>();
    }

    private void Start()
    {
        var root = new GameObject("TutorialCanvas", typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 45;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var go = new GameObject("Hint", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(root.transform, false);
        panel = (RectTransform)go.transform;
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0f, 0.5f);
        panel.anchoredPosition = new Vector2(30, 120);
        panel.sizeDelta = new Vector2(760, 130);
        back = go.GetComponent<Image>();
        back.sprite = Resources.Load<Sprite>("UI/BrushStroke_Back");   // 양기 숫자 뒤와 같은 종이색 붓 획
        back.raycastTarget = false;

        var tgo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        tgo.transform.SetParent(go.transform, false);
        var trt = (RectTransform)tgo.transform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(50, 10); trt.offsetMax = new Vector2(-40, -10);
        text = tgo.GetComponent<Text>();
        text.font = Resources.Load<Font>("Fonts/KirangHaerang-Regular");
        text.fontSize = 34; text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
        text.color = new Color(0.12f, 0.08f, 0.06f, 1f);
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        for (int i = 0; i < Lines.Length; i++)
        {
            text.text = Lines[i];
            progress = 0f;
            yield return Fade(0f, 1f);
            float shown = 0f;
            while (!Done(i, shown)) { shown += Time.deltaTime; yield return null; }
            text.color = new Color(0.62f, 0.4f, 0.08f, 1f);   // 해냈다: 금빛으로 잠깐
            yield return new WaitForSeconds(0.5f);
            yield return Fade(1f, 0f);
            text.color = new Color(0.12f, 0.08f, 0.06f, 1f);
        }
        PlayerPrefs.SetInt(DoneKey, 1); PlayerPrefs.Save();
        Destroy(gameObject);
    }

    // 안내마다 직접 해 봤는지
    private bool Done(int i, float shown)
    {
        var kb = Keyboard.current; var mouse = Mouse.current;
        if (kb == null || mouse == null) return shown > 6f;
        switch (i)
        {
            case 0:
                if (kb.wKey.isPressed || kb.aKey.isPressed || kb.sKey.isPressed || kb.dKey.isPressed) progress += Time.deltaTime;
                return progress > 1.2f;   // 잠깐이라도 걸어 봤으면
            case 1:
                if (mouse.leftButton.wasPressedThisFrame) progress++;
                return progress >= 3;
            case 2: return mouse.rightButton.wasPressedThisFrame;
            case 3: return kb.cKey.wasPressedThisFrame;
            default: return shown > 7f || (shown > 1f && kb.eKey.wasPressedThisFrame);
        }
    }

    private IEnumerator Fade(float from, float to)
    {
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            float a = Mathf.Lerp(from, to, t / 0.35f);
            back.color = new Color(0.92f, 0.88f, 0.78f, 0.9f * a);
            var c = text.color; c.a = a; text.color = c;
            panel.anchoredPosition = new Vector2(30 - 40 * (1f - a), 120);   // 왼쪽에서 살짝 밀려 들어옴
            yield return null;
        }
        back.color = new Color(0.92f, 0.88f, 0.78f, 0.9f * to);
        var e = text.color; e.a = to; text.color = e;
    }
}
