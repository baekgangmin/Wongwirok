using UnityEngine;

// 씬별 수묵화 화면 효과 설정. 렌더러의 InkWash 패스가 이 전역값을 읽는다.
// 이 컴포넌트가 없는 씬은 _InkAmount = 0 → 효과 꺼짐.
[ExecuteAlways]
public class InkStyle : MonoBehaviour
{
    [Range(0, 1)] public float amount = 1f;
    public Color paper = new Color(0.93f, 0.9f, 0.82f);
    public Color ink = new Color(0.09f, 0.08f, 0.07f);
    [Range(0, 1)] [Tooltip("원래 색을 남기는 정도 (0=먹만, 1=원래 색)")] public float tint = 0.55f;
    public float farStart = 90f, farEnd = 260f;

    private void OnEnable() => Apply();
    private void OnValidate() => Apply();
    private void OnDisable() => Shader.SetGlobalFloat("_InkAmount", 0f);

    private void Apply()
    {
        Shader.SetGlobalFloat("_InkAmount", isActiveAndEnabled ? amount : 0f);
        Shader.SetGlobalColor("_InkPaper", paper);
        Shader.SetGlobalColor("_InkColor", ink);
        Shader.SetGlobalFloat("_InkTint", tint);
        Shader.SetGlobalFloat("_InkFarStart", farStart);
        Shader.SetGlobalFloat("_InkFarEnd", farEnd);
    }
}
