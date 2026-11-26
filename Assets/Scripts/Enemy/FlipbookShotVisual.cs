using UnityEngine;

// 날아가는 불똥 그림: 2x2 플립북(불덩이가 오른쪽, 꼬리가 왼쪽)을 카메라 정면으로 세우고,
// 화면에서 날아가는 방향으로 불덩이 머리를 돌린다. EnemyProjectile 의 모양 프리팹으로 쓴다.
public class FlipbookShotVisual : MonoBehaviour
{
    [SerializeField] private Texture2D sheet;
    [SerializeField] private float size = 0.8f;
    [SerializeField] private float fps = 14f;

    private Material mat;
    private Mesh mesh;
    private Transform quad;
    private readonly Vector2[] uvs = new Vector2[4];
    private int frame = -1;
    private Vector3 lastPos;
    private float angle;

    private void Awake()
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(q.GetComponent<Collider>());
        quad = q.transform;
        quad.SetParent(transform, false);
        quad.localScale = Vector3.one * size;
        mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = sheet };
        mesh = q.GetComponent<MeshFilter>().mesh;
        var r = q.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lastPos = transform.position;
    }

    private void LateUpdate()
    {
        var cam = Camera.main;
        if (cam != null)
        {
            // 화면에서 움직인 방향 → 그 쪽으로 불덩이 머리(그림의 오른쪽)를 돌림
            Vector3 v = transform.position - lastPos;
            Vector3 sv = cam.transform.InverseTransformDirection(v);
            if (sv.x * sv.x + sv.y * sv.y > 1e-8f) angle = Mathf.Atan2(sv.y, sv.x) * Mathf.Rad2Deg;
            quad.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, angle);
        }
        lastPos = transform.position;
        int f = (int)(Time.time * fps) % 4;
        if (f == frame) return;
        frame = f;   // Sprites/Default 는 재질 타일링을 무시해서 UV 를 직접 한 칸으로
        float u = (f % 2) * 0.5f, w = f < 2 ? 0.5f : 0f;
        uvs[0] = new Vector2(u, w); uvs[1] = new Vector2(u + 0.5f, w);
        uvs[2] = new Vector2(u, w + 0.5f); uvs[3] = new Vector2(u + 0.5f, w + 0.5f);
        mesh.uv = uvs;
    }

    private void OnDestroy()
    {
        if (mat != null) Destroy(mat);
        if (mesh != null) Destroy(mesh);
    }
}
