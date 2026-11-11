using UnityEngine;

// 렌더러 색을 잠깐 바꾸는 도우미 (맞았을 때 번쩍임, 패링당함 표시 등).
// 셰이더마다 색 이름이 달라서(URP: _BaseColor, 기본: _Color, 바르코 glTF: baseColorFactor) 있는 걸 찾아 쓴다.
// 재질을 복제하지 않고 MaterialPropertyBlock으로 덮어쓴다.
public class RendererTint
{
    private static readonly int[] CandidateIds =
    {
        Shader.PropertyToID("_BaseColor"),
        Shader.PropertyToID("_Color"),
        Shader.PropertyToID("baseColorFactor"),
    };

    private readonly Renderer renderer;
    private readonly int colorId = -1;
    private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();

    public Color BaseColor { get; } = Color.white;
    public bool IsValid => renderer != null && colorId != -1;

    public RendererTint(Renderer target)
    {
        renderer = target;
        if (renderer == null || renderer.sharedMaterial == null)
            return;
        Material m = renderer.sharedMaterial;
        foreach (int id in CandidateIds)
        {
            if (m.HasProperty(id))
            {
                colorId = id;
                BaseColor = m.GetColor(id);
                break;
            }
        }
    }

    public void Set(Color color)
    {
        if (!IsValid)
            return;
        renderer.GetPropertyBlock(block);
        block.SetColor(colorId, color);
        renderer.SetPropertyBlock(block);
    }

    // 원래 색에 곱해서 칠한다 (텍스처 밝기는 유지)
    public void SetMultiplied(Color multiplier)
    {
        Set(BaseColor * multiplier);
    }

    public void Restore()
    {
        Set(BaseColor);
    }
}
