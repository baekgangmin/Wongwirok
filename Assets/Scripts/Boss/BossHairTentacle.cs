using UnityEngine;

// 3D 머리카락 촉수 모델을 곡선을 따라 휘게 만든다 (뼈대 없이 정점을 직접 옮김).
// - 원본 메시는 곧게 뻗은 촉수여야 한다. 가장 긴 축을 길이 방향으로 보고, 굵은 쪽 끝을 뿌리로 본다.
// - Apply(곡선, reveal): 뿌리(0)부터 reveal까지의 곡선 구간에 촉수 전체를 늘려 입힌다 → reveal을 0→1로 올리면 쑥 자라남.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class BossHairTentacle : MonoBehaviour
{
    private const int Samples = 32;

    private UnityEngine.Mesh mesh;
    private float[] vT;          // 정점의 길이 방향 위치 (0 뿌리 ~ 1 끝)
    private Vector2[] vSide;     // 정점의 옆 방향 위치 (원본 굵기 기준)
    private Vector3[] nLocal;    // 정점 법선 (길이축, 옆1, 옆2 성분)
    private Vector3[] verts;
    private Vector3[] normals;
    private readonly Vector3[] cp = new Vector3[Samples + 1];
    private readonly Vector3[] ct = new Vector3[Samples + 1];
    private readonly Vector3[] cn = new Vector3[Samples + 1];
    private readonly Vector3[] cb = new Vector3[Samples + 1];
    private float sideScale = 1f;

    // radius: 뿌리 쪽 굵기(반지름, m). 모델의 Read/Write가 꺼져 있으면 false (플레이 중엔 점을 읽을 수 없음)
    // rootTrim: 뿌리 쪽에서 잘라낼 비율 (0.19면 앞 19%의 둥근 뿌리 덩어리를 없애고 몸통부터 씀)
    public bool Init(UnityEngine.Mesh source, Material[] materials, float radius, float rootTrim = 0f)
    {
        if (!source.isReadable)
        {
            Debug.LogWarning($"머리카락 촉수 모델 '{source.name}'의 Read/Write가 꺼져 있어 휘게 만들 수 없습니다. 모델 가져오기 설정에서 Read/Write를 켜 주세요.");
            return false;
        }
        mesh = Instantiate(source);
        mesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = mesh;
        var mr = GetComponent<MeshRenderer>();
        mr.sharedMaterials = materials;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

        Vector3[] src = source.vertices;
        Vector3[] srcN = source.normals;
        Bounds b = source.bounds;
        // 가장 긴 축 = 길이 방향
        int axis = b.size.x >= b.size.y && b.size.x >= b.size.z ? 0 : (b.size.y >= b.size.z ? 1 : 2);
        int s1 = (axis + 1) % 3, s2 = (axis + 2) % 3;
        float min = b.min[axis], len = Mathf.Max(0.0001f, b.size[axis]);
        Vector2 center = new Vector2(b.center[s1], b.center[s2]);

        // 어느 끝이 굵은지 (뿌리) 확인
        float rLow = 0f, rHigh = 0f; int nLow = 0, nHigh = 0;
        for (int i = 0; i < src.Length; i++)
        {
            float t = (src[i][axis] - min) / len;
            float r = (new Vector2(src[i][s1], src[i][s2]) - center).magnitude;
            if (t < 0.15f) { rLow += r; nLow++; }
            else if (t > 0.85f) { rHigh += r; nHigh++; }
        }
        bool flip = nLow > 0 && nHigh > 0 && rHigh / nHigh > rLow / nLow;
        rootTrim = Mathf.Clamp(rootTrim, 0f, 0.8f);

        // 잘라낸 뒤 새 뿌리 부근(trim ~ trim+10%)의 굵기를 기준으로 굵기를 맞춤
        float rSum = 0f; int rCount = 0;
        for (int i = 0; i < src.Length; i++)
        {
            float t = (src[i][axis] - min) / len;
            if (flip) t = 1f - t;
            if (t >= rootTrim && t <= rootTrim + 0.1f)
            {
                rSum += (new Vector2(src[i][s1], src[i][s2]) - center).magnitude;
                rCount++;
            }
        }
        float baseRadius = Mathf.Max(0.0001f, rCount > 0 ? rSum / rCount : 0.0001f);
        sideScale = radius / baseRadius;

        vT = new float[src.Length];
        vSide = new Vector2[src.Length];
        nLocal = new Vector3[src.Length];
        float[] rawT = new float[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            float t = (src[i][axis] - min) / len;
            if (flip) t = 1f - t;
            rawT[i] = t;
            vT[i] = Mathf.Clamp01((t - rootTrim) / (1f - rootTrim));
            vSide[i] = new Vector2(src[i][s1], src[i][s2]) - center;
            Vector3 n = srcN != null && srcN.Length == src.Length ? srcN[i] : Vector3.up;
            nLocal[i] = new Vector3(flip ? -n[axis] : n[axis], n[s1], n[s2]);
        }
        // 잘라낸 부분(뿌리 덩어리)에 걸친 면은 그리지 않음
        if (rootTrim > 0f)
        {
            for (int sm = 0; sm < mesh.subMeshCount; sm++)
            {
                int[] tris = mesh.GetTriangles(sm);
                var kept = new System.Collections.Generic.List<int>(tris.Length);
                for (int k = 0; k < tris.Length; k += 3)
                {
                    if (rawT[tris[k]] < rootTrim || rawT[tris[k + 1]] < rootTrim || rawT[tris[k + 2]] < rootTrim)
                        continue;
                    kept.Add(tris[k]); kept.Add(tris[k + 1]); kept.Add(tris[k + 2]);
                }
                mesh.SetTriangles(kept, sm);
            }
        }

        verts = new Vector3[src.Length];
        normals = new Vector3[src.Length];
        return true;
    }

    // curve(u): u = 0 뿌리 ~ 1 끝의 월드 좌표. reveal: 지금 뻗어 있는 비율
    public void Apply(System.Func<float, Vector3> curve, float reveal)
    {
        if (mesh == null)
            return;
        reveal = Mathf.Clamp(reveal, 0.001f, 1f);
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        transform.localScale = Vector3.one;

        for (int k = 0; k <= Samples; k++)
            cp[k] = curve(reveal * k / Samples);
        for (int k = 0; k <= Samples; k++)
        {
            Vector3 d = cp[Mathf.Min(k + 1, Samples)] - cp[Mathf.Max(k - 1, 0)];
            ct[k] = d.sqrMagnitude > 1e-8f ? d.normalized : (k > 0 ? ct[k - 1] : Vector3.forward);
        }
        // 비틀림 없이 이어지는 기준 방향 (평행 이동)
        Vector3 n0 = Vector3.Cross(ct[0], Vector3.up);
        if (n0.sqrMagnitude < 1e-4f) n0 = Vector3.Cross(ct[0], Vector3.right);
        cn[0] = n0.normalized;
        cb[0] = Vector3.Cross(ct[0], cn[0]);
        for (int k = 1; k <= Samples; k++)
        {
            Quaternion q = Quaternion.FromToRotation(ct[k - 1], ct[k]);
            cn[k] = (q * cn[k - 1]).normalized;
            cb[k] = Vector3.Cross(ct[k], cn[k]);
        }

        for (int i = 0; i < verts.Length; i++)
        {
            float f = vT[i] * Samples;
            int k = Mathf.Min((int)f, Samples - 1);
            float w = f - k;
            Vector3 p = Vector3.LerpUnclamped(cp[k], cp[k + 1], w);
            Vector3 t = Vector3.LerpUnclamped(ct[k], ct[k + 1], w);
            Vector3 n = Vector3.LerpUnclamped(cn[k], cn[k + 1], w);
            Vector3 b = Vector3.LerpUnclamped(cb[k], cb[k + 1], w);
            Vector2 s = vSide[i] * sideScale;
            verts[i] = p + n * s.x + b * s.y;
            Vector3 nl = nLocal[i];
            normals[i] = (t * nl.x + n * nl.y + b * nl.z).normalized;
        }
        mesh.vertices = verts;
        mesh.normals = normals;
        mesh.RecalculateBounds();
    }

    private void OnDestroy()
    {
        if (mesh != null)
            Destroy(mesh);
    }
}
