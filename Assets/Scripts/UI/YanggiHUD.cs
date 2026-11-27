using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 화면 오른쪽 위 양기 수치. 얻거나 잃으면 "+12" / "-40" 이 떠올랐다 사라진다.
// 씬에 따로 두지 않아도 게임이 시작되면 스스로 하나 만들어 계속 유지된다.
// 적을 쓰러뜨려 얻은 양기는 푸른 양기 덩이(플립북)가 날아와 숫자에 닿을 때마다 조금씩 올라간다.
public class YanggiHUD : MonoBehaviour
{
    private static YanggiHUD instance;
    private static Sprite[] orbFrames;
    private Text valueText;
    private int held;   // 이미 저장됐지만 덩이가 아직 날아오는 중이라 숫자에 안 보탠 양
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
        instance = hud;
        orbFrames = new Sprite[4];
        for (int i = 0; i < 4; i++) orbFrames[i] = Resources.Load<Sprite>("UI/YanggiOrb_" + i);
        var font = Resources.Load<Font>("Fonts/KirangHaerang-Regular");
        // 숫자 뒤 먹 붓 획 (오른쪽에서 왼쪽으로 그은 듯 뒤집음)
        var back = new GameObject("Brush", typeof(RectTransform), typeof(Image));
        back.transform.SetParent(root.transform, false);
        var brt = (RectTransform)back.transform;
        brt.anchorMin = brt.anchorMax = new Vector2(1f, 1f);
        brt.pivot = new Vector2(0.5f, 0.5f);   // 가운데 기준으로 뒤집어야 화면 안에 남음
        brt.anchoredPosition = new Vector2(-14 - 165, -26 - 40);
        brt.sizeDelta = new Vector2(330, 80);
        brt.localScale = new Vector3(-1, 1, 1);
        var bimg = back.GetComponent<Image>();
        bimg.sprite = Resources.Load<Sprite>("UI/BrushStroke_Back");
        bimg.color = new Color(0.92f, 0.88f, 0.78f, 0.85f);   // 체력바 바탕과 같은 종이색 붓 획
        bimg.raycastTarget = false;
        hud.valueText = MakeText(root.transform, font, 46, new Vector2(-48, -36), TextAnchor.UpperRight);
        hud.popText = MakeText(root.transform, font, 36, new Vector2(-40, -100), TextAnchor.UpperRight);
        hud.popText.text = "";
        hud.Refresh(GameProgress.Yanggi);
        // 시작 화면에서는 숨김
        canvas.enabled = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Title";
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged += (_, s) => { if (canvas != null) canvas.enabled = s.name != "Title"; };
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

    private void Refresh(int now) => valueText.text = "양기  " + (now - held);

    // 쓰러진 몬스터 몸에서 양기 덩이가 피어올라 몸 위에 잠깐 모여 떠 있다가, 한 줄로 차례차례 오른쪽 위 숫자로 흘러 들어간다.
    // 반드시 GameProgress.AddYanggi 보다 먼저 부를 것 (숫자를 덩이가 도착할 때까지 붙잡아 둠).
    public static void FlyFrom(Vector3 worldPos, int amount)
    {
        if (instance == null || Camera.main == null || amount <= 0 || orbFrames[0] == null)
            return;
        int count = Mathf.Clamp(amount / 6, 3, 8);
        instance.held += amount;
        for (int i = 0; i < count; i++)
        {
            int share = amount / count + (i < amount % count ? 1 : 0);
            instance.StartCoroutine(instance.Orb(worldPos, share, i));
        }
    }

    private IEnumerator Orb(Vector3 body, int share, int index)
    {
        const float RiseT = 0.5f, HoverEnd = 1.5f, FlowGap = 0.12f, FlyT = 0.85f;
        var go = new GameObject("YanggiOrb", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;   // 왼쪽 아래 기준 = 화면 좌표
        rt.sizeDelta = new Vector2(70, 70);
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.enabled = false;
        float scale = ((RectTransform)transform).localScale.x;   // 캔버스 배율 (화면 → 캔버스 좌표)
        // 몸 위에서 모여 떠 있을 자리 (월드 좌표라 카메라가 움직여도 몬스터 위에 붙어 있음)
        Vector2 ring = Random.insideUnitCircle * 0.6f;
        Vector3 hover = body + new Vector3(ring.x, Random.Range(1.0f, 1.6f), ring.y);
        float riseStart = index * 0.07f;                  // 몸에서 하나씩 스며 나옴
        float flyStart = HoverEnd + index * FlowGap;      // 한 줄로 차례차례 흘러감
        int frame = Random.Range(0, 4);
        float t = 0f;
        Vector2 from = Vector2.zero;
        bool flying = false;
        while (true)
        {
            t += Time.unscaledDeltaTime;
            img.sprite = orbFrames[(frame + (int)(Time.unscaledTime * 12f)) % 4];   // 플립북 초당 12장
            Vector2 end = (Vector2)valueText.rectTransform.position / scale + new Vector2(-90f, -25f);   // "양기" 글자 근처
            if (t < flyStart)
            {
                if (t < riseStart) { yield return null; continue; }
                float k = Mathf.Clamp01((t - riseStart) / RiseT);
                Vector3 w = Vector3.Lerp(body, hover, 1f - (1f - k) * (1f - k))
                            + Vector3.up * (k >= 1f ? Mathf.Sin(t * 4f + index) * 0.08f : 0f);   // 다 오르면 둥실
                var cam = Camera.main;
                Vector3 sp = cam != null ? cam.WorldToScreenPoint(w) : new Vector3(0, 0, -1);
                img.enabled = sp.z > 0f;   // 카메라 뒤면 잠깐 숨김
                rt.anchoredPosition = (Vector2)sp / scale;
                rt.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, k);
            }
            else
            {
                if (!flying) { flying = true; from = rt.anchoredPosition; img.enabled = true; }
                float k = Mathf.Clamp01((t - flyStart) / FlyT);
                Vector2 mid = Vector2.Lerp(from, end, 0.5f) + Vector2.left * 120f;   // 살짝 휘어 들어감
                Vector2 prev = rt.anchoredPosition;
                float e = k * k;
                rt.anchoredPosition = Vector2.Lerp(Vector2.Lerp(from, mid, e), Vector2.Lerp(mid, end, e), e);
                Vector2 v = rt.anchoredPosition - prev;
                if (v.sqrMagnitude > 0.01f)   // 아래쪽(둥근 몸)이 날아가는 방향, 꼬리가 뒤로
                    rt.localRotation = Quaternion.Slerp(rt.localRotation, Quaternion.Euler(0, 0, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg + 90f), Time.unscaledDeltaTime * 12f);
                rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.55f, k);
                if (k >= 1f) break;
            }
            yield return null;
        }
        Destroy(go);
        held = Mathf.Max(0, held - share);
        Refresh(GameProgress.Yanggi);
        valueText.rectTransform.localScale = Vector3.one * 1.15f;   // 닿을 때 숫자가 톡 튐
    }

    private void Update()
    {
        var vs = valueText.rectTransform.localScale.x;
        if (vs > 1f) valueText.rectTransform.localScale = Vector3.one * Mathf.MoveTowards(vs, 1f, Time.unscaledDeltaTime * 1.5f);
        if (popTimer <= 0f) return;
        popTimer -= Time.unscaledDeltaTime;
        float k = 1f - popTimer / PopTime;
        popText.rectTransform.anchoredPosition = new Vector2(-40, -100 - k * 20f);
        var c = popText.color; c.a = 1f - k * k; popText.color = c;
        if (popTimer <= 0f) popText.text = "";
    }
}
