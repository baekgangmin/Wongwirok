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

    private CharacterController controller;
    private float verticalVelocity;
    private float dodgeCooldownTimer;

    public bool IsDodging { get; private set; }
    public bool IsInvulnerable { get; private set; }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void Update()
    {
        dodgeCooldownTimer -= Time.deltaTime;

        if (IsDodging)
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

        if (move.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(move, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        controller.Move((move * moveSpeed + Vector3.up * verticalVelocity) * Time.deltaTime);
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
