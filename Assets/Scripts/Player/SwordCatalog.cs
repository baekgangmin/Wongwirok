using UnityEngine;

// 나뭇가지 5단계 목록. Resources/SwordCatalog 에 하나 두고 어느 씬에서든 단계 번호로 꺼내 쓴다.
[CreateAssetMenu(fileName = "SwordCatalog", menuName = "원귀록/나뭇가지 목록", order = 1)]
public class SwordCatalog : ScriptableObject
{
    [Tooltip("1단계부터 순서대로")]
    public SwordData[] swords;

    private static SwordCatalog instance;
    public static SwordCatalog Instance => instance != null ? instance : (instance = Resources.Load<SwordCatalog>("SwordCatalog"));

    public static SwordData Get(int tier)
    {
        var c = Instance;
        if (c == null || c.swords == null || tier < 1 || tier > c.swords.Length) return null;
        return c.swords[tier - 1];
    }

    public static int Count => Instance != null && Instance.swords != null ? Instance.swords.Length : 0;
}
