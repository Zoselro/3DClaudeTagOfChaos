using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

// GameLobbyScene 상시 노출 UI에 부착. 대기실에서 언제든 다시 눌러 바꿀 수 있다 — 강제 선택이
// 아니며, 한 번도 안 누르면 기본값(A)로 스폰된다(GameRule.md §1.5).
public class PlayerSkinSelector : MonoBehaviour
{
    [SerializeField] private Button skinAButton;
    [SerializeField] private Button skinBButton;
    [SerializeField] private Button skinCButton;

    private void Awake()
    {
        skinAButton.onClick.AddListener(() => SelectSkin(0));
        skinBButton.onClick.AddListener(() => SelectSkin(1));
        skinCButton.onClick.AddListener(() => SelectSkin(2));
    }

    private void SelectSkin(int index)
    {
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { NetKeys.SkinIndex, index } });
    }
}
