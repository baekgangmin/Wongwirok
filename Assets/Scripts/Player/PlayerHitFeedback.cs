using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 주인공이 맞았을 때의 연출: 화면 흔들림, 화면 가장자리 번쩍임, 맞은 자리의 할퀸 자국과 붉은 파편.
// 물귀신 손의 '느려짐' 공격은 피해가 없으므로 푸른빛 번쩍임으로 구분한다.
public class PlayerHitFeedback : MonoBehaviour
{
    [Header("화면 흔들림")]
    [SerializeField] private float shakeAmplitude = 0.18f;
    [SerializeField] private float shakeDuration = 0.25f;

    [Header("화면 가장자리 번쩍임")]
    [SerializeField] private Color damageVignetteColor = new Color(0.75f, 0f, 0f, 0.6f);
    [SerializeField] private Color slowVignetteColor = new Color(0.2f, 0.5f, 1f, 0.45f);
    [SerializeField] private float vignetteFadeTime = 0.45f;

    [Header("할퀸 자국")]
    [SerializeField] private Material clawMaterial;
    [SerializeField] private Color clawColor = new Color(1f, 0.08f, 0.05f, 1f);
    [SerializeField] private float clawLength = 0.9f;
    [SerializeField] private float clawWidth = 0.07f;
    [SerializeField] private float clawLifetime = 0.35f;

    [Header("붉은 파편 (적중 효과 재사용)")]
    [SerializeField] private ParticleSystem hitSparkPrefab;
    [SerializeField] private Color sparkColor = new Color(0.9f, 0.05f, 0.05f, 1f);

    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Transform hitPoint;   // 비워두면 가슴 높이 자동

    private RawImage vignette;
    private Coroutine vignetteRoutine;
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();
        BuildVignette();
    }

    private void OnEnable()
    {
        if (playerHealth != null)
            playerHealth.OnDamaged += HandleDamaged;
        if (playerMovement != null)
            playerMovement.OnSlowed += HandleSlowed;
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnDamaged -= HandleDamaged;
        if (playerMovement != null)
            playerMovement.OnSlowed -= HandleSlowed;
    }

    private void HandleDamaged(float amount)
    {
        if (ThirdPersonCamera.Instance != null)
            ThirdPersonCamera.Instance.Shake(shakeAmplitude, shakeDuration);
        FlashVignette(damageVignetteColor);

        Vector3 p = HitPosition();
        StartCoroutine(ClawRoutine(p));
        SpawnSparks(p);
    }

    private void HandleSlowed()
    {
        if (ThirdPersonCamera.Instance != null)
            ThirdPersonCamera.Instance.Shake(shakeAmplitude * 0.5f, shakeDuration);
        FlashVignette(slowVignetteColor);
    }

    private Vector3 HitPosition()
    {
        if (hitPoint != null)
            return hitPoint.position;
        var cc = GetComponent<CharacterController>();
        float h = cc != null ? cc.center.y + cc.height * 0.15f : 0.3f;   // 가슴 높이
        return transform.position + Vector3.up * h;
    }

    // ---------- 화면 가장자리 번쩍임 ----------
    private void BuildVignette()
    {
        var canvasGo = new GameObject("HitVignetteCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        var imgGo = new GameObject("Vignette");
        imgGo.transform.SetParent(canvasGo.transform, false);
        vignette = imgGo.AddComponent<RawImage>();
        vignette.raycastTarget = false;
        vignette.texture = MakeVignetteTexture(128);
        var rt = vignette.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        vignette.color = new Color(1f, 1f, 1f, 0f);
    }

    private static Texture2D MakeVignetteTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v) / 1.414f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, d));
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private void FlashVignette(Color color)
    {
        if (vignette == null)
            return;
        if (vignetteRoutine != null)
            StopCoroutine(vignetteRoutine);
        vignetteRoutine = StartCoroutine(VignetteRoutine(color));
    }

    private IEnumerator VignetteRoutine(Color color)
    {
        float t = 0f;
        while (t < vignetteFadeTime)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Clamp01(t / vignetteFadeTime);
            vignette.color = new Color(color.r, color.g, color.b, color.a * k * k);
            yield return null;
        }
        vignette.color = new Color(color.r, color.g, color.b, 0f);
    }

    // ---------- 할퀸 자국 (가는 빛줄기 3개) ----------
    private IEnumerator ClawRoutine(Vector3 center)
    {
        if (clawMaterial == null)
            yield break;
        Camera cam = Camera.main;
        if (cam == null)
            yield break;

        Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var root = new GameObject("ClawMark");
        root.transform.position = center + (cam.transform.position - center).normalized * 0.4f;
        root.transform.rotation = Quaternion.LookRotation(root.transform.position - cam.transform.position, cam.transform.up) * Quaternion.Euler(0f, 0f, Random.Range(-60f, -30f));

        var strips = new Transform[3];
        var renderers = new MeshRenderer[3];
        var mpb = new MaterialPropertyBlock();
        for (int i = 0; i < 3; i++)
        {
            var s = new GameObject("Claw" + i);
            s.transform.SetParent(root.transform, false);
            s.transform.localPosition = new Vector3(0f, (i - 1) * clawWidth * 2.6f, 0f);
            s.AddComponent<MeshFilter>().sharedMesh = quad;
            var mr = s.AddComponent<MeshRenderer>();
            mr.sharedMaterial = clawMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            strips[i] = s.transform;
            renderers[i] = mr;
        }

        float t = 0f;
        while (t < clawLifetime)
        {
            t += Time.unscaledDeltaTime;
            float grow = Mathf.Clamp01(t / 0.06f);                   // 빠르게 그어짐
            float fade = 1f - Mathf.Clamp01((t - 0.06f) / (clawLifetime - 0.06f));
            for (int i = 0; i < 3; i++)
            {
                float len = clawLength * (i == 1 ? 1f : 0.8f) * grow;
                strips[i].localScale = new Vector3(len, clawWidth, 1f);
                Color c = clawColor;
                c.a *= fade;
                mpb.SetColor(ColorId, c);
                renderers[i].SetPropertyBlock(mpb);
            }
            yield return null;
        }
        Destroy(root);
    }

    // ---------- 붉은 파편 ----------
    private void SpawnSparks(Vector3 p)
    {
        if (hitSparkPrefab == null)
            return;
        Camera cam = Camera.main;
        Quaternion rot = cam != null ? Quaternion.LookRotation(cam.transform.position - p) : Quaternion.identity;
        ParticleSystem fx = Instantiate(hitSparkPrefab, p, rot);
        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>())
        {
            var main = ps.main;
            main.startColor = sparkColor;
        }
        fx.Play(true);
        Destroy(fx.gameObject, 1.5f);
    }
}
