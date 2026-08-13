using Photon.Pun;
using UnityEngine;

public class OfflineModeBootstrap : MonoBehaviour
{
    [Header("색상 선택 테스트 자동 시작")]
    [SerializeField] private bool autoStartColorSelection = false; // 체크하면 Play 버튼만 눌러도 방 생성 + 색상 선택이 바로 시작됨 (필요할 때만 켜서 사용)
    [SerializeField] private string testRoomName = "OfflineTestRoom";
    private void Awake()
    {
        PhotonNetwork.OfflineMode = true;
    }

private void Start()
    {
        if (!autoStartColorSelection) return;
        if (!PhotonNetwork.OfflineMode) return;

        PhotonNetwork.CreateRoom(testRoomName);

        var manager = FindFirstObjectByType<ColorSelectionManager>();
        if (manager == null)
        {
            Debug.LogWarning("OfflineModeBootstrap: 씬에 ColorSelectionManager가 없어 색상 선택을 자동으로 시작하지 못했습니다.");
            return;
        }

        manager.StartColorSelection();
    }

}
