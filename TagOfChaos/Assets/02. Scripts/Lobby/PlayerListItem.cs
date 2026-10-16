using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 대기실 참가자 목록 한 줄: 이름 + 녹음 표시(목소리를 녹음한 사람, §26 H11) + (호스트 화면에만) 강퇴 버튼(Request1009Plan.md §19.6).
public class PlayerListItem : MonoBehaviour
{
    [SerializeField] private TMP_Text nicknameText;
    [SerializeField] private Button kickButton;

    [SerializeField] private string recordedMark = " <color=#66ff88>\u25CF</color>";

    public void SetNickname(string nickname, bool recorded = false)
    {
        nicknameText.text = recorded ? nickname + recordedMark : nickname;
    }

    public void SetKick(bool visible, Action onKick)
    {
        if (kickButton == null) return;
        kickButton.gameObject.SetActive(visible);
        kickButton.onClick.RemoveAllListeners();
        if (visible && onKick != null) kickButton.onClick.AddListener(() => onKick());
    }
}
