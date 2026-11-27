using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 인간 세계의 작은 귀신 "양기귀". 말을 걸어 모은 양기를 건네면 다음 단계 나뭇가지를 준다.
public class YanggiGhost : MonoBehaviour
{
    [SerializeField] private float talkRadius = 2.6f;
    [Tooltip("양기를 빨아들이는 입 위치 (없으면 이 물체 위쪽)")]
    [SerializeField] private Transform mouth;
    [SerializeField] private float bobHeight = 0.12f;
    [SerializeField] private float bobSpeed = 1.6f;

    private PlayerMovement player;
    private PlayerAttack attack;
    private ScreenPrompt prompt;
    private GameObject panel;
    private Text lineText, offerText, keysText;
    private bool open, busy;
    private Transform visual;
    private Vector3 visualBase;

    // 진행 단계마다 양기귀가 들려주는 이야기 (마을에 들어왔을 때 아직 안 들었으면 먼저 말을 건다)
    private static readonly string[][] Story =
    {
        new[] {
            "어? 너… 나를 볼 수 있어? 산 사람이 귀신을 보다니, 너도 문턱에 한 발 걸친 몸이구나.",
            "이 궁궐 밑에는 '귀의 세계'가 거울처럼 붙어 있어. 죽음 가까이 가야만 넘어갈 수 있지.",
            "저 연못 말이야. 삼십 년 동안 '연못귀신'이 산다고 다들 피했는데… 사실 거기 잠긴 건 억울하게 죽은 상궁의 원귀야.",
            "연못가 나무에 밧줄을 묶고 물속으로 걸어 들어가 봐. 돌아올 땐 밧줄을 당기면 돼.",
            "아, 그 나뭇가지! 이승에선 막대기지만 저쪽에선 귀신을 베는 검이 돼. 귀신을 베면 양기가 나오니까 나한테 가져와. 더 좋은 가지랑 바꿔 줄게.",
        },
        new[] {
            "상궁의 한이 풀렸어. 연못이 조용해졌네… 그런데 마지막에 '연못귀신은 내가 아니다'래.",
            "이번엔 대비전 쪽이 시끄러워. 옛날 소금 창고에 불이 났을 때, 문이 밖에서 잠겨서 궁녀 열세 명이 그대로 타 죽었대.",
            "문을 잠그라 한 사람이 대비마마였다는 소문이 있어. 그래서 열세 궁녀가 밤마다 대비 곁을 맴돌아.",
            "대비전 뒤뜰 우물을 들여다봐. 기둥에 밧줄 묶는 거 잊지 말고!",
        },
        new[] {
            "궁녀들도 이제 쉴 수 있겠다. 고마워.",
            "그런데 이상하지? 상궁도 궁녀들도 진짜 원수는 따로 있다는 듯이 굴었어. 이 궁궐 가장 깊은 곳에… 진짜 연못귀신이 있는 것 같아.",
            "다음은 훈련장의 별감 귀신이야. 누명을 쓰고 죽었다던데, 그 사람이라면 진실을 알지도 몰라.",
        },
    };
    private Vector3 homePos;   // 평소 자리 (이야기하러 주인공 앞에 왔다가 대화가 끝나면 돌아감)
    private string[] story;   // 지금 들려주는 이야기 (없으면 null = 거래 화면)
    private int page;
    private float arriveTime;

    private static int Chapter => GameProgress.Stage2Cleared ? 2 : GameProgress.Stage1Cleared ? 1 : 0;
    private static bool StoryPending => GameProgress.StoryHeard <= Chapter;

    private void Start()
    {
        player = FindAnyObjectByType<PlayerMovement>();
        if (player != null) attack = player.GetComponent<PlayerAttack>();
        prompt = ScreenPrompt.Create(transform);
        BuildPanel();
        if (transform.childCount > 0) { visual = transform.GetChild(0); visualBase = visual.localPosition; }
        homePos = transform.position;
    }

    private float Ground(Vector3 p)
    {
        float best = float.NegativeInfinity;
        foreach (var h in Physics.RaycastAll(p + Vector3.up * 5f, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore))
            if (!h.collider.transform.IsChildOf(transform) && (player == null || !h.collider.transform.IsChildOf(player.transform)) && h.point.y > best)
                best = h.point.y;   // 자기·주인공 몸은 빼고 가장 위의 땅
        return float.IsNegativeInfinity(best) ? p.y : best;
    }

