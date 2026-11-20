using UnityEngine;

// 가로로 붙인 그림 묶음(플립북)을 정해 둔 순서로 뚝뚝 넘기며, 늘 카메라를 보는 2D 판.
// 부드럽게 섞지 않고 칸을 통째로 바꿔야 손그림 애니메이션처럼 보인다.
[RequireComponent(typeof(Renderer))]
public class FlipbookBillboard : MonoBehaviour
{
    [Tooltip("그림 묶음의 칸 수 (가로)")]
    [SerializeField] private int columns = 3;
    [Tooltip("보여 줄 칸 순서 (0부터). 1·2·3·2 = 0,1,2,1")]
    [SerializeField] private int[] sequence = { 0, 1, 2, 1 };
    [Tooltip("초당 바뀌는 칸 수")]
    [SerializeField] private float framesPerSecond = 10f;

    private Renderer rend;
    private MaterialPropertyBlock block;
    private float startOffset;
    private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

    private void Awake()
    {
        rend = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
        startOffset = Random.value * 10f;   // 여러 마리가 같은 박자로 깜빡이지 않게
    }

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, cam.transform.up);

        if (sequence == null || sequence.Length == 0 || columns <= 0)
            return;
        int step = Mathf.FloorToInt((Time.time + startOffset) * framesPerSecond) % sequence.Length;
        int frame = Mathf.Clamp(sequence[step], 0, columns - 1);
        rend.GetPropertyBlock(block);   // 맞았을 때 색 바꾸기(RendererTint)와 같이 쓰므로 덮어쓰지 않게
        block.SetVector(BaseMapStId, new Vector4(1f / columns, 1f, (float)frame / columns, 0f));
        rend.SetPropertyBlock(block);
    }
}
