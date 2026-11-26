using UnityEngine;

// 붓 그림 불길: 2x2 플립북 그림을 카메라 쪽으로 세워 초당 몇 장씩 넘겨 일렁이게 하고,
// 같은 자리의 불빛(있으면)을 함께 깜빡인다. 실내가 불타는 2스테이지용.
[DefaultExecutionOrder(-100)]   // 판을 Awake 에서 만들어, 불똥귀처럼 몸이 플립북일 때 맞음·색 처리 코드가 이 판을 찾게
public class FireFlipbook : MonoBehaviour
{
    [SerializeField] private Texture2D sheet;
    [SerializeField] private float size = 1.2f;
    [SerializeField] private float fps = 10f;
    [SerializeField] private Light flickerLight;
    [Header("불마다 다르게 (같은 불이 반복돼 보이지 않게)")]
    [Tooltip("있으면 sheet 와 함께 이 중 하나를 무작위로 고른다 (모양이 다른 불)")]
    [SerializeField] private Texture2D[] variants;
    [SerializeField] private bool randomLook = true;   // 좌우 뒤집기 · 세로 늘이기 · 색 살짝
    [SerializeField] private Vector2 stretchRange = new Vector2(0.8f, 1.6f);
    private static readonly Color[] Tints = { new Color(1f, 0.82f, 0.78f), Color.white, new Color(1f, 1f, 0.8f) };   // 붉은 · 그대로 · 노란빛

    private Material mat;
    private Mesh mesh;
    private readonly Vector2[] uvs = new Vector2[4];
    private int frame = -1;
    private Transform quad;
    private float timeOffset;
    private float baseIntensity;

    private void Awake()
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(q.GetComponent<Collider>());
        quad = q.transform;
        quad.SetParent(transform, false);
        float stretch = randomLook ? Random.Range(stretchRange.x, stretchRange.y) : 1f;
        quad.localPosition = Vector3.up * size * stretch * 0.3f;   // 그림 속 불 밑동(칸 아래쪽 약 20% 위)이 바닥에 닿게
        quad.localScale = new Vector3(randomLook && Random.value < 0.5f ? -size : size, size * stretch, size);
        var tex = sheet;
        if (variants != null && variants.Length > 0)
        {
            int k = Random.Range(0, variants.Length + 1);
            if (k < variants.Length && variants[k] != null) tex = variants[k];
        }
        mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };
        if (randomLook) mat.color = Tints[Random.Range(0, Tints.Length)];
        mesh = q.GetComponent<MeshFilter>().mesh;   // 이 불만의 사본 (칸 고르기용 UV 를 바꿈)
        var r = q.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        timeOffset = Random.value * 10f;   // 여러 불길이 같은 박자로 움직이지 않게
        if (flickerLight != null) baseIntensity = flickerLight.intensity;
    }

    private void LateUpdate()
    {
        var cam = Camera.main;
        if (cam != null)
        {
            // 세로는 세운 채 카메라 쪽으로만 돈다 (불길이 눕지 않게)
            Vector3 d = quad.position - cam.transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.001f) quad.rotation = Quaternion.LookRotation(d);
        }
        int f = (int)((Time.time + timeOffset) * fps) % 4;
        if (f != frame)
        {
            // Sprites/Default 는 재질의 타일링(크기·오프셋)을 무시해서, 판자 UV 를 직접 칸 하나로 바꾼다
            frame = f;
            float u = (f % 2) * 0.5f, v = f < 2 ? 0.5f : 0f;   // 왼쪽 위부터 오른쪽 아래 순서
            uvs[0] = new Vector2(u, v); uvs[1] = new Vector2(u + 0.5f, v);
            uvs[2] = new Vector2(u, v + 0.5f); uvs[3] = new Vector2(u + 0.5f, v + 0.5f);
            mesh.uv = uvs;
        }
        if (flickerLight != null)
            flickerLight.intensity = baseIntensity * (0.75f + 0.5f * Mathf.PerlinNoise(Time.time * 6f, timeOffset));
    }

    private void OnDestroy()
    {
        if (mat != null) Destroy(mat);
        if (mesh != null) Destroy(mesh);
    }
}