    // 이야기하러 주인공 앞으로 날아옴 (평소 자리와 같은 높이만큼 땅 위에 뜸)
    private void ComeToPlayer()
    {
        Vector3 d = player.transform.position - transform.position; d.y = 0f;
        if (d.magnitude < talkRadius * 1.5f) return;
        Vector3 to = player.transform.position + player.transform.forward * 2.6f + player.transform.right * 1.4f;   // 앞 오른쪽 (바로 앞이면 주인공 몸에 가려짐)
        to.y = Ground(to) + (homePos.y - Ground(homePos));
        transform.position = to;
    }

    private IEnumerator GoHome()
    {
        Vector3 from = transform.position;
        if ((from - homePos).sqrMagnitude < 0.01f) yield break;
        for (float t = 0f; t < 2.5f && !open; t += Time.deltaTime)
        {
            float k = t / 2.5f; k = k * k * (3f - 2f * k);
            transform.position = Vector3.Lerp(from, homePos, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 2f;   // 둥실 떠서 돌아감
            yield return null;
        }
        if (!open) transform.position = homePos;
    }

    private void Update()
    {
        if (visual != null)   // 둥실둥실
            visual.localPosition = visualBase + Vector3.up * Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        if (player == null || WorldTransition.IsRunning) { prompt.Hide(); arriveTime = Time.time; return; }

        var kb = Keyboard.current;
        if (open)
        {
            prompt.Hide();
            FacePlayer();
            if (busy || kb == null) return;
            if (story != null)
            {
                if (kb.eKey.wasPressedThisFrame) { page++; if (page < story.Length) ShowPage(); else EndStory(); }
                else if (kb.escapeKey.wasPressedThisFrame) EndStory();   // 건너뛰기
                return;
            }
            if (kb.eKey.wasPressedThisFrame) Give();
            else if (kb.escapeKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame) Close();
            return;
        }

        // 마을에 들어와 잠시 뒤, 아직 안 들은 이야기가 있으면 양기귀가 먼저 말을 건다
        if (StoryPending && Time.time - arriveTime > 1.2f) { Open(); return; }

        Vector3 d = player.transform.position - transform.position; d.y = 0f;
        if (d.sqrMagnitude > talkRadius * talkRadius) { prompt.Hide(); return; }
        prompt.Show("E   양기귀에게 말을 건다");
        if (kb != null && kb.eKey.wasPressedThisFrame) Open();
    }

    private void FacePlayer()
    {
        Vector3 d = player.transform.position - transform.position; d.y = 0f;
        if (d.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 6f * Time.deltaTime);
    }

    private SwordData Next => SwordCatalog.Get(GameProgress.SwordTier + 1);

    private void Open()
    {
        open = true;
        SetPlayerFree(false);
        panel.SetActive(true);
        if (StoryPending) { ComeToPlayer(); story = Story[Chapter]; page = 0; ShowPage(); return; }
        ShowShop();
    }

    private void ShowPage() =>
        Show(story[page], $"{page + 1} / {story.Length}", page + 1 < story.Length ? "E   다음        Esc   건너뛰기" : "E   알겠어");

    private void EndStory()
    {
        GameProgress.StoryHeard = Chapter + 1;
        story = null;
        ShowShop();
    }

    private void ShowShop()
    {
        var next = Next;
        if (next == null) Show("배불러… 이제 줄 가지가 없어. 네 가지가 제일 좋아.", "", "Esc   그만둔다");
        else Show("양기… 따뜻한 양기 냄새가 나네. 조금만 나눠 주면 좋은 가지를 줄게.", OfferLine(next), Keys(next));
    }

    private void Close()
    {
        open = false;
        panel.SetActive(false);
        SetPlayerFree(true);
        StartCoroutine(GoHome());
    }

    private void Give()
    {
        var next = Next;
        if (next == null) return;
        if (GameProgress.Yanggi < next.price)
        {
            Show("에이, 그걸로는 모자라. 귀신을 더 베고 와.", OfferLine(next), Keys(next));
            return;
        }
        StartCoroutine(Trade(next));
    }

    private IEnumerator Trade(SwordData next)
    {
        busy = true;
        GameProgress.AddYanggi(-next.price);
        yield return Suck();
        GameProgress.SwordTier = next.tier;
        if (attack != null) attack.Equip(next);
        var after = Next;
        Show($"하아… 맛있다! 자, {next.displayName}. 귀의 세계에서는 검이 될 거야.",
             after != null ? OfferLine(after) : "", after != null ? Keys(after) : "Esc   그만둔다");
        busy = false;
    }

    // 양기가 주인공 가슴에서 귀신 입으로 빨려 들어감
    // shortcut: 먹색 작은 공 12개를 곡선으로 날림, 11/30 연출 작업 때 붓 번짐 입자로 교체
    private IEnumerator Suck()
    {
        Vector3 from = player.transform.position + Vector3.up * 1.2f;
        Vector3 to = mouth != null ? mouth.position : transform.position + Vector3.up * 0.8f;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.color = new Color(0.95f, 0.75f, 0.35f, 1f);
        const int n = 12;
        var dots = new Transform[n]; var offs = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(g.GetComponent<Collider>());
            g.GetComponent<Renderer>().sharedMaterial = mat;
            g.transform.localScale = Vector3.one * Random.Range(0.08f, 0.16f);
            dots[i] = g.transform;
            offs[i] = Random.insideUnitSphere * 0.6f;
        }
        const float dur = 1.1f;
        for (float t = 0f; t < dur + 0.5f; t += Time.deltaTime)
        {
            for (int i = 0; i < n; i++)
            {
                float k = Mathf.Clamp01((t - i * 0.04f) / dur);
                k = k * k;
                Vector3 mid = Vector3.Lerp(from, to, 0.5f) + Vector3.up * 0.6f + offs[i];
                dots[i].position = Vector3.Lerp(Vector3.Lerp(from + offs[i] * 0.3f, mid, k), Vector3.Lerp(mid, to, k), k);
                dots[i].localScale *= k > 0.9f ? 0.9f : 1f;
            }
            yield return null;
        }
        foreach (var d in dots) Destroy(d.gameObject);
        Destroy(mat);
    }

