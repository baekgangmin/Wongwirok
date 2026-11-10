using UnityEngine;

public class WillOWispFloatEffect : MonoBehaviour
{
    [Header("Float Motion")]
    [SerializeField] private Transform visual;
    [SerializeField] private float bobHeight = 0.25f;
    [SerializeField] private float bobSpeed = 1.2f;
    [SerializeField] private float swayRadius = 0.15f;
    [SerializeField] private float swaySpeed = 0.7f;

    [Header("Flame Flicker")]
    [SerializeField] private Renderer flameRenderer;
    [SerializeField] private Color flameColor = new Color(1f, 0.45f, 0.1f);
    [SerializeField] private float emissionMinIntensity = 1.5f;
    [SerializeField] private float emissionMaxIntensity = 4f;
    [SerializeField] private float flickerSpeed = 8f;

    [Header("Flame Light")]
    [SerializeField] private Light flameLight;
    [SerializeField] private float lightMinIntensity = 1f;
    [SerializeField] private float lightMaxIntensity = 3f;

    [Header("Shape Distortion")]
    [SerializeField] private MeshFilter flameMeshFilter;
    [SerializeField] private float distortAmount = 0.03f;
    [SerializeField] private float distortSpeed = 2.5f;
    [SerializeField] private float distortNoiseScale = 4f;

    private Vector3 basePosition;
    private Material flameMaterialInstance;
    private float noiseSeed;

    private Mesh flameMeshInstance;
    private Vector3[] baseVertices;
    private Vector3[] baseNormals;
    private Vector3[] workingVertices;

    private void Awake()
    {
        if (visual == null)
            visual = transform;
        basePosition = visual.localPosition;

        if (flameRenderer != null)
        {
            flameMaterialInstance = flameRenderer.material;
            flameMaterialInstance.EnableKeyword("_EMISSION");
        }

        noiseSeed = Random.Range(0f, 100f);

        if (flameMeshFilter != null && flameMeshFilter.sharedMesh != null && !flameMeshFilter.sharedMesh.isReadable)
        {
            // 모델의 Read/Write가 꺼져 있으면 정점을 바꿀 수 없으므로, 매 프레임 오류를 내는 대신 꿀렁임 효과만 건너뛴다.
            Debug.LogWarning($"[{name}] '{flameMeshFilter.sharedMesh.name}' 메시의 Read/Write가 꺼져 있어 불꽃 꿀렁임 효과를 건너뜁니다. 모델 Import 설정에서 Read/Write를 켜 주세요.", this);
        }
        else if (flameMeshFilter != null && flameMeshFilter.sharedMesh != null)
        {
            flameMeshInstance = Instantiate(flameMeshFilter.sharedMesh);
            flameMeshFilter.mesh = flameMeshInstance;
            baseVertices = flameMeshInstance.vertices;
            baseNormals = flameMeshInstance.normals;
            workingVertices = new Vector3[baseVertices.Length];
        }
    }

    private void Update()
    {
        float t = Time.time;

        Vector3 offset = new Vector3(
            Mathf.Sin(t * swaySpeed + noiseSeed) * swayRadius,
            Mathf.Sin(t * bobSpeed + noiseSeed) * bobHeight,
            Mathf.Cos(t * swaySpeed * 0.8f + noiseSeed) * swayRadius
        );
        visual.localPosition = basePosition + offset;

        float flicker = Mathf.PerlinNoise(t * flickerSpeed, noiseSeed);
        float intensity = Mathf.Lerp(emissionMinIntensity, emissionMaxIntensity, flicker);

        if (flameMaterialInstance != null)
            flameMaterialInstance.SetColor("_EmissionColor", flameColor * intensity);

        if (flameLight != null)
            flameLight.intensity = Mathf.Lerp(lightMinIntensity, lightMaxIntensity, flicker);

        if (flameMeshInstance != null)
        {
            for (int i = 0; i < baseVertices.Length; i++)
            {
                Vector3 v = baseVertices[i];
                float n = Mathf.PerlinNoise(
                    (v.x + v.y) * distortNoiseScale + noiseSeed,
                    t * distortSpeed + v.z * distortNoiseScale
                ) - 0.5f;
                workingVertices[i] = v + baseNormals[i] * (n * distortAmount);
            }
            flameMeshInstance.vertices = workingVertices;
            flameMeshInstance.RecalculateNormals();
            flameMeshInstance.RecalculateBounds();
        }
    }

    private void OnDestroy()
    {
        if (flameMaterialInstance != null)
            Destroy(flameMaterialInstance);
        if (flameMeshInstance != null)
            Destroy(flameMeshInstance);
    }
}
