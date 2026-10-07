using System.Collections;
using UnityEngine;

public class BossArenaPlatforms : MonoBehaviour
{
    [SerializeField] private GameObject[] platforms;
    [SerializeField] private int platformsToSinkOnPhase2 = 3;
    [SerializeField] private float sinkDistance = 3f;
    [SerializeField] private float sinkDuration = 1.5f;

    private bool sunk;

    public void SinkPlatforms()
    {
        if (sunk || platforms == null)
            return;

        sunk = true;
        int count = Mathf.Min(platformsToSinkOnPhase2, platforms.Length);

        for (int i = 0; i < count; i++)
        {
            if (platforms[i] != null)
                StartCoroutine(SinkRoutine(platforms[i].transform));
        }
    }

    private IEnumerator SinkRoutine(Transform platform)
    {
        Vector3 start = platform.position;
        Vector3 end = start + Vector3.down * sinkDistance;
        float elapsed = 0f;

        while (elapsed < sinkDuration)
        {
            elapsed += Time.deltaTime;
            platform.position = Vector3.Lerp(start, end, elapsed / sinkDuration);
            yield return null;
        }
    }
}
