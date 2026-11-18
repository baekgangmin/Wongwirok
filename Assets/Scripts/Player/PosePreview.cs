using UnityEngine;

// 자세 맞추기용: 플레이 중 Mode 를 Walk/Run 으로 두면 키 입력 없이 제자리에서 계속 걷기·달리기를 재생한다.
// 그동안 Arm Pose·Relaxed Fingers 값을 바꾸면 바로 보인다. 다 맞추면 Off.
[DefaultExecutionOrder(1000)]   // PlayerMovement 가 Speed 를 쓴 뒤에 덮어쓰도록
[RequireComponent(typeof(Animator))]
public class PosePreview : MonoBehaviour
{
    public enum Mode { Off, Idle, Walk, Run }
    public Mode mode = Mode.Off;

    private Animator anim;
    private Mode last = Mode.Off;
    private Vector3 pos;
    private Quaternion rot;
    private bool rootMotion;

    private void Awake() { anim = GetComponent<Animator>(); }

    private void Update()
    {
        if (mode != last)
        {
            if (last == Mode.Off) { pos = transform.localPosition; rot = transform.localRotation; rootMotion = anim.applyRootMotion; }
            if (mode == Mode.Off) { anim.applyRootMotion = rootMotion; transform.localPosition = pos; transform.localRotation = rot; }
            else anim.Play(mode == Mode.Idle ? "Idle" : "Walk", 0, 0f);
            last = mode;
        }
        if (mode == Mode.Off) return;
        anim.applyRootMotion = false;   // 제자리에서
        anim.SetFloat("Speed", mode == Mode.Run ? 1f : mode == Mode.Walk ? 0.5f : 0f);
    }

    private void LateUpdate()
    {
        if (mode == Mode.Off) return;
        transform.localPosition = pos;
        transform.localRotation = rot;
    }
}
