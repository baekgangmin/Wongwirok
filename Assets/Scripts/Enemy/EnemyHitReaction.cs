using System.Collections;
using UnityEngine;

[RequireComponent(typeof(EnemyHealth))]
public class EnemyHitReaction : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private float hitFlashDuration = 0.1f;
    [SerializeField] private float deathSinkDuration = 1f;
    [Tooltip("죽은 뒤 땅속으로 가라앉는 거리 (몸집이 큰 적은 크게)")]
    [SerializeField] private float deathSinkDistance = 1f;
    [Tooltip("죽는 동작이 있으면, 이 시간만큼 동작을 보여준 뒤 땅속으로 가라앉는다")]
    [SerializeField] private float deathAnimDelay = 0f;

    [Tooltip("맞았을 때 몸 위에 덧그리는 발광 막 (HitFlashOverlay). 비우면 색만 바꿈")]
    [SerializeField] private Material flashOverlayMaterial;
    [SerializeField] private float flashIntensity = 1.6f;

    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private Material overlayInstance;
    private Material[] originalMaterials;
    private Material[] flashMaterials;
    private EnemyHealth health;
    private RendererTint tint;
    private Color restColor;
    private Coroutine activeRoutine;

    // 번쩍임이 끝나고 돌아갈 색 (보스 2단계처럼 평소 색이 바뀔 때)
    public void SetRestColor(Color color)
    {
        restColor = color;
    }

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();
        tint = new RendererTint(targetRenderer);
        restColor = tint.BaseColor;
        SetupOverlay();

        health.OnDamaged += HandleDamaged;
        health.OnDeath += HandleDeath;
    }

    private void OnDestroy()
    {
        health.OnDamaged -= HandleDamaged;
        health.OnDeath -= HandleDeath;
        if (overlayInstance != null)
            Destroy(overlayInstance);
    }

    private void HandleDamaged(float amount)
    {
        if (health.IsDead)
            return;
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        // 발광 막이 있으면 몸 위에 덧그려서 확 빛났다가 사라지게 (조명이 어두워도 잘 보임)
        if (overlayInstance != null)
        {
            targetRenderer.sharedMaterials = flashMaterials;
            for (float t = 0f; t < hitFlashDuration; t += Time.deltaTime)
            {
                overlayInstance.SetFloat(IntensityId, flashIntensity * (1f - t / hitFlashDuration));
                yield return null;
            }
            targetRenderer.sharedMaterials = originalMaterials;
        }
        else
        {
            tint.Set(hitColor);
            yield return new WaitForSeconds(hitFlashDuration);
            tint.Set(restColor);
        }
        activeRoutine = null;
    }

    private void SetupOverlay()
    {
        if (flashOverlayMaterial == null || targetRenderer == null)
            return;
        originalMaterials = targetRenderer.sharedMaterials;
        overlayInstance = new Material(flashOverlayMaterial);
        Color c = hitColor;
        float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        if (max > 1f)
            c /= max;   // 밝기는 flashIntensity로 조절
        overlayInstance.SetColor("_Color", c);
        flashMaterials = new Material[originalMaterials.Length + 1];
        originalMaterials.CopyTo(flashMaterials, 0);
        flashMaterials[flashMaterials.Length - 1] = overlayInstance;
    }

    private void HandleDeath()
    {
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);
        if (originalMaterials != null && targetRenderer != null)
            targetRenderer.sharedMaterials = originalMaterials;
        tint.Set(restColor);
        activeRoutine = StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.enabled = false;

        if (deathAnimDelay > 0f)
            yield return new WaitForSeconds(deathAnimDelay);

        Vector3 startPosition = transform.position;
        Vector3 endPosition = startPosition + Vector3.down * deathSinkDistance;
        float elapsed = 0f;

        while (elapsed < deathSinkDuration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(startPosition, endPosition, elapsed / deathSinkDuration);
            yield return null;
        }

        Destroy(gameObject);
    }
}
