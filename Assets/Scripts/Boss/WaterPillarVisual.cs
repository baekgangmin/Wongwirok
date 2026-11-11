using System.Collections;
using UnityEngine;

// 보스 물기둥 겉모습.
// - 물 원통(코드로 만든 열린 관 메시 + WaterColumn 셰이더)이 아래에서 위로 솟고(_Rise 0→1), 무너질 때 위에서 아래로 사라짐
// - 바닥 물튀김(솟을 때/무너질 때), 꼭대기 물보라(계속), 안쪽 거품(계속)
// 프리팹 구조: 이 컴포넌트 + 자식 Column(MeshFilter/MeshRenderer), BaseSplash, TopSpray, Bubbles(ParticleSystem)
public class WaterPillarVisual : MonoBehaviour
{
    [SerializeField] private MeshFilter columnFilter;
    [SerializeField] private MeshRenderer columnRenderer;
    [SerializeField] private ParticleSystem baseSplash;
    [SerializeField] private ParticleSystem topSpray;
    [SerializeField] private ParticleSystem bubbles;
    [SerializeField] private float riseTime = 0.22f;
    [SerializeField] private float collapseTime = 0.35f;

    private static Mesh tubeMesh;
    private static readonly int RiseId = Shader.PropertyToID("_Rise");
    private Material columnMaterial;
    private float height = 3f;
    private float radius = 1.5f;
    private float rise;
    private bool collapsing;

    // 기둥을 세운다 (radius = 바닥 반지름, height = 높이)
    public void Play(float pillarHeight, float pillarRadius)
    {
        height = pillarHeight;
        radius = pillarRadius;
        if (tubeMesh == null)
            tubeMesh = BuildTube(32, 24);
        columnFilter.sharedMesh = tubeMesh;
        columnFilter.transform.localScale = new Vector3(radius, height, radius);
        columnMaterial = columnRenderer.material;   // 기둥마다 따로
        SetRise(0f);

        if (baseSplash != null)
        {
            var shape = baseSplash.shape;
            shape.radius = radius;
            baseSplash.Play(true);
        }
        if (bubbles != null)
        {
            var shape = bubbles.shape;
            shape.radius = radius * 0.6f;
            var main = bubbles.main;
            main.startLifetime = Mathf.Max(0.5f, height / 2.5f);
            bubbles.Play(true);
        }
        if (topSpray != null)
        {
            var shape = topSpray.shape;
            shape.radius = radius * 0.7f;
            topSpray.Play(true);
        }
        StartCoroutine(RiseRoutine());
    }

    // 기둥이 무너진다. 끝나면 스스로 사라짐
    public void Collapse()
    {
        if (collapsing)
            return;
        collapsing = true;
        StopAllCoroutines();
        StartCoroutine(CollapseRoutine());
    }

    private IEnumerator RiseRoutine()
    {
        for (float t = 0f; t < riseTime; t += Time.deltaTime)
        {
            // 처음에 빠르게 치솟고 끝에서 살짝 넘쳤다가 자리잡음
            float k = t / riseTime;
            SetRise(Mathf.Min(1f, 1.08f * (1f - (1f - k) * (1f - k))));
            yield return null;
        }
        SetRise(1f);
    }

    private IEnumerator CollapseRoutine()
    {
        if (topSpray != null)
            topSpray.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (bubbles != null)
            bubbles.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        float start = rise;
        for (float t = 0f; t < collapseTime; t += Time.deltaTime)
        {
            float k = t / collapseTime;
            SetRise(start * (1f - k * k));   // 점점 빨리 무너짐
            yield return null;
        }
        SetRise(0f);

        // 무너진 물이 바닥을 치며 한 번 더 튐
        if (baseSplash != null)
            baseSplash.Emit(40);
        Destroy(gameObject, 1.5f);
    }

    private void SetRise(float value)
    {
        rise = value;
        if (columnMaterial != null)
            columnMaterial.SetFloat(RiseId, value);
        // 꼭대기 물보라는 물기둥 끝을 따라감
        if (topSpray != null)
            topSpray.transform.localPosition = Vector3.up * height * Mathf.Clamp01(value);
        columnRenderer.enabled = value > 0.001f;
    }

    private void OnDestroy()
    {
        if (columnMaterial != null)
            Destroy(columnMaterial);
    }

    // 높이 1, 반지름 1 기준의 열린 관. 아래는 살짝 퍼지고(물이 튀어 오르는 뿌리), 위로 갈수록 조금 좁아짐
    private static Mesh BuildTube(int segments, int rings)
    {
        var verts = new Vector3[(segments + 1) * (rings + 1)];
        var normals = new Vector3[verts.Length];
        var uvs = new Vector2[verts.Length];
        var tris = new int[segments * rings * 6];
        for (int r = 0; r <= rings; r++)
        {
            float v = (float)r / rings;
            float profile = 1f + 0.35f * Mathf.Pow(1f - v, 6f) - 0.18f * v;   // 뿌리 퍼짐 + 위로 좁아짐
            for (int s = 0; s <= segments; s++)
            {
                float u = (float)s / segments;
                float a = u * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                int i = r * (segments + 1) + s;
                verts[i] = dir * profile + Vector3.up * v;
                normals[i] = dir;
                uvs[i] = new Vector2(u, v);
            }
        }
        int ti = 0;
        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                int a = r * (segments + 1) + s;
                int b = a + segments + 1;
                tris[ti++] = a; tris[ti++] = b; tris[ti++] = a + 1;
                tris[ti++] = a + 1; tris[ti++] = b; tris[ti++] = b + 1;
            }
        }
        var mesh = new Mesh { name = "WaterPillarTube" };
        mesh.vertices = verts;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        mesh.bounds = new Bounds(Vector3.up * 0.5f, new Vector3(3f, 1.5f, 3f));   // 출렁임 여유
        return mesh;
    }
}
