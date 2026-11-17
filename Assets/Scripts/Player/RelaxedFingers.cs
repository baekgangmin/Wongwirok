using UnityEngine;

// 애니메이션의 손가락 값이 범위를 넘어 갈퀴 손이 되고, 이 모델은 손가락이 붙어 있어 휴머노이드 굽힘 축도 틀어진다.
// 그래서 애니메이션이 끝난 뒤 손가락을 모델 원래(바인드) 자세로 되돌리고, 손등 관절 줄(검지→새끼)을 축으로 직접 굽힌다.
// 마디별 각도(angles)는 T포즈 AccuRig 버전에서 주먹 쥔 손을 재서 넣은 값. 모델마다 손가락 뼈 방향이 달라 회전값은 못 옮기고 각도만 옮김.
[RequireComponent(typeof(Animator))]
public class RelaxedFingers : MonoBehaviour
{
    [Range(0, 1.5f)] public float amount = 1f;     // 0 = 모델 원래 손, 1 = 잰 주먹
    public float[] angles = new float[30];          // Order 순서의 마디별 굽힘 각도(도)

    private Animator anim;
    private Transform[] bones;
    private Quaternion[] rest;

    // 순서: 왼손 검지·중지·약지·새끼·엄지, 오른손 같은 순서 (각 3마디)
    public static readonly HumanBodyBones[] Order =
    {
        HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal,
        HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal,
        HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal,
        HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal,
        HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal,
        HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal,
        HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal,
        HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal,
        HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal,
        HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal,
    };

    private void Awake()
    {
        anim = GetComponent<Animator>();
        var smr = GetComponentInChildren<SkinnedMeshRenderer>();
        bones = new Transform[Order.Length];
        rest = new Quaternion[Order.Length];
        for (int i = 0; i < Order.Length; i++)
        {
            bones[i] = anim.GetBoneTransform(Order[i]);
            if (bones[i] != null) rest[i] = RestLocal(smr, bones[i]);
        }
    }

    // 모델 원래(바인드) 자세의 로컬 회전: 장면에 저장된 손가락은 애니메이션 값이 남아 있을 수 있어서 바인드 행렬에서 꺼낸다
    public static Quaternion RestLocal(SkinnedMeshRenderer smr, Transform b)
    {
        int ci = System.Array.IndexOf(smr.bones, b), pi = System.Array.IndexOf(smr.bones, b.parent);
        if (ci < 0 || pi < 0) return b.localRotation;
        Matrix4x4 m = smr.sharedMesh.bindposes[pi] * smr.sharedMesh.bindposes[ci].inverse;
        return Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
    }

    private void LateUpdate()
    {
        for (int i = 0; i < bones.Length; i++) if (bones[i] != null) bones[i].localRotation = rest[i];
        Bend(0);
        Bend(15);
    }

    private void Bend(int o)
    {
        Transform a = bones[o], b = bones[o + 9], thumb = bones[o + 12], mid2 = bones[o + 4];
        if (a == null || b == null || thumb == null) return;
        Vector3 knuckles = (b.position - a.position).normalized;
        for (int f = 0; f < 5; f++)
        {
            int i = o + f * 3;
            Transform p = bones[i], d = bones[i + 2];
            if (p == null || d == null) continue;
            bool isThumb = f == 4;
            // 굽는 방향: 손가락은 끝이 엄지 밑동에, 엄지는 끝이 (이미 접힌) 중지 가운데 마디에 가까워지는 쪽
            Vector3 tip = d.position, piv = p.position, aim = isThumb ? (mid2 != null ? mid2.position : a.position) : thumb.position;
            Vector3 axis = isThumb ? Vector3.Cross(tip - piv, aim - piv).normalized : knuckles;
            if (axis == Vector3.zero) continue;
            float s = (Quaternion.AngleAxis(10, axis) * (tip - piv) + piv - aim).sqrMagnitude
                    < (Quaternion.AngleAxis(-10, axis) * (tip - piv) + piv - aim).sqrMagnitude ? 1 : -1;
            for (int j = 0; j < 3; j++)
                if (bones[i + j] != null && i + j < angles.Length)
                    bones[i + j].rotation = Quaternion.AngleAxis(s * angles[i + j] * amount, axis) * bones[i + j].rotation;
        }
    }
}
