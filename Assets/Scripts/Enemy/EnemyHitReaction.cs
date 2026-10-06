using System.Collections;
using UnityEngine;

[RequireComponent(typeof(EnemyHealth))]
public class EnemyHitReaction : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private float hitFlashDuration = 0.1f;
    [SerializeField] private float deathSinkDuration = 1f;

    private EnemyHealth health;
    private Color originalColor;
    private Coroutine activeRoutine;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();
        if (targetRenderer != null)
            originalColor = targetRenderer.material.color;

        health.OnDamaged += HandleDamaged;
        health.OnDeath += HandleDeath;
    }

    private void OnDestroy()
    {
        health.OnDamaged -= HandleDamaged;
        health.OnDeath -= HandleDeath;
    }

    private void HandleDamaged(float amount)
    {
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        if (targetRenderer == null)
            yield break;

        targetRenderer.material.color = hitColor;
        yield return new WaitForSeconds(hitFlashDuration);
        targetRenderer.material.color = originalColor;
    }

    private void HandleDeath()
    {
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.enabled = false;

        Vector3 startPosition = transform.position;
        Vector3 endPosition = startPosition + Vector3.down * 1f;
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
