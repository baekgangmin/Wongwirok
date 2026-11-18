using UnityEngine;

// 손이 아래팔 축으로 비틀린 만큼 그 일부를 아래팔 비틀림 뼈(ForearmTwist02)에 나눠 준다.
// 메쉬의 아래팔 살은 팔꿈치(Twist01) → 손목(Twist02)으로 무게가 점점 바뀌게 칠해 두어, 비틀림이 팔 전체에 고르게 퍼진다.
// 소매는 비틀림 뼈가 아닌 아래팔 뼈에 붙어 있어 같이 돌지 않는다.
[DefaultExecutionOrder(100)]   // ArmPose(팔 자세) 다음, SleeveSway 전에
[RequireComponent(typeof(Animator))]
public class ForearmTwist : MonoBehaviour
{
    [Range(0, 1)] public float share = 0.66f;   // 손 비틀림 중 손목 쪽 아래팔이 같이 도는 비율

    private struct Side { public Transform lo, hand, tw2; public Quaternion tw2Rest, handRest; public Vector3 axis; }
    private Side[] sides;

    private void Awake()
    {
        var anim = GetComponent<Animator>();
        var smr = GetComponentInChildren<SkinnedMeshRenderer>();
        sides = new[] { Make(anim, smr, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand), Make(anim, smr, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand) };
    }

    private static Side Make(Animator anim, SkinnedMeshRenderer smr, HumanBodyBones loB, HumanBodyBones handB)
    {
        var s = new Side { lo = anim.GetBoneTransform(loB), hand = anim.GetBoneTransform(handB) };
        if (s.lo == null || s.hand == null) return s;
        foreach (var t in s.lo.GetComponentsInChildren<Transform>())
            if (t.name.EndsWith("ForearmTwist02")) s.tw2 = t;
        if (s.tw2 != null) s.tw2Rest = s.tw2.localRotation;
        s.handRest = RelaxedFingers.RestLocal(smr, s.hand);   // 모델 원래(바인드) 손 회전
        s.axis = s.hand.localPosition.normalized;              // 아래팔 기준 팔꿈치→손목 방향
        return s;
    }

    private void LateUpdate()
    {
        foreach (var s in sides)
        {
            if (s.tw2 == null) continue;
            s.tw2.localRotation = s.tw2Rest;
            // 원래 손 → 지금 손 회전(아래팔 기준)에서 팔 축 방향 비틀림만 떼어 냄
            Quaternion q = s.hand.localRotation * Quaternion.Inverse(s.handRest);
            Vector3 p = Vector3.Project(new Vector3(q.x, q.y, q.z), s.axis);
            var twist = new Quaternion(p.x, p.y, p.z, q.w);
            float mag = Mathf.Sqrt(twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w);
            if (mag < 1e-6f) continue;
            twist = new Quaternion(twist.x / mag, twist.y / mag, twist.z / mag, twist.w / mag);
            Quaternion part = Quaternion.Slerp(Quaternion.identity, twist, share);
            s.tw2.rotation = s.lo.rotation * part * Quaternion.Inverse(s.lo.rotation) * s.tw2.rotation;
        }
    }
}
