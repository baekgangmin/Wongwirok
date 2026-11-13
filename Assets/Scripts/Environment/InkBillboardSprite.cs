using UnityEngine;

// 2D 그림판 하나 (InkBillboard 셰이더용). 바닥 가운데가 원점인 사각형 메시를 만들어 붙인다.
[ExecuteAlways, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class InkBillboardSprite : MonoBehaviour
{
    private static Mesh quad;

    private void OnEnable()
    {
        if (quad == null)
        {
            quad = new Mesh { name = "InkBillboardQuad" };
            quad.vertices = new[] { new Vector3(-0.5f, 0, 0), new Vector3(0.5f, 0, 0), new Vector3(-0.5f, 1, 0), new Vector3(0.5f, 1, 0) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            quad.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            quad.RecalculateNormals();
            quad.bounds = new Bounds(new Vector3(0, 0.5f, 0), new Vector3(2, 2, 2)); // 돌고 흔들려도 잘리지 않게 넉넉히
        }
        GetComponent<MeshFilter>().sharedMesh = quad;
    }
}
