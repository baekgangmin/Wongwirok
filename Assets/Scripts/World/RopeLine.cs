using UnityEngine;

// 두 점 사이에 늘어진 밧줄을 그린다. 땅을 뚫지 않도록 지형 위로 올려준다.
[RequireComponent(typeof(LineRenderer))]
public class RopeLine : MonoBehaviour
{
    public Transform startPoint;
    public Vector3 startOffset;
    public Transform endPoint;
    public Vector3 endOffset;
    [Tooltip("처지는 정도 (0 = 팽팽함)")]
    public float slack = 0.25f;
    [SerializeField] private int segments = 28;
    [SerializeField] private float groundClearance = 0.04f;

    private LineRenderer line;

    public static RopeLine Create(Transform parent, Material material, float width = 0.035f)
    {
        var go = new GameObject("Rope");
        if (parent != null) go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = material;
        lr.widthMultiplier = width;
        lr.numCapVertices = 2;
        lr.textureMode = LineTextureMode.Tile;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        return go.AddComponent<RopeLine>();
    }

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = segments + 1;
    }

    private void LateUpdate()
    {
        if (startPoint == null || endPoint == null)
        {
            line.enabled = false;
            return;
        }
        line.enabled = true;
        Vector3 a = startPoint.position + startOffset;
        Vector3 b = endPoint.position + endOffset;
        float len = Vector3.Distance(a, b);
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            Vector3 p = Vector3.Lerp(a, b, t);
            p.y -= slack * len * 0.25f * 4f * t * (1f - t);
            float g = GroundHeight(p);
            if (p.y < g + groundClearance && t > 0.02f && t < 0.98f)
                p.y = g + groundClearance;
            line.SetPosition(i, p);
        }
    }

    private static float GroundHeight(Vector3 p)
    {
        float best = float.NegativeInfinity;
        foreach (var t in Terrain.activeTerrains)
        {
            var tp = t.transform.position; var s = t.terrainData.size;
            if (p.x < tp.x || p.x > tp.x + s.x || p.z < tp.z || p.z > tp.z + s.z) continue;
            best = Mathf.Max(best, t.SampleHeight(p) + tp.y);
        }
        return best;
    }
}
