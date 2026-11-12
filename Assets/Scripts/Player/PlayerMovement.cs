using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("달리기 속도 (Shift를 누르는 동안)")]
    [SerializeField] private float moveSpeed = 5.8f;
    [Tooltip("걷기 속도 (기본 이동). Starter Assets 걷기 동작 보폭에 맞춘 값")]
    [SerializeField] private float walkSpeed = 2.7f;
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private float gravity = -20f;

    [Header("Dodge (구르기)")]
    [Tooltip("서 있거나 걸을 때: 짧게 구르기 거리(m) / 이동 시간 / 일어나는 동안 조작 잠금")]
    [SerializeField] private float dodgeDistance = 3f;
    [SerializeField] private float dodgeDuration = 0.55f;
    [SerializeField] private float dodgeRecovery = 0.3f;
    [Tooltip("달릴 때: 몸을 날려 길게 구르기 거리(m) / 이동 시간 / 일어나는 동안 조작 잠금")]
    [SerializeField] private float runDodgeDistance = 5f;
    [SerializeField] private float runDodgeDuration = 0.6f;
    [SerializeField] private float runDodgeRecovery = 0.17f;
    [SerializeField] private float dodgeCooldown = 0.1f;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Animator animator;

    private CharacterController controller;
    private PlayerAttack playerAttack;
    private PlayerParry playerParry;
    private float verticalVelocity;
    private float dodgeCooldownTimer;
    private float rollRecoverTimer;
    private static readonly int RollShortId = Animator.StringToHash("RollShort");
    private static readonly int RollLongId = Animator.StringToHash("RollLong");
    private float slowMultiplier = 1f;
    private float slowTimer;

    private Coroutine restrainRoutine;

    public bool IsDodging { get; private set; }
    public bool IsRunning { get; private set; }
    // 마지막으로 회피가 끝난 시각 (회피 공격 판단용)
    public float LastDodgeEndTime { get; private set; } = -99f;
    public bool IsInvulnerable { get; private set; }
    public bool IsRestrained { get; private set; }
    // 물기둥에 갇혀 떠올랐다가 떨어지는 동안 (입력 잠김)
    public bool IsTrapped { get; private set; }
    public bool IsTrapFalling { get; private set; }
    public event System.Action OnTrapLanded;

    private float trapHoldRemaining;
    private static readonly int TrappedId = Animator.StringToHash("Trapped");

    // 물기둥에 갇힘: 기둥 가운데로 끌려가며 떠오름 → 위에서 들썩임 → 떨어져 착지.
    public void ApplyWaterTrap(Vector3 pillarCenter, float riseHeight, float riseTime, float holdTime, float bobAmplitude)
    {
        if (IsTrapped)
            return;
        BreakRestrain();
        StartCoroutine(WaterTrapRoutine(pillarCenter, riseHeight, riseTime, holdTime, bobAmplitude));
    }

    // 갇힌 동안 공격 버튼을 누를 때마다 빨리 빠져나온다
    public void StruggleTrap(float seconds)
    {
        if (IsTrapped)
            trapHoldRemaining -= seconds;
    }

    private IEnumerator WaterTrapRoutine(Vector3 center, float riseHeight, float riseTime, float holdTime, float bobAmplitude)
    {
        IsTrapped = true;
        verticalVelocity = 0f;
        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.SetBool(TrappedId, true);
        }

        Vector3 start = transform.position;
        Vector3 top = new Vector3(center.x, start.y + riseHeight, center.z);

        // 떠오르기 (가운데로 끌려감)
        for (float t = 0f; t < riseTime; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / riseTime);
            controller.Move(Vector3.Lerp(start, top, k) - transform.position);
            yield return null;
        }

        // 위에서 살짝 들썩임
        trapHoldRemaining = holdTime;
        float bobTime = 0f;
        while (trapHoldRemaining > 0f)
        {
            trapHoldRemaining -= Time.deltaTime;
            bobTime += Time.deltaTime;
            Vector3 target = top + Vector3.up * (Mathf.Sin(bobTime * Mathf.PI * 2f * 1.2f) * bobAmplitude);
            controller.Move(target - transform.position);
            yield return null;
        }

        // 떨어지기
        IsTrapFalling = true;
        if (animator != null)
            animator.SetBool(TrappedId, false);
        float fallVelocity = 0f;
        for (float t = 0f; t < 3f; t += Time.deltaTime)
        {
            fallVelocity += gravity * 1.5f * Time.deltaTime;
            CollisionFlags flags = controller.Move(Vector3.up * fallVelocity * Time.deltaTime);
            if ((flags & CollisionFlags.Below) != 0)
                break;
            yield return null;
        }

        verticalVelocity = -2f;
        IsTrapFalling = false;
        IsTrapped = false;
        OnTrapLanded?.Invoke();
    }

    public event System.Action OnSlowed;

    public void ApplySlow(float multiplier, float duration)
    {
        slowMultiplier = multiplier;
        slowTimer = duration;
        OnSlowed?.Invoke();
    }

    public void ApplyRestrain(float duration)
    {
        if (restrainRoutine != null)
            StopCoroutine(restrainRoutine);
        restrainRoutine = StartCoroutine(RestrainRoutine(duration));
    }

    public void BreakRestrain()
    {
        if (!IsRestrained)
            return;

        if (restrainRoutine != null)
            StopCoroutine(restrainRoutine);
        IsRestrained = false;
    }

    private IEnumerator RestrainRoutine(float duration)
    {
        IsRestrained = true;
        yield return new WaitForSeconds(duration);
        IsRestrained = false;
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        playerAttack = GetComponent<PlayerAttack>();
        playerParry = GetComponent<PlayerParry>();
    }

    private void Update()
    {
        dodgeCooldownTimer -= Time.deltaTime;

        if (slowTimer > 0f)
        {
            slowTimer -= Time.deltaTime;
            if (slowTimer <= 0f)
                slowMultiplier = 1f;
        }

        if (IsRestrained || IsDodging || IsTrapped)
            return;

        Vector2 input = ReadMoveInput();

        // 회피: C
        if (dodgeCooldownTimer <= 0f && Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
        {
            Vector3 dodgeDirection = CameraRelativeMove(input);
            // 방향키 없이 누르면 지금 바라보는 방향으로 구른다
            if (dodgeDirection.sqrMagnitude < 0.0001f)
            {
                dodgeDirection = transform.forward;
                dodgeDirection.y = 0f;
                dodgeDirection.Normalize();
            }

            // 달리는 중(Shift + 이동)이면 길게, 아니면 짧게 구른다
            bool runRoll = Keyboard.current.leftShiftKey.isPressed && input.sqrMagnitude > 0.01f;
            dodgeCooldownTimer = dodgeCooldown + (runRoll ? runDodgeDuration + runDodgeRecovery : dodgeDuration + dodgeRecovery);
            if (runRoll)
                StartCoroutine(DodgeRoutine(dodgeDirection, runDodgeDistance, runDodgeDuration, runDodgeRecovery, RollLongId));
            else
                StartCoroutine(DodgeRoutine(dodgeDirection, dodgeDistance, dodgeDuration, dodgeRecovery, RollShortId));
            return;
        }

        if (rollRecoverTimer > 0f)
        {
            // 구르고 일어나는 동안은 이동하지 않는다 (공격은 가능 → 회피 공격)
            rollRecoverTimer -= Time.deltaTime;
            if (animator != null)
                animator.SetFloat("Speed", 0f);
            ApplyGravityOnly();
            return;
        }

        Vector3 move = CameraRelativeMove(input);

        // 공격 중에는 이동 입력을 막는다 (전진은 공격 동작의 루트 모션이 담당).
        if (playerAttack != null && playerAttack.IsAttacking)
            move = Vector3.zero;
        // 패링 자세를 취하는 동안에도 멈춘다.
        if (playerParry != null && playerParry.IsParrying)
            move = Vector3.zero;

        // 달리기: 왼쪽 Shift를 누르는 동안. 애니메이터 Speed는 걷기 0.5 / 뛰기 1.0
        bool running = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
        IsRunning = running && move.sqrMagnitude > 0.0001f;
        float speed = running ? moveSpeed : walkSpeed;
        if (animator != null)
            animator.SetFloat("Speed", move.magnitude * (running ? 1f : 0.5f), 0.1f, Time.deltaTime);

        if (move.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(move, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        controller.Move((move * speed * slowMultiplier + Vector3.up * verticalVelocity) * Time.deltaTime);
    }

    private IEnumerator DodgeRoutine(Vector3 direction, float distance, float duration, float recovery, int animTrigger)
    {
        IsDodging = true;
        IsInvulnerable = true;
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        if (animator != null)
            animator.SetTrigger(animTrigger);

        // 처음에 빠르고 끝으로 갈수록 느려지게 (구르며 감속)
        float moved = 0f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / duration);
            float target = distance * (1f - (1f - k) * (1f - k));
            Vector3 step = direction * (target - moved);
            moved = target;
            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;
            verticalVelocity += gravity * Time.deltaTime;
            controller.Move(step + Vector3.up * verticalVelocity * Time.deltaTime);
            yield return null;
        }

        IsInvulnerable = false;
        IsDodging = false;
        LastDodgeEndTime = Time.time;
        rollRecoverTimer = recovery;
    }

    private void ApplyGravityOnly()
    {
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;
        controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
    }

    private Vector2 ReadMoveInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return Vector2.zero;

        float x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
        float y = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
        return new Vector2(x, y);
    }

    private Vector3 CameraRelativeMove(Vector2 input)
    {
        if (input.sqrMagnitude < 0.0001f || cameraTransform == null)
            return Vector3.zero;

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        return (forward * input.y + right * input.x).normalized;
    }
}
