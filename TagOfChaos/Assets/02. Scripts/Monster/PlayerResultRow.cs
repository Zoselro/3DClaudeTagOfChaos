using TMPro;
using UnityEngine;

// 결과 화면 플레이어 목록 행(이름+상태) — GameRule.md §8.2, §10.2.
public class PlayerResultRow : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text statusText;
    // 표시 문구는 프리팹 인스펙터에서 입력한다(코드에 한글 금지). "✕"는 기본 폰트에 글리프가 없어 □로 보였다(Bug-fix-plan.md §24.6).
    [SerializeField] private string monsterLabel = "(Monster)";
    [SerializeField] private string aliveLabel = "O Alive";
    [SerializeField] private string brokenLabel = "X Broken";

    public void SetMonster(string nickname)
    {
        if (nameText != null) nameText.text = nickname;
        if (statusText != null) statusText.text = monsterLabel;
    }

    public void SetCookie(string nickname, bool alive)
    {
        if (nameText != null) nameText.text = nickname;
        if (statusText != null) statusText.text = alive ? aliveLabel : brokenLabel;
    }
}
