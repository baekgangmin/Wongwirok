using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 인간 세계 ↔ 귀의 세계 이동 연출.
// 물에 가라앉으며 화면이 물빛 → 핏빛 → 암전, 다음 씬을 불러온 뒤
// 같은 자리 물속에서 떠오르며 화면이 밝아진다.
public class WorldTransition : MonoBehaviour
{
    public static bool IsRunning { get; private set; }

    private string targetScene;
    private Vector3 arrivalPoint;
    private float arrivalYaw;
    private Color waterTint;
    private Color worldTint;

    private Image overlay;

    // 효과 시간 (초)
    private const float SinkTime = 1.6f;
    private const float TintTime = 1.1f;
    private const float BlackTime = 0.5f;
    private const float RiseTime = 1.8f;
    private const float SinkDepth = 2.4f;

    private static readonly int TrappedId = Animator.StringToHash("Trapped");
    private static readonly int SpeedId = Animator.StringToHash("Speed");

    private bool riseOnArrival = true;

    // riseOnArrival: true = 도착한 곳 물속에서 떠오름 / false = 도착한 곳 땅 위에 바로 서 있음
    public static void Begin(string targetScene, Vector3 arrivalPoint, float arrivalYaw, Color waterTint, Color worldTint, bool riseOnArrival = true)
    {
        if (IsRunning)
            return;
        var go = new GameObject("WorldTransition");
        DontDestroyOnLoad(go);
        var t = go.AddComponent<WorldTransition>();
        t.targetScene = targetScene;
        t.arrivalPoint = arrivalPoint;
        t.arrivalYaw = arrivalYaw;
        t.waterTint = waterTint;
        t.worldTint = worldTint;
        t.riseOnArrival = riseOnArrival;
        t.BuildOverlay();
        t.StartCoroutine(t.Run());
    }

    private void BuildOverlay()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var imgGo = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
        imgGo.transform.SetParent(transform, false);
        var rt = (RectTransform)imgGo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        overlay = imgGo.GetComponent<Image>();
        overlay.raycastTarget = false;
        overlay.color = new Color(waterTint.r, waterTint.g, waterTint.b, 0f);
    }

    private IEnumerator Run()
    {
        IsRunning = true;

        // 1) 가라앉기 + 물빛
        var player = FindPlayer();
        SetPlayerControl(player, false);
        Vector3 start = player != null ? player.transform.position : Vector3.zero;
        for (float t = 0f; t < SinkTime; t += Time.deltaTime)
        {
            float k = t / SinkTime;
            if (player != null)
                player.transform.position = start + Vector3.down * (SinkDepth * k * k);
            overlay.color = new Color(waterTint.r, waterTint.g, waterTint.b, Mathf.Lerp(0f, 0.6f, k));
            yield return null;
        }

        // 2) 물빛 → 다음 세계 빛
        for (float t = 0f; t < TintTime; t += Time.deltaTime)
        {
            float k = t / TintTime;
            Color c = Color.Lerp(waterTint, worldTint, k);
            c.a = Mathf.Lerp(0.6f, 0.9f, k);
            overlay.color = c;
            yield return null;
        }

        // 3) 암전
        Color from = overlay.color;
        for (float t = 0f; t < BlackTime; t += Time.deltaTime)
        {
            overlay.color = Color.Lerp(from, Color.black, t / BlackTime);
            yield return null;
        }
        overlay.color = Color.black;

        // 4) 다음 씬 불러오기
        var op = SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Single);
        while (!op.isDone)
            yield return null;
        yield return null;

        // 5) 같은 자리 물속에서 떠오르기
        player = FindPlayer();
        float groundY = GroundHeight(arrivalPoint);
        Vector3 standPos = new Vector3(arrivalPoint.x, groundY + 1.05f, arrivalPoint.z);
        Vector3 underPos = riseOnArrival ? standPos + Vector3.down * SinkDepth : standPos;
        if (player != null)
        {
            SetPlayerControl(player, false);
            player.transform.SetPositionAndRotation(underPos, Quaternion.Euler(0f, arrivalYaw, 0f));
            var cam = FindAnyObjectByType<ThirdPersonCamera>();
            if (cam != null)
                cam.SetYaw(arrivalYaw);
        }

        for (float t = 0f; t < RiseTime; t += Time.deltaTime)
        {
            float k = t / RiseTime;
            if (player != null)
                player.transform.position = Vector3.Lerp(underPos, standPos, 1f - (1f - k) * (1f - k));
            // 검정 → 다음 세계 빛 → 투명
            Color c = k < 0.3f
                ? Color.Lerp(Color.black, worldTint, k / 0.3f)
                : Color.Lerp(worldTint, new Color(worldTint.r, worldTint.g, worldTint.b, 0f), (k - 0.3f) / 0.7f);
            if (k < 0.3f) c.a = Mathf.Lerp(1f, 0.85f, k / 0.3f);
            else c.a = Mathf.Lerp(0.85f, 0f, (k - 0.3f) / 0.7f);
            overlay.color = c;
            yield return null;
        }
        if (player != null)
        {
            player.transform.position = standPos;
            SetPlayerControl(player, true);
        }

        IsRunning = false;
        Destroy(gameObject);
    }

    private static PlayerMovement FindPlayer() => FindAnyObjectByType<PlayerMovement>();

    public static void SetPlayerControl(PlayerMovement player, bool on)
    {
        if (player == null)
            return;
        player.enabled = on;
        var attack = player.GetComponent<PlayerAttack>();
        if (attack != null) attack.enabled = on;
        var parry = player.GetComponent<PlayerParry>();
        if (parry != null) parry.enabled = on;
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = on;
        var anim = player.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.SetFloat(SpeedId, 0f);
            anim.SetBool(TrappedId, !on);
        }
    }

    public static float GroundHeight(Vector3 p)
    {
        // 지형만 보고 바닥 높이를 찾는다
        float best = float.NegativeInfinity;
        foreach (var t in Terrain.activeTerrains)
        {
            var tp = t.transform.position;
            var s = t.terrainData.size;
            if (p.x < tp.x || p.x > tp.x + s.x || p.z < tp.z || p.z > tp.z + s.z)
                continue;
            best = Mathf.Max(best, t.SampleHeight(p) + tp.y);
        }
        return float.IsNegativeInfinity(best) ? p.y : best;
    }
}
