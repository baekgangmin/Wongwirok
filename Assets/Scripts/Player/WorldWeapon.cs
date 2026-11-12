using UnityEngine;

public enum WorldKind { Human, Ghost }

// 같은 무기라도 인간 세계에서는 평범한 나뭇가지, 귀의 세계에서는 검으로 보인다.
// 씬마다 world 값을 정해두면 시작할 때 맞는 모양만 켠다.
public class WorldWeapon : MonoBehaviour
{
    [SerializeField] private WorldKind world = WorldKind.Human;
    [Tooltip("인간 세계에서 쥐는 나뭇가지")]
    [SerializeField] private GameObject humanWeapon;
    [Tooltip("귀의 세계에서 쥐는 검")]
    [SerializeField] private GameObject ghostWeapon;
    [Tooltip("예전 임시 검 모델 (모양만 숨기고, 칼날 궤적 기준점은 그대로 사용)")]
    [SerializeField] private Renderer legacyWeaponRenderer;

    public WorldKind World => world;

    private void Awake()
    {
        Apply();
    }

    public void SetWorld(WorldKind kind)
    {
        world = kind;
        Apply();
    }

    private void Apply()
    {
        if (humanWeapon != null) humanWeapon.SetActive(world == WorldKind.Human);
        if (ghostWeapon != null) ghostWeapon.SetActive(world == WorldKind.Ghost);
        if (legacyWeaponRenderer != null) legacyWeaponRenderer.enabled = false;
    }
}
