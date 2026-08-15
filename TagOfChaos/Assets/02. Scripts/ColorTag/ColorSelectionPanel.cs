using ExitGames.Client.Photon;
using Photon.Pun;
using TMPro;
using UnityEngine;

// 팔레트 패널 UI 총괄: 라운드/남은시간 표시 + 이미 확정된 색 스와치 잠금 (6.1)
public class ColorSelectionPanel : MonoBehaviourPunCallbacks
{
[SerializeField] private TextMeshProUGUI roundLabel;
    [SerializeField] private TextMeshProUGUI timeLabel;
    [SerializeField] private ColorSwatchButton[] swatches; // 인덱스 = 팔레트 색상 인덱스(0~9)

    private void Update()
    {
        if (!RoomState.TryGetInt(NetKeys.RoundIndex, out int roundIndex)) return;

        bool isColorRound = roundIndex >= 0 && roundIndex < 4;

        gameObject.SetActive(isColorRound);
        if (!isColorRound) return;

        if (roundLabel != null)
            roundLabel.text = $"{roundIndex + 1} / 4";

        if (timeLabel != null && RoomState.TryGetDouble(NetKeys.RoundEndTime, out double endTime))
        {
            double remaining = System.Math.Max(0, endTime - PhotonNetwork.Time);
            timeLabel.text = Mathf.CeilToInt((float)remaining).ToString();
        }

        UpdateSwatchLocks(roundIndex);
    }

    // Color0..Color(roundIndex-1)에 해당하는 스와치를 잠금 (3.3 중복 금지 규칙)
private void UpdateSwatchLocks(int roundIndex)
    {
        if (swatches == null) return;

        bool[] usedColors = new bool[swatches.Length];
        for (int i = 0; i < roundIndex; i++)
        {
            if (RoomState.TryGetInt(NetKeys.ColorPrefix + i, out int used))
            {
                if (used >= 0 && used < usedColors.Length)
                    usedColors[used] = true;
            }
        }

        for (int i = 0; i < swatches.Length; i++)
        {
            if (swatches[i] != null)
                swatches[i].SetLocked(usedColors[i]);
        }
    }
}
