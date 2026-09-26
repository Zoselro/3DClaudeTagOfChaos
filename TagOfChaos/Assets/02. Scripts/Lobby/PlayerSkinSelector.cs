using ExitGames.Client.Photon;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// GameLobbyScene 상시 노출 UI에 부착. 대기실에서 언제든 다시 눌러 바꿀 수 있다 — 강제 선택이
// 아니며, 한 번도 안 누르면 기본값(0번)으로 스폰된다(GameRule.md §1.5).
// 버튼은 스킨 목록(SkinCatalogSO) 수만큼 템플릿에서 만든다 — 예전에는 A/B/C 버튼 3개가 코드에 고정돼 있었다
// (research.md §12 E4). 버튼 글자는 목록의 label을 쓴다.
public class PlayerSkinSelector : MonoBehaviour
{
    [SerializeField] private SkinCatalogSO catalog;
    [SerializeField] private Button buttonTemplate; // 복제 원본(보통 첫 버튼), 버튼들은 이 오브젝트의 직계 자식

    private void Awake()
    {
        if (catalog == null || buttonTemplate == null)
        {
            Debug.LogWarning($"[PlayerSkinSelector] Catalog or button template is not assigned on {name}.");
            return;
        }

        var buttons = UiListBuilder.Sync(transform, buttonTemplate, catalog.Count);
        for (int i = 0; i < buttons.Count; i++)
        {
            int index = i; // 람다 캡처용 복사
            Button button = buttons[i];
            button.name = $"SkinButton{i}";
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectSkin(index));

            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = catalog.GetLabel(i);
        }
    }

    private void SelectSkin(int index)
    {
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { NetKeys.SkinIndex, index } });
    }
}
