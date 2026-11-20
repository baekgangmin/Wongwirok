using UnityEngine;

// 날아가는 2D 그림 판: 늘 카메라를 보면서, 그림 속 머리 방향이 화면에서 움직이는 쪽을 향하게 돌린다.
// (예: 불덩이 그림은 머리가 왼쪽 아래, 꼬리가 오른쪽 위)
public class MotionBillboard : MonoBehaviour
{
    [Tooltip("그림 속 머리가 향하는 방향 (그림 기준, x 오른쪽 · y 위쪽)")]
    [SerializeField] private Vector2 imageHeadDirection = new Vector2(-1f, -1f);
    [Tooltip("손그림 떨림 (도)")]
    [SerializeField] private float boilAngle = 4f;

    private Vector3 lastPos;
    private float roll, boil, nextBoil;

    private void OnEnable() => lastPos = transform.position;

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;
        Vector3 move = transform.position - lastPos;
        lastPos = transform.position;
        // 화면에서의 움직임 방향 → 그림을 그 방향으로 굴림
        Vector2 screen = new Vector2(Vector3.Dot(move, cam.transform.right), Vector3.Dot(move, cam.transform.up));
        if (screen.sqrMagnitude > 1e-8f)
            roll = Vector2.SignedAngle(imageHeadDirection, screen);
        if (Time.time >= nextBoil)
        {
            nextBoil = Time.time + 0.06f;
            boil = Random.Range(-boilAngle, boilAngle);
        }
        // 판이 카메라 반대쪽을 보게 두면 그림이 좌우 뒤집혀 보이지 않음
        transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, cam.transform.up) * Quaternion.Euler(0f, 0f, -(roll + boil));
    }
}
