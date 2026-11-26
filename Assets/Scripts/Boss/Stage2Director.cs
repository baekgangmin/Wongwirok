using System.Collections;
using UnityEngine;

// 2스테이지 흐름: 복도 잡몹을 모두 베면 침전에 13궁녀가 세 무리(5/4/4)로 차례차례 바닥에서 솟아오른다.
// 마지막 무리까지 쓰러뜨리면 2스테이지를 깬 것으로 기록하고, 복도 끝 밧줄을 당겨 마을 우물 앞으로 돌아갈 수 있게 된다.
public class Stage2Director : MonoBehaviour
{
    [SerializeField] private Transform corridorMobs;     // 복도 잡몹들의 부모
    [SerializeField] private Transform[] waves;          // 궁녀 무리 1~3
    [SerializeField] private float riseDepth = 2.2f;
    [SerializeField] private float riseTime = 1.2f;
    [SerializeField] private float waveGap = 1.5f;
    [SerializeField] private GhostRopeReturn ropeReturn;   // 다 쓰러뜨리면 복도 끝 밧줄을 당길 수 있게 됨

    private Transform current;   // 지금 싸우는 무리 (처음엔 복도)
    private int nextWave;
    private bool busy;

    private void Start()
    {
        foreach (var w in waves) if (w != null) w.gameObject.SetActive(false);
        current = corridorMobs;
    }

    private void Update()
    {
        if (busy || current == null || Alive(current) > 0) return;
        if (nextWave < waves.Length) StartCoroutine(Rise(waves[nextWave++]));
        else StartCoroutine(Clear());
    }

    private static int Alive(Transform group)
    {
        int n = 0;
        foreach (var h in group.GetComponentsInChildren<EnemyHealth>()) if (!h.IsDead) n++;
        return n;
    }

    // 무리가 바닥 아래에서 솟아오른다 (쓰러뜨린 다음 무리까지 잠깐 쉼)
    private IEnumerator Rise(Transform wave)
    {
        busy = true;
        yield return new WaitForSeconds(waveGap);
        var ladies = new Transform[wave.childCount];
        var tops = new Vector3[ladies.Length];
        for (int i = 0; i < ladies.Length; i++) { ladies[i] = wave.GetChild(i); tops[i] = ladies[i].position; ladies[i].position = tops[i] + Vector3.down * riseDepth; }
        var chases = wave.GetComponentsInChildren<EnemyChase>();
        foreach (var c in chases) c.enabled = false;   // 다 솟기 전엔 움직이지 않게
        wave.gameObject.SetActive(true);
        for (float t = 0f; t < riseTime; t += Time.deltaTime)
        {
            float k = 1f - (1f - t / riseTime) * (1f - t / riseTime);
            for (int i = 0; i < ladies.Length; i++) ladies[i].position = Vector3.Lerp(tops[i] + Vector3.down * riseDepth, tops[i], k);
            yield return null;
        }
        for (int i = 0; i < ladies.Length; i++) ladies[i].position = tops[i];
        foreach (var c in chases) c.enabled = true;
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
