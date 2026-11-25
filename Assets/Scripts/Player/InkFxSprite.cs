using UnityEngine;

// 칼 이펙트 그림 한 장을 카메라 쪽으로 띄웠다가 커지며 사라지게 한다 (베기 궤적, 맞을 때 터짐).
// 그림은 칼마다 SwordData 에 넣어 둔다.
public class InkFxSprite : MonoBehaviour
{
    private Material mat;
    private float life, age, startScale, endScale;
    private Vector3 baseScale;
    private Color tint;

    public static void Spawn(Texture2D tex, Vector3 pos, float size, float rollDeg, bool mirror, float life, float grow, Color tint)
    {
        if (tex == null) return;
        var cam = Camera.main;
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "InkFx_" + tex.name;
        Destroy(go.GetComponent<Collider>());
        go.transform.position = pos;
        if (cam != null)
            go.transform.rotation = Quaternion.LookRotation(go.transform.position - cam.transform.position, cam.transform.up) * Quaternion.Euler(0, 0, rollDeg);
        var fx = go.AddComponent<InkFxSprite>();
        fx.mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };
        fx.tint = tint;
        fx.mat.color = tint;
        go.GetComponent<Renderer>().sharedMaterial = fx.mat;
        go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        fx.baseScale = new Vector3(mirror ? -size : size, size, 1f);
        fx.startScale = 1f - grow * 0.5f; fx.endScale = 1f + grow * 0.5f;
        fx.life = life;
        go.transform.localScale = fx.baseScale * fx.startScale;
    }

    private void Update()
    {
        age += Time.unscaledDeltaTime;   // 타격 멈춤 중에도 그림은 움직이게
        float k = Mathf.Clamp01(age / life);
        transform.localScale = baseScale * Mathf.Lerp(startScale, endScale, 1f - (1f - k) * (1f - k));
        var c = tint; c.a = tint.a * (k < 0.35f ? 1f : 1f - (k - 0.35f) / 0.65f);
        mat.color = c;
        if (k >= 1f) Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }
}
