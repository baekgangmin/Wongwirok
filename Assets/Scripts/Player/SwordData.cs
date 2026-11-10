using UnityEngine;

// 검 한 자루의 정보. 검마다 하나씩 만들어 두고, PlayerAttack에 장착할 검을 넣으면
// 공격력과 칼날 궤적 색이 함께 바뀐다. (나중에 마을 상점에서도 그대로 사용)
[CreateAssetMenu(fileName = "Sword_", menuName = "원귀록/검 정보", order = 0)]
public class SwordData : ScriptableObject
{
    [Header("기본 정보")]
    public string displayName = "버드나무 가지";
    [Tooltip("검 성장 단계 (1~5)")]
    public int tier = 1;
    public float attackPower = 10f;
    [Tooltip("마을 상점 가격 (혼전)")]
    public int price;

    [Header("칼날 궤적 색 (왼쪽 = 방금 지나간 자리, 오른쪽 = 사라지는 끝)")]
    public Gradient trailColor = new Gradient();
}
