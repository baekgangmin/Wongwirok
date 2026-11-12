using UnityEngine;

// 높이 안개(AERO)는 렌더러 전체가 같은 재질을 쓰기 때문에, 씬마다 색과 짙기를 바꿔 끼우고
// 씬을 나갈 때 원래 값으로 되돌린다. (인간 세계 = 맑은 낮 안개, 귀의 세계 = 붉은 안개)
public class SceneHeightFogOverride : MonoBehaviour
{
    [SerializeField] private Material heightFogMaterial;
    [SerializeField] private Color fogColour = new Color(0.78f, 0.84f, 0.9f, 1f);
    [SerializeField] private float density = 0.03f;
    [SerializeField] private float maxDistance = 220f;

    private static readonly int ColourId = Shader.PropertyToID("_Colour");
    private static readonly int DensityId = Shader.PropertyToID("_Density");
    private static readonly int MaxDistanceId = Shader.PropertyToID("_Max_Distance");

    private Color savedColour;
    private float savedDensity;
    private float savedMaxDistance;
    private bool applied;

    private void OnEnable()
    {
        if (heightFogMaterial == null)
            return;
        savedColour = heightFogMaterial.GetColor(ColourId);
        savedDensity = heightFogMaterial.GetFloat(DensityId);
        savedMaxDistance = heightFogMaterial.GetFloat(MaxDistanceId);
        heightFogMaterial.SetColor(ColourId, fogColour);
        heightFogMaterial.SetFloat(DensityId, density);
        heightFogMaterial.SetFloat(MaxDistanceId, maxDistance);
        applied = true;
    }

    private void OnDisable()
    {
        if (!applied || heightFogMaterial == null)
            return;
        heightFogMaterial.SetColor(ColourId, savedColour);
        heightFogMaterial.SetFloat(DensityId, savedDensity);
        heightFogMaterial.SetFloat(MaxDistanceId, savedMaxDistance);
        applied = false;
    }
}