    private string OfferLine(SwordData s) => $"{s.displayName}   공격력 {s.attackPower}   ·   양기 {s.price}  (지금 {GameProgress.Yanggi})";
    private string Keys(SwordData s) => GameProgress.Yanggi >= s.price ? "E   양기를 건넨다        Esc   그만둔다" : "Esc   그만둔다";

    private void Show(string line, string offer, string keys)
    {
        lineText.text = line; offerText.text = offer; keysText.text = keys;
    }

    private void SetPlayerFree(bool on)
    {
        if (player != null) player.enabled = on;
        if (attack != null) attack.enabled = on;
    }

    private void BuildPanel()
    {
        var font = Resources.Load<Font>("Fonts/KirangHaerang-Regular");
        var root = new GameObject("YanggiGhostTalk", typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        var prt = (RectTransform)panel.transform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.2f);
        prt.sizeDelta = new Vector2(1150, 290);
        var bg = panel.GetComponent<Image>();
        bg.sprite = Resources.Load<Sprite>("UI/Dialog_Paper");   // 먹 붓 테두리 종이 (9칸 나눔, 가운데만 늘어남)
        if (bg.sprite != null) { bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = 1.6f; }
        else bg.color = new Color(0.94f, 0.91f, 0.84f, 0.94f);   // 그림 없으면 예전 종이색
        lineText = Line(prt, font, 40, 60, new Color(0.1f, 0.07f, 0.05f));
        offerText = Line(prt, font, 34, -10, new Color(0.45f, 0.25f, 0.05f));
        keysText = Line(prt, font, 30, -75, new Color(0.62f, 0.08f, 0.07f));
        panel.SetActive(false);
    }

    private static Text Line(RectTransform parent, Font font, int size, float y, Color c)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, y);
        rt.sizeDelta = new Vector2(1040, 60);
        var t = go.GetComponent<Text>();
        t.font = font; t.fontSize = size; t.alignment = TextAnchor.MiddleCenter; t.color = c; t.raycastTarget = false;
        t.verticalOverflow = VerticalWrapMode.Overflow;   // 이야기 대사가 두 줄로 넘어가도 잘리지 않게
        return t;
    }
}
