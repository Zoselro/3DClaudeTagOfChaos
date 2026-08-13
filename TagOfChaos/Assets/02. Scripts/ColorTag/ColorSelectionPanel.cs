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
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return;

        var props = PhotonNetwork.CurrentRoom.CustomProperties;
        if (!props.TryGetValue(NetKeys.RoundIndex, out object riObj)) return;

        int roundIndex = (int)riObj;
        bool isColorRound = roundIndex >= 0 && roundIndex < 4;

        gameObject.SetActive(isColorRound);
        if (!isColorRound) return;

        if (roundLabel != null)
            roundLabel.text = $"{roundIndex + 1} / 4";

        if (timeLabel != null && props.TryGetValue(NetKeys.RoundEndTime, out object endObj))
        {
            double remaining = System.Math.Max(0, (double)endObj - PhotonNetwork.Time);
            timeLabel.text = Mathf.CeilToInt((float)remaining).ToString();
        }

        UpdateSwatchLocks(props, roundIndex);
    }

    // Color0..Color(roundIndex-1)에 해당하는 스와치를 잠금 (3.3 중복 금지 규칙)
    private void UpdateSwatchLocks(Hashtable roomProps, int roundIndex)
    {
        if (swatches == null) return;

        bool[] usedColors = new bool[swatches.Length];
        for (int i = 0; i < roundIndex; i++)
        {
            if (roomProps.TryGetValue(NetKeys.ColorPrefix + i, out object colorObj))
            {
                int used = (int)colorObj;
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
