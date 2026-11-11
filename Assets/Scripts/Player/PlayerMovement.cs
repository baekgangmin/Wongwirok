using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private float gravity = -20f;

    [Header("Dodge")]
    [SerializeField] private float dodgeDistance = 4f;
    [SerializeField] private float dodgeDuration = 0.25f;
    [SerializeField] private float dodgeCooldown = 0.6f;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Animator animator;

    private CharacterController controller;
    private PlayerAttack playerAttack;
    private PlayerParry playerParry;
    private float verticalVelocity;
    private float dodgeCooldownTimer;
    private float slowMultiplier = 1f;
    private float slowTimer;

    private Coroutine restrainRoutine;

    public bool IsDodging { get; private set; }
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

        if (dodgeCooldownTimer <= 0f && Keyboard.current != null && Keyboard.current.leftShiftKey.wasPressedThisFrame)
        {
            Vector3 dodgeDirection = CameraRelativeMove(input);
            if (dodgeDirection.sqrMagnitude < 0.0001f)
                dodgeDirection = -transform.forward;

            dodgeCooldownTimer = dodgeCooldown;
            StartCoroutine(DodgeRoutine(dodgeDirection));
            return;
        }

        Vector3 move = CameraRelativeMove(input);

        // 공격 중에는 이동 입력을 막는다 (전진은 공격 동작의 루트 모션이 담당).
        if (playerAttack != null && playerAttack.IsAttacking)
            move = Vector3.zero;
        // 패링 자세를 취하는 동안에도 멈춘다.
        if (playerParry != null && playerParry.IsParrying)
            move = Vector3.zero;

        if (animator != null)
            animator.SetFloat("Speed", move.magnitude);

        if (move.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(move, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        controller.Move((move * moveSpeed * slowMultiplier + Vector3.up * verticalVelocity) * Time.deltaTime);
    }

    private IEnumerator DodgeRoutine(Vector3 direction)
    {
        IsDodging = true;
        IsInvulnerable = true;
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

        float dodgeSpeed = dodgeDistance / dodgeDuration;
        float elapsed = 0f;

        while (elapsed < dodgeDuration)
        {
            elapsed += Time.deltaTime;
            controller.Move(direction * dodgeSpeed * Time.deltaTime);
            yield return null;
        }

        IsInvulnerable = false;
        IsDodging = false;
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
