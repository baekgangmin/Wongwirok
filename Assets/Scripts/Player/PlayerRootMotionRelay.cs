using UnityEngine;

// 겉모습(Animator) 오브젝트에 붙인다.
// 공격 동작이 앞으로 내딛는 만큼(루트 모션)을 부모 Player의 CharacterController로 전달해 실제로 전진시킨다.
// 공격 중이 아닐 때는 루트 모션을 버려서 겉모습이 부모에게서 떨어져 나가지 않게 한다.
[RequireComponent(typeof(Animator))]
public class PlayerRootMotionRelay : MonoBehaviour
{
    [SerializeField] private CharacterController controller;
    [SerializeField] private PlayerAttack playerAttack;
    [SerializeField, Range(0f, 2f)] private float attackMoveScale = 1f;

    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (controller == null)
            controller = GetComponentInParent<CharacterController>();
        if (playerAttack == null)
            playerAttack = GetComponentInParent<PlayerAttack>();
    }

    private void OnAnimatorMove()
    {
        if (controller == null || playerAttack == null || !playerAttack.IsAttacking)
            return;

        Vector3 delta = animator.deltaPosition * attackMoveScale;
        delta.y = 0f;
        if (delta.sqrMagnitude > 0f)
            controller.Move(delta);
    }
}
