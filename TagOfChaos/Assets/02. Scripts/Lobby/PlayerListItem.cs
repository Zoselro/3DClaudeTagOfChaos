using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 대기실 참가자 목록 한 줄: 이름 + (호스트 화면에만) 강퇴 버튼(Request1009Plan.md §19.6).
public class PlayerListItem : MonoBehaviour
{
    [SerializeField] private TMP_Text nicknameText;
    [SerializeField] private Button kickButton;

    public void SetNickname(string nickname)
    {
        nicknameText.text = nickname;
    }

    public void SetKick(bool visible, Action onKick)
    {
        if (kickButton == null) return;
        kickButton.gameObject.SetActive(visible);
        kickButton.onClick.RemoveAllListeners();
        if (visible && onKick != null) kickButton.onClick.AddListener(() => onKick());
    }
}
