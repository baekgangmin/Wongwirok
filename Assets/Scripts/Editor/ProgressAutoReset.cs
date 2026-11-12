using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 에디터 전용: 인간 세계 씬에서 플레이를 시작하면 "보스 깬 기록"을 자동으로 지운다.
// (테스트할 때마다 연못에 다시 들어갈 수 있게. 실제 게임 빌드에는 영향 없음)
[InitializeOnLoad]
public static class ProgressAutoReset
{
    static ProgressAutoReset()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode)
            return;
        if (EditorSceneManager.GetActiveScene().name != "HumanWorld_01")
            return;
        if (GameProgress.Stage1Cleared)
        {
            GameProgress.ResetAll();
            Debug.Log("[원귀록] 인간 세계에서 시작 → 보스 깬 기록을 자동으로 지웠습니다.");
        }
    }
}
