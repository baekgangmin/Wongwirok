using UnityEngine;

// 진행 기록 (게임을 껐다 켜도 남음). 지금은 스테이지 1 보스를 깼는지만 기억한다.
public static class GameProgress
{
    private const string Stage1Key = "wongwirok_stage1_cleared";

    public static bool Stage1Cleared
    {
        get => PlayerPrefs.GetInt(Stage1Key, 0) == 1;
        set { PlayerPrefs.SetInt(Stage1Key, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(Stage1Key);
        PlayerPrefs.Save();
    }

    // 양기: 몬스터를 베면 모이고, 양기귀에게 건네 나뭇가지와 바꾼다
    private const string YanggiKey = "wongwirok_yanggi";
    private const string SwordTierKey = "wongwirok_sword_tier";

    public static event System.Action<int, int> YanggiChanged;   // (지금 양기, 바뀐 양)

    public static int Yanggi => PlayerPrefs.GetInt(YanggiKey, 0);

    public static void AddYanggi(int amount)
    {
        int now = Mathf.Max(0, Yanggi + amount);
        int delta = now - Yanggi;
        PlayerPrefs.SetInt(YanggiKey, now);
        PlayerPrefs.Save();
        YanggiChanged?.Invoke(now, delta);
    }

    // 지금 쥔 나뭇가지 단계 (1 = 버드나무, 기본 지급)
    public static int SwordTier
    {
        get => Mathf.Max(1, PlayerPrefs.GetInt(SwordTierKey, 1));
        set { PlayerPrefs.SetInt(SwordTierKey, value); PlayerPrefs.Save(); }
    }

    public static void ResetYanggiAndSword()
    {
        PlayerPrefs.DeleteKey(YanggiKey);
        PlayerPrefs.DeleteKey(SwordTierKey);
        PlayerPrefs.Save();
        YanggiChanged?.Invoke(0, 0);
    }
}

// 밧줄로 두 세계를 오가는 동안만 쓰는 정보 (씬이 바뀌어도 유지)
public static class RopeSession
{
    public static bool Active;
    // 귀의 세계에서 밧줄이 물속으로 이어지는 자리 (= 인간 세계에서 가라앉은 자리)
    public static Vector3 GhostAnchor;
    // 인간 세계에서 밧줄을 묶은 자리 (돌아올 곳)
    public static Vector3 HumanReturnPoint;
    public static float HumanReturnYaw;
}
