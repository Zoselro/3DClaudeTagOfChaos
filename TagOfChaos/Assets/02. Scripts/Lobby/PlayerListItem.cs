using TMPro;
using UnityEngine;

public class PlayerListItem : MonoBehaviour
{
    [SerializeField] private TMP_Text nicknameText;

    public void SetNickname(string nickname)
    {
        nicknameText.text = nickname;
    }
}
