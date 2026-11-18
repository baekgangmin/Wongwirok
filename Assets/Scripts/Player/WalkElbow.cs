using UnityEngine;

// 걷기 애니메이션에서 왼팔꿈치가 너무 굽어 보여서, 걷기(Walk 상태: 걷기·달리기 블렌드) 중에만 팔꿈치 굽힘을 일정 비율만큼 펴 준다
[RequireComponent(typeof(Animator))]
public class WalkElbow : MonoBehaviour
{
    [Range(0, 1)] public float leftStraighten = 0.5f;   // 0 = 애니메이션 그대로, 1 = 팔꿈치를 일자로
    public string walkState = "Walk";

    private Animator anim;
    private float weight;   // 걷기에 들어가고 나올 때 부드럽게

    private void Awake() { anim = GetComponent<Animator>(); }

    private void LateUpdate()
    {
        bool walking = anim.GetCurrentAnimatorStateInfo(0).IsName(walkState);
        weight = Mathf.MoveTowards(weight, walking ? 1 : 0, Time.deltaTime * 4f);
        if (weight <= 0) return;

        Transform up = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        Transform lo = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        Transform hand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        if (up == null || lo == null || hand == null) return;

        Vector3 u = lo.position - up.position, f = hand.position - lo.position;
        Vector3 axis = Vector3.Cross(f, u);   // 아래팔을 위팔 방향으로 돌리는 축
        if (axis.sqrMagnitude < 1e-8f) return;
        lo.rotation = Quaternion.AngleAxis(Vector3.Angle(u, f) * leftStraighten * weight, axis.normalized) * lo.rotation;
    }
}
