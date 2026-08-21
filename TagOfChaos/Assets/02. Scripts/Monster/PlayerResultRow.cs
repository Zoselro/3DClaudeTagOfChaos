using TMPro;
using UnityEngine;

// 결과 화면 플레이어 목록 행(이름+상태) — GameRule.md §8.2, §10.2.
public class PlayerResultRow : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text statusText;

    public void SetMonster(string nickname)
    {
        if (nameText != null) nameText.text = nickname;
        if (statusText != null) statusText.text = "(괴물)";
    }

    public void SetCookie(string nickname, bool alive)
    {
        if (nameText != null) nameText.text = nickname;
        if (statusText != null) statusText.text = alive ? "○ 생존" : "✕ 부숴짐";
    }
}
