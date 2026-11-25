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
    [Tooltip("넣으면 붉은 파편 대신 붓 그림 먹물이 터짐")]
    [SerializeField] private Texture2D hitInkTexture;

    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Transform hitPoint;   // 비워두면 가슴 높이 자동

    [Header("때린 잡몹 모양 (잡몹이 SetNextHit 으로 넘겨줌)")]
    [SerializeField] private float hitMarkSize = 1.1f;
    [SerializeField] private float hitMarkLifetime = 0.5f;

    private RawImage vignette;
    private Coroutine vignetteRoutine;
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

    // 다음 피해 한 번에만 쓰는 때린 잡몹의 모양
    private bool hasNextHit;
    private Color nextInk, nextSecond;
    private Material nextMark;
    private ParticleSystem nextSplatter;
    private Texture nextFrame;
    private Texture defaultVignetteTexture;

    public void SetNextHit(Color ink, Color second, Material mark, ParticleSystem splatter, Texture frame)
    {
        hasNextHit = true;
        nextInk = ink;
        nextSecond = second;
        nextMark = mark;
        nextSplatter = splatter;
        nextFrame = frame;
    }

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
        if (PlayStyledHit())
            return;
        Vector3 p = HitPosition();
        vignette.texture = defaultVignetteTexture;
        FlashVignette(damageVignetteColor);
        StartCoroutine(ClawRoutine(p));
        SpawnSparks(p);
    }

    // 잡몹이 모양을 넘겨줬으면 그 모양으로 맞은 연출 (피해·느려짐 공통)
    private bool PlayStyledHit()
    {
        if (!hasNextHit)
            return false;
        Vector3 p = HitPosition();
        {
            hasNextHit = false;
            // 가장자리: 잡몹 테두리 그림이 있으면 그 그림, 없으면 두 번째 색(먹 색만 쓰면 너무 어두워 안 보임)
            vignette.texture = nextFrame != null ? nextFrame : defaultVignetteTexture;
            FlashVignette(nextFrame != null ? Color.white : new Color(nextSecond.r, nextSecond.g, nextSecond.b, damageVignetteColor.a));
            if (nextMark != null)
                StartCoroutine(MarkRoutine(p, nextMark));
            else
                StartCoroutine(ClawRoutine(p));
            if (nextSplatter != null)
                SpawnInk(p, nextSplatter, nextInk, nextSecond);
            else
                SpawnSparks(p);
        }
        return true;
    }

    // 때린 잡몹의 상처 그림: 카메라 쪽 몸 앞에 붓으로 긋듯 펼쳐졌다가 사라짐
    private IEnumerator MarkRoutine(Vector3 center, Material mat)
    {
        Camera cam = Camera.main;
        if (cam == null)
            yield break;
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(go.GetComponent<Collider>());
        Destroy(go, hitMarkLifetime);
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Texture tex = mat.mainTexture;
        float aspect = tex != null ? (float)tex.width / tex.height : 1f;
        float roll = Random.Range(-25f, 25f);
        var mpb = new MaterialPropertyBlock();
        for (float t = 0f; t < hitMarkLifetime && go != null; t += Time.unscaledDeltaTime)
        {
            Vector3 pos = center + (cam.transform.position - center).normalized * 0.4f;
            go.transform.rotation = Quaternion.LookRotation(pos - cam.transform.position) * Quaternion.Euler(0f, 0f, roll);
            float draw = Mathf.Clamp01(t / 0.06f);
            float fade = 1f - Mathf.Clamp01((t - 0.12f) / (hitMarkLifetime - 0.12f));
            float w = aspect * hitMarkSize;
            go.transform.position = pos + go.transform.right * w * (draw - 1f) * 0.5f;
            go.transform.localScale = new Vector3(w * Mathf.Max(draw, 0.001f), hitMarkSize, 1f);
            mpb.SetVector(BaseMapStId, new Vector4(draw, 1f, 0f, 0f));
            mpb.SetColor(BaseColorId, new Color(1f, 1f, 1f, fade));
            mr.SetPropertyBlock(mpb);
            yield return null;
        }
    }

    // 먹과 두 번째 색이 섞여 카메라 쪽으로 튐
    private void SpawnInk(Vector3 p, ParticleSystem prefab, Color ink, Color second)
    {
        Camera cam = Camera.main;
        // 카메라 쪽으로 튀면 화면을 덮으므로 화면 옆(좌우 중 한쪽, 살짝 위)으로 튀게
        Vector3 side = cam != null ? cam.transform.right * (Random.value < 0.5f ? -1f : 1f) + Vector3.up * 0.4f : Vector3.up;
        ParticleSystem fx = Instantiate(prefab, p, Quaternion.LookRotation(side));
        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>())
        {
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(ink, second);   // 두 색 중 무작위
            main.startSizeMultiplier *= 0.7f;    // 잡몹 할퀴기용보다 작게
            main.startSpeedMultiplier *= 0.6f;
        }
        fx.Play(true);
    }

    private void HandleSlowed()
    {
        if (ThirdPersonCamera.Instance != null)
            ThirdPersonCamera.Instance.Shake(shakeAmplitude * 0.5f, shakeDuration);
        if (PlayStyledHit())
            return;
        vignette.texture = defaultVignetteTexture;
        FlashVignette(slowVignetteColor);
    }

    private Vector3 HitPosition()
    {
        if (hitPoint != null)
            return hitPoint.position;
        var anim = GetComponentInChildren<Animator>();
        Transform chest = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Chest) : null;
        if (chest != null)
            return chest.position;
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
        vignette.texture = defaultVignetteTexture = MakeVignetteTexture(128);
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
                // 붓으로 번진 테두리: 각도·거리에 따라 안쪽 경계를 들쭉날쭉하게
                float ang = Mathf.Atan2(v, u);
                d += (Mathf.PerlinNoise(ang * 2.5f + 10f, d * 3f) - 0.5f) * 0.22f + (Mathf.PerlinNoise(u * 9f, v * 9f) - 0.5f) * 0.08f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.9f, d));
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
        if (hitInkTexture != null)
        {
            InkFxSprite.Spawn(hitInkTexture, p, 1.4f, Random.Range(-30f, 30f), Random.value < 0.5f, 0.45f, 0.5f, Color.white);
            return;
        }
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
