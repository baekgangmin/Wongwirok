using System.Collections.Generic;
using UnityEngine;

// 칼날 궤적(잔상). 칼날 밑동과 칼끝 위치를 매 프레임 기록해서, 지나간 면을 빛나는 띠 메시로 그린다.
// PlayerAttack이 칼을 휘두르는 구간에만 SetEmitting(true)로 켠다.
public class SwordSlashTrail : MonoBehaviour
{
    [Header("칼날 위치")]
    [SerializeField] private Transform bladeBase;
    [SerializeField] private Transform bladeTip;

    [Header("모양")]
    [SerializeField] private Material trailMaterial;
    [SerializeField] private float lifetime = 0.16f;
    [SerializeField] private float strongLifetime = 0.24f;
    [SerializeField, Range(1, 8)] private int subdivisions = 4;

    [Header("색 (왼쪽 = 방금 지나간 자리, 오른쪽 = 사라지는 끝)")]
    [SerializeField] private Gradient colorOverLife = DefaultGradient();
    [SerializeField] private float strongBrightness = 1.6f;

    private struct Sample
    {
        public float time;
        public Vector3 baseP;
        public Vector3 tipP;
        public bool segmentStart;
    }

    private readonly List<Sample> samples = new List<Sample>();
    private readonly List<Vector3> verts = new List<Vector3>();
    private readonly List<Color> colors = new List<Color>();
    private readonly List<Vector2> uvs = new List<Vector2>();
    private readonly List<int> tris = new List<int>();

    private Mesh mesh;
    private GameObject meshObject;
    private bool emitting;
    private bool strong;
    private bool newSegment = true;

    public void SetColor(Gradient gradient)
    {
        if (gradient != null)
            colorOverLife = gradient;
    }

    public void SetEmitting(bool on, bool strongHit)
    {
        if (on && !emitting)
            newSegment = true;
        emitting = on;
        if (on)
            strong = strongHit;
    }

    private void LateUpdate()
    {
        Tick(Time.time);
    }

    private void OnDisable()
    {
        samples.Clear();
        if (mesh != null)
            mesh.Clear();
    }

    private void OnDestroy()
    {
        if (meshObject != null)
            Destroy(meshObject);
    }

    // 시간을 받아 한 프레임 진행 (에디터 검증용으로도 쓸 수 있게 공개)
    public void Tick(float now)
    {
        if (bladeBase == null || bladeTip == null)
            return;
        EnsureMesh();

        if (emitting)
        {
            samples.Add(new Sample { time = now, baseP = bladeBase.position, tipP = bladeTip.position, segmentStart = newSegment });
            newSegment = false;
        }

        float life = strong ? strongLifetime : lifetime;
        while (samples.Count > 0 && now - samples[0].time > life)
            samples.RemoveAt(0);
        if (samples.Count > 0 && !samples[0].segmentStart)
        {
            Sample s = samples[0];
            s.segmentStart = true;
            samples[0] = s;
        }

        BuildMesh(now, life);
    }

    private void EnsureMesh()
    {
        if (mesh != null)
            return;
        mesh = new Mesh { name = "SwordSlashTrail" };
        mesh.MarkDynamic();
        meshObject = new GameObject("SwordSlashTrailMesh");
        meshObject.hideFlags = HideFlags.DontSave;
        meshObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = meshObject.AddComponent<MeshRenderer>();
        mr.sharedMaterial = trailMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    private void BuildMesh(float now, float life)
    {
        verts.Clear(); colors.Clear(); uvs.Clear(); tris.Clear();
        float brightness = strong ? strongBrightness : 1f;

        int segStart = 0;
        for (int i = 1; i <= samples.Count; i++)
        {
            bool endOfSegment = i == samples.Count || samples[i].segmentStart;
            if (!endOfSegment)
                continue;
            AppendSegment(segStart, i - 1, now, life, brightness);
            segStart = i;
        }

        mesh.Clear();
        if (verts.Count < 4)
            return;
        mesh.SetVertices(verts);
        mesh.SetColors(colors);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
    }

    // 한 구간(연속으로 기록된 샘플들)을 곡선으로 보간해서 띠를 만든다.
    private void AppendSegment(int from, int to, float now, float life, float brightness)
    {
        if (to - from < 1)
            return;

        int firstVert = verts.Count;
        for (int i = from; i <= to; i++)
        {
            int steps = i == to ? 1 : subdivisions;
            for (int s = 0; s < steps; s++)
            {
                float t = (float)s / subdivisions;
                Sample p0 = samples[Mathf.Max(from, i - 1)];
                Sample p1 = samples[i];
                Sample p2 = samples[Mathf.Min(to, i + 1)];
                Sample p3 = samples[Mathf.Min(to, i + 2)];
                Vector3 b = CatmullRom(p0.baseP, p1.baseP, p2.baseP, p3.baseP, t);
                Vector3 tip = CatmullRom(p0.tipP, p1.tipP, p2.tipP, p3.tipP, t);
                float time = Mathf.Lerp(p1.time, p2.time, t);
                float age = Mathf.Clamp01((now - time) / life);
                Color c = colorOverLife.Evaluate(age);
                c.r *= brightness; c.g *= brightness; c.b *= brightness;

                verts.Add(b); verts.Add(tip);
                colors.Add(c); colors.Add(c);
                uvs.Add(new Vector2(age, 0f)); uvs.Add(new Vector2(age, 1f));
            }
        }

        int count = (verts.Count - firstVert) / 2;
        for (int k = 0; k < count - 1; k++)
        {
            int a = firstVert + k * 2;
            tris.Add(a); tris.Add(a + 1); tris.Add(a + 2);
            tris.Add(a + 1); tris.Add(a + 3); tris.Add(a + 2);
        }
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    // 기본 색: 푸르스름한 흰빛(영혼빛)
    private static Gradient DefaultGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(0.7f, 0.9f, 1f), 0f), new GradientColorKey(new Color(0.35f, 0.6f, 1f), 0.35f), new GradientColorKey(new Color(0.2f, 0.35f, 1f), 1f) },
            new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0.22f, 0.45f), new GradientAlphaKey(0f, 1f) });
        return g;
    }
}
