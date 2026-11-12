using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Orbit")]
    [SerializeField] private float distance = 5.5f;
    [SerializeField] private float mouseSensitivity = 0.15f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 60f;

    private float yaw;
    private float pitch = 15f;

    // 화면 흔들림 (피격 등)
    public static ThirdPersonCamera Instance { get; private set; }
    private float shakeAmplitude;
    private float shakeDuration;
    private float shakeTimer;

    public void Shake(float amplitude, float duration)
    {
        if (amplitude >= shakeAmplitude * (shakeDuration > 0f ? shakeTimer / shakeDuration : 0f))
        {
            shakeAmplitude = amplitude;
            shakeDuration = duration;
            shakeTimer = duration;
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    // 세계 이동 등으로 플레이어를 옮긴 뒤 카메라를 등 뒤로 맞춘다
    public void SetYaw(float newYaw)
    {
        yaw = newYaw;
        pitch = 15f;
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (target != null)
            yaw = target.eulerAngles.y;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            bool isLocked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = isLocked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = isLocked;
        }
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector2 lookDelta = ReadLookInput();
        yaw += lookDelta.x * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - lookDelta.y * mouseSensitivity, minPitch, maxPitch);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 position = target.position - rotation * Vector3.forward * distance;

        if (shakeTimer > 0f)
        {
            // 타격 멈춤 중에도 흔들리도록 실제 시간 기준
            shakeTimer -= Time.unscaledDeltaTime;
            float strength = shakeAmplitude * Mathf.Clamp01(shakeTimer / shakeDuration);
            float t = Time.unscaledTime * 35f;
            Vector3 offset = new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, 0f) * 2f * strength;
            position += rotation * offset;
        }

        transform.SetPositionAndRotation(position, rotation);
    }

    private Vector2 ReadLookInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || Cursor.lockState != CursorLockMode.Locked)
            return Vector2.zero;

        return mouse.delta.ReadValue();
    }
}
