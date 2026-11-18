using UnityEngine;

// 서 있기·걷기·달리기 중에만 양팔의 팔·팔꿈치·손목을 직접 정한 값으로 덮어쓴다 (공격·구르기 등은 애니메이션 그대로)
// 방향은 캐릭터 기준 (x = 오른쪽, y = 위, z = 앞). 손목 각도 0,0,0 = 아래팔과 일자로 편 손목
[RequireComponent(typeof(Animator))]
public class ArmPose : MonoBehaviour
{
    [System.Serializable]
    public class Arm
    {
        [Range(0, 1)] public float weight = 1f;                     // 0 = 애니메이션 그대로, 1 = 아래 값 그대로
        public Vector3 upperArm = new Vector3(0.25f, -0.95f, 0.1f); // 어깨 → 팔꿈치 방향
        [Range(0, 150)] public float elbowBend = 15f;               // 팔꿈치를 앞으로 굽히는 각도
        [Range(-90, 90)] public float wristUpDown = 0f;             // 손목 위아래 꺾기 (+ = 손바닥 쪽)
        [Range(-60, 60)] public float wristSide = 0f;               // 손목 좌우 꺾기 (+ = 엄지 쪽)
        [Range(-180, 180)] public float wristTwist = 0f;            // 손목 비틀기

        public static Arm Lerp(Arm a, Arm b, float t) => new Arm
        {
            weight = Mathf.Lerp(a.weight, b.weight, t),
            upperArm = Vector3.Lerp(a.upperArm, b.upperArm, t),
            elbowBend = Mathf.Lerp(a.elbowBend, b.elbowBend, t),
            wristUpDown = Mathf.Lerp(a.wristUpDown, b.wristUpDown, t),
            wristSide = Mathf.Lerp(a.wristSide, b.wristSide, t),
            wristTwist = Mathf.Lerp(a.wristTwist, b.wristTwist, t),
        };
    }

    [System.Serializable]
    public class Pose
    {
        public Arm right = new Arm();
        public Arm left = new Arm { weight = 0f, upperArm = new Vector3(-0.25f, -0.95f, 0.1f) };
    }

    public Pose idle = new Pose();
    public Pose walk = new Pose();
    public Pose run = new Pose { right = new Arm { upperArm = new Vector3(0.35f, -0.6f, -0.7f), elbowBend = 25f } };
    [Range(0, 45)] public float walkSwing = 18f;     // 걸을 때 팔이 앞뒤로 흔들리는 각도 (양팔 반대로)
    [Range(0, 1)] public float walkSwingPhase = 0f;  // 흔들림 박자 맞추기 (발과 어긋나면 0.5)

    private Animator anim;
    private float active, walkT;
    private Bones R, L;

    private struct Bones { public Transform up, lo, hand, mid, idx, lit; }

    private void Awake()
    {
        anim = GetComponent<Animator>();
        R = Get(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightIndexProximal, HumanBodyBones.RightLittleProximal);
        L = Get(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftLittleProximal);
    }

    private Bones Get(params HumanBodyBones[] b) => new Bones
    {
        up = anim.GetBoneTransform(b[0]), lo = anim.GetBoneTransform(b[1]), hand = anim.GetBoneTransform(b[2]),
        mid = anim.GetBoneTransform(b[3]), idx = anim.GetBoneTransform(b[4]), lit = anim.GetBoneTransform(b[5]),
    };

    public void LateUpdate()
    {
        var st = anim.GetCurrentAnimatorStateInfo(0);
        bool walking = st.IsName("Walk");
        active = Mathf.MoveTowards(active, walking || st.IsName("Idle") ? 1 : 0, Time.deltaTime * 5f);
        walkT = Mathf.MoveTowards(walkT, walking ? 1 : 0, Time.deltaTime * 5f);
        if (active <= 0) return;

        // 걷기(Speed 0.5)·달리기(Speed 1) 사이를 섞고, 서 있기와는 walkT 로 부드럽게 넘어감
        float runT = Mathf.InverseLerp(0.5f, 1f, anim.GetFloat("Speed"));
        float swing = walkT * (1 - runT) * walkSwing * Mathf.Sin((st.normalizedTime + walkSwingPhase) * Mathf.PI * 2);
        Apply(R, Arm.Lerp(idle.right, Arm.Lerp(walk.right, run.right, runT), walkT), swing);
        Apply(L, Arm.Lerp(idle.left, Arm.Lerp(walk.left, run.left, runT), walkT), -swing);
    }

    private void Apply(Bones b, Arm a, float swing)
    {
        float w = a.weight * active;
        if (w <= 0 || b.up == null || b.lo == null || b.hand == null || b.mid == null) return;
        Transform c = transform;
        Quaternion u0 = b.up.rotation, l0 = b.lo.rotation, h0 = b.hand.rotation;

        Vector3 ua = Quaternion.AngleAxis(swing, c.right) * c.TransformDirection(a.upperArm).normalized;
        Vector3 fa = Vector3.RotateTowards(ua, c.forward, a.elbowBend * Mathf.Deg2Rad, 0f);
        b.up.rotation = Quaternion.FromToRotation(b.lo.position - b.up.position, ua) * b.up.rotation;
        b.lo.rotation = Quaternion.FromToRotation(b.hand.position - b.lo.position, fa) * b.lo.rotation;
        b.hand.rotation = Quaternion.FromToRotation(b.mid.position - b.hand.position, fa) * b.hand.rotation; // 손목 일자로

        // 손목 축: 비틀기 = 아래팔 방향, 위아래 = 손등 관절 줄, 좌우 = 손바닥 면에 수직
        Vector3 knuckle = b.idx != null && b.lit != null ? Vector3.ProjectOnPlane(b.idx.position - b.lit.position, fa).normalized : c.forward;
        Vector3 palm = Vector3.Cross(fa, knuckle);
        b.hand.rotation = Quaternion.AngleAxis(a.wristTwist, fa) * Quaternion.AngleAxis(a.wristUpDown, knuckle)
                        * Quaternion.AngleAxis(a.wristSide, palm) * b.hand.rotation;

        if (w < 1)
        {
            b.up.rotation = Quaternion.Slerp(u0, b.up.rotation, w);
            b.lo.rotation = Quaternion.Slerp(l0, b.lo.rotation, w);
            b.hand.rotation = Quaternion.Slerp(h0, b.hand.rotation, w);
        }
    }
}
