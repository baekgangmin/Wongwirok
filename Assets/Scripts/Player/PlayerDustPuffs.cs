using UnityEngine;

// 걷거나 달릴 때 발걸음마다 발 뒤에 붓 그림 흙먼지 덩이를 하나씩 띄운다.
// 발소리와 같은 애니메이션 이벤트(OnFootstep)를 받으므로 Animator가 붙은 오브젝트에 붙인다.
public class PlayerDustPuffs : MonoBehaviour
{
    [SerializeField] private Texture2D[] humanPuffs;
    [SerializeField] private Texture2D[] ghostPuffs;
    [SerializeField] private float size = 0.55f;
    [SerializeField] private float life = 0.5f;
    [Tooltip("Animator Speed 값이 이보다 클 때만 (걷기 0.5, 달리기 1)")]
    [SerializeField] private float minSpeed = 0.3f;

    private Animator animator;
    private WorldWeapon world;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        world = GetComponentInParent<WorldWeapon>();
        if (world == null) world = FindAnyObjectByType<WorldWeapon>();
    }

    private void OnFootstep(AnimationEvent e)
    {
        float speed = animator != null ? animator.GetFloat("Speed") : 0f;
        if (e.animatorClipInfo.weight < 0.5f || speed < minSpeed)
            return;
        var set = world != null && world.World == WorldKind.Ghost ? ghostPuffs : humanPuffs;
        if (set == null || set.Length == 0)
            return;
        float s = size * Random.Range(0.8f, 1.2f) * Mathf.Clamp(speed, 0.6f, 1f);   // 걸을 때는 작은 덩이
        Vector3 p = transform.position - transform.forward * 0.3f + transform.right * Random.Range(-0.15f, 0.15f) + Vector3.up * s * 0.4f;
        InkFxSprite.Spawn(set[Random.Range(0, set.Length)], p, s, Random.Range(-20f, 20f), Random.value < 0.5f, life, 0.6f, Color.white);
    }
}
