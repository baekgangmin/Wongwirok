using System.Collections;
using UnityEngine;

public class Stage1Director : MonoBehaviour
{
    [SerializeField] private EnemyHealth[] mobHealths;
    [SerializeField] private GameObject boss;
    [SerializeField] private float riseDistance = 2.5f;
    [SerializeField] private float riseDuration = 2f;

    private int aliveCount;
    private Vector3 bossTargetPosition;
    private bool bossTriggered;
    private PondGhostBoss bossScript;
    private Collider bossCollider;
    private Transform bossHealthBar;

    private void Awake()
    {
        aliveCount = mobHealths != null ? mobHealths.Length : 0;

        if (boss != null)
        {
            bossTargetPosition = boss.transform.position;
            boss.transform.position = bossTargetPosition + Vector3.down * riseDistance;

            bossScript = boss.GetComponent<PondGhostBoss>();
            if (bossScript != null)
                bossScript.enabled = false;

            bossCollider = boss.GetComponent<Collider>();
            if (bossCollider != null)
                bossCollider.enabled = false;

            bossHealthBar = boss.transform.Find("HealthBarCanvas");
            if (bossHealthBar != null)
                bossHealthBar.gameObject.SetActive(false);
        }

        if (mobHealths != null)
        {
            foreach (EnemyHealth health in mobHealths)
            {
                if (health != null)
                    health.OnDeath += HandleMobDeath;
            }
        }

        if (aliveCount <= 0)
            StartCoroutine(RiseBoss());
    }

    private void HandleMobDeath()
    {
        aliveCount--;
        if (aliveCount <= 0 && !bossTriggered)
            StartCoroutine(RiseBoss());
    }

    private IEnumerator RiseBoss()
    {
        if (boss == null || bossTriggered)
            yield break;

        bossTriggered = true;
        Debug.Log("Wongwirok: 잡몹을 모두 처치함 - 연못 귀신이 호수 중앙에서 떠오릅니다");

        if (bossHealthBar != null)
            bossHealthBar.gameObject.SetActive(true);
        if (bossCollider != null)
            bossCollider.enabled = true;

        Vector3 start = boss.transform.position;
        float elapsed = 0f;

        while (elapsed < riseDuration)
        {
            elapsed += Time.deltaTime;
            boss.transform.position = Vector3.Lerp(start, bossTargetPosition, elapsed / riseDuration);
            yield return null;
        }

        boss.transform.position = bossTargetPosition;

        if (bossScript != null)
            bossScript.enabled = true;
    }
}
