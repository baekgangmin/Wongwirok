using System.Collections;
using UnityEngine;

public class WeaponSwing : MonoBehaviour
{
    [Header("Swing (공격)")]
    [SerializeField] private float swingAngle = 90f;
    [SerializeField] private float swingDuration = 0.25f;

    [Header("Block (패링)")]
    [SerializeField] private float blockPitch = -70f;
    [SerializeField] private float blockTransitionTime = 0.08f;

    private Quaternion restRotation;
    private Coroutine activeRoutine;

    private void Awake()
    {
        restRotation = transform.localRotation;
    }

    public void PlaySwing()
    {
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(SwingRoutine());
    }

    public void PlayBlock(float holdDuration)
    {
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(BlockRoutine(holdDuration));
    }

    private IEnumerator SwingRoutine()
    {
        Quaternion startRotation = restRotation * Quaternion.Euler(0f, -swingAngle * 0.5f, 0f);
        Quaternion endRotation = restRotation * Quaternion.Euler(0f, swingAngle * 0.5f, 0f);

        float elapsed = 0f;
        while (elapsed < swingDuration)
        {
            elapsed += Time.deltaTime;
            transform.localRotation = Quaternion.Slerp(startRotation, endRotation, elapsed / swingDuration);
            yield return null;
        }

        transform.localRotation = restRotation;
    }

    private IEnumerator BlockRoutine(float holdDuration)
    {
        Quaternion blockRotation = restRotation * Quaternion.Euler(blockPitch, 0f, 0f);

        yield return RotateOverTime(blockRotation, blockTransitionTime);
        yield return new WaitForSeconds(holdDuration);
        yield return RotateOverTime(restRotation, blockTransitionTime);
    }

    private IEnumerator RotateOverTime(Quaternion target, float duration)
    {
        Quaternion start = transform.localRotation;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localRotation = Quaternion.Slerp(start, target, elapsed / duration);
            yield return null;
        }

        transform.localRotation = target;
    }
}
