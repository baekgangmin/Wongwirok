using UnityEngine;

// 귀의 세계 검 둘레의 은은한 기운. 덧그린 발광 막의 세기를 천천히 오르내리게 한다.
public class WeaponAuraPulse : MonoBehaviour
{
    [SerializeField] private float minIntensity = 0.35f;
    [SerializeField] private float maxIntensity = 0.9f;
    [SerializeField] private float speed = 1.6f;

    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private Renderer[] renderers;
    private MaterialPropertyBlock block;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        block = new MaterialPropertyBlock();
    }

    private void Update()
    {
        float k = 0.5f + 0.5f * Mathf.Sin(Time.time * speed * Mathf.PI * 2f * 0.5f);
        float v = Mathf.Lerp(minIntensity, maxIntensity, k);
        foreach (var r in renderers)
        {
            r.GetPropertyBlock(block);
            block.SetFloat(IntensityId, v);
            r.SetPropertyBlock(block);
        }
    }
}
