using System.Collections;
using UnityEngine;

// 2스테이지 흐름: 침전에는 13궁녀가 처음부터 누워 있는 대비를 둘러싸고 노려보며 서 있다.
// 복도 잡몹을 모두 베면 세 무리(5/4/4)가 차례로 주인공에게 덤벼든다 (나머지는 대비 곁에서 대기).
// 마지막 무리까지 쓰러뜨리면 2스테이지를 깬 것으로 기록하고, 복도 끝 밧줄을 당겨 마을 우물 앞으로 돌아갈 수 있게 된다.
public class Stage2Director : MonoBehaviour
{
    [SerializeField] private Transform corridorMobs;     // 복도 잡몹들의 부모
    [SerializeField] private Transform[] waves;          // 궁녀 무리 1~3
    [SerializeField] private Transform daebi;            // 대기하는 궁녀들이 바라보는 대비
    [SerializeField] private float waveGap = 1.5f;
    [SerializeField] private GhostRopeReturn ropeReturn;   // 다 쓰러뜨리면 복도 끝 밧줄을 당길 수 있게 됨

    private Transform current;   // 지금 싸우는 무리 (처음엔 복도)
    private int nextWave;
    private bool busy;

    private void Start()
    {
        foreach (var w in waves)
        {
            if (w == null) continue;
            w.gameObject.SetActive(true);
            SetEngaged(w, false);
            if (daebi == null) continue;
            foreach (Transform lady in w)
            {
                Vector3 to = daebi.position - lady.position; to.y = 0f;
                if (to.sqrMagnitude > 0.01f) lady.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
            }
        }
        current = corridorMobs;
    }

    private void Update()
    {
        if (busy || current == null || Alive(current) > 0) return;
        if (nextWave < waves.Length) StartCoroutine(Engage(waves[nextWave++]));
        else StartCoroutine(Clear());
    }

    private static int Alive(Transform group)
    {
        int n = 0;
        foreach (var h in group.GetComponentsInChildren<EnemyHealth>()) if (!h.IsDead) n++;
        return n;
    }

    // 대기 중엔 쫓아오지도 공격하지도 않음
    private static void SetEngaged(Transform wave, bool on)
    {
        foreach (var mb in wave.GetComponentsInChildren<MonoBehaviour>())
            if (mb is EnemyChase || mb is IEnemyAttack) mb.enabled = on;
    }

    // 다음 무리가 대비 곁을 떠나 덤벼든다 (앞 무리를 쓰러뜨린 뒤 잠깐 쉼)
    private IEnumerator Engage(Transform wave)
    {
        busy = true;
        yield return new WaitForSeconds(waveGap);
        SetEngaged(wave, true);
        current = wave;
        busy = false;
    }

    private IEnumerator Clear()
    {
        busy = true;
        current = null;
        GameProgress.Stage2Cleared = true;
        if (ropeReturn != null) ropeReturn.Unlock();
        yield break;
    }
}
