using UnityEngine;

// 소매 흔들림: 이 뼈(아래팔 자식, 팔꿈치에 위치)가 팔 움직임을 한 박자 늦게 따라가며 출렁인다.
// 소매 살은 소매 끝으로 갈수록 이 뼈를 더 따르게 가중치를 나눠 둠 (가만히 있으면 아래팔과 똑같이 움직여 모양 변화 없음)
[DefaultExecutionOrder(500)]   // ArmPose(팔 자세) 다음에 계산
public class SleeveSway : MonoBehaviour
{
    [Range(1, 200)] public float stiffness = 60f;   // 제자리로 돌아오려는 힘 (작을수록 크게 늦게 흔들림)
    [Range(0, 30)] public float damping = 8f;       // 흔들림이 멎는 빠르기
    [Range(0, 3)] public float gravity = 0.6f;      // 움직일 때 아래로 처지는 정도
    [Range(0, 90)] public float maxAngle = 35f;     // 최대로 벌어지는 각도
    public Vector3 tip = new Vector3(0, -0.2f, 0);  // 흔들리는 끝점 (이 뼈 기준, 설정 때 소매 끝 쪽으로 잡아 둠)
    [Range(0, 1)] public float hangDown = 1f;       // 팔을 비틀어도(팔꿈치 돌리기·손목 비틀기) 소매는 아래로 늘어진 기본 방향을 유지하는 정도

    private Quaternion rest;
    private Vector3 axisLocal;                      // 팔꿈치 → 손목 방향 (이 뼈 기준)
    private Vector3 pos, vel;
    private bool started;

    private void Awake()
    {
        rest = transform.localRotation;
        var anim = GetComponentInParent<Animator>();
        var hand = anim ? anim.GetBoneTransform(name.EndsWith("_L") ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand) : null;
        axisLocal = hand ? transform.InverseTransformDirection(hand.position - transform.position).normalized : Vector3.zero;
    }
    private void OnEnable() { started = false; }

    private void LateUpdate()
    {
        transform.localRotation = rest;
        // 팔이 자기 축으로 비틀린 만큼 되돌려, 소매가 늘 아래로 처진 기본 상태에서 흔들리게
        if (hangDown > 0 && axisLocal != Vector3.zero)
        {
            Vector3 ax = transform.TransformDirection(axisLocal).normalized;
            Vector3 p = Vector3.ProjectOnPlane(transform.TransformVector(tip), ax), d = Vector3.ProjectOnPlane(Vector3.down, ax);
            if (p.sqrMagnitude > 1e-8f && d.sqrMagnitude > 1e-8f)
                transform.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(p, d, ax) * hangDown, ax) * transform.rotation;
        }
        Vector3 target = transform.TransformPoint(tip);
        float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
        if (!started || (pos - target).sqrMagnitude > 1f) { pos = target; vel = Vector3.zero; started = true; }   // 처음·순간이동 때는 바로 붙임
        if (dt <= 0) return;

        vel += (target - pos) * stiffness * dt + Vector3.down * gravity * dt;
        vel *= Mathf.Clamp01(1f - damping * dt);
        pos += vel * dt;

        Vector3 from = target - transform.position, to = pos - transform.position;
        float ang = Vector3.Angle(from, to);
        if (ang > maxAngle) { to = Vector3.Slerp(from, to, maxAngle / ang); pos = transform.position + to.normalized * from.magnitude; }
        transform.rotation = Quaternion.FromToRotation(from, to) * transform.rotation;
    }
}
