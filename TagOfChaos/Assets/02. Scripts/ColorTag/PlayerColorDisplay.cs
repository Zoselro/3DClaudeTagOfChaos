using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;

// 최종 색상 적용: 일반 플레이어는 라운드마다 PlayerPaintCanvas가 이미 확정색으로 정리해두므로 할 일이 없고,
// 술래 1명에 대해서만 확정 4색 중 치환된 슬롯을 캔버스에 반영한다 (6.3)
[RequireComponent(typeof(PlayerPaintCanvas))]
public class PlayerColorDisplay : MonoBehaviourPunCallbacks
{
    private const int CompleteRoundIndex = 5;

    [SerializeField] private PhotonView pv;
    [SerializeField] private ColorPaletteSO palette;
    [SerializeField] private Material colorReplaceMaterial;

    private PlayerPaintCanvas paintCanvas;
    private bool hasApplied;

    private void Awake()
    {
        paintCanvas = GetComponent<PlayerPaintCanvas>();
    }

    private void Start()
    {
        TryApplyTaggerColor();
    }

    public override void OnEnable()
    {
        base.OnEnable();
        PhotonNetwork.AddCallbackTarget(this);
    }

    public override void OnDisable()
    {
        base.OnDisable();
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    public override void OnRoomPropertiesUpdate(Hashtable changedProps)
    {
        if (!changedProps.ContainsKey(NetKeys.RoundIndex)) return;
        TryApplyTaggerColor();
    }

    private void TryApplyTaggerColor()
    {
        if (hasApplied) return;
        if (paintCanvas == null || paintCanvas.PaintCanvas == null) return; // 아직 캔버스가 생성되기 전
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return;

        var props = PhotonNetwork.CurrentRoom.CustomProperties;
        if (!props.TryGetValue(NetKeys.RoundIndex, out object riObj)) return;
        if ((int)riObj != CompleteRoundIndex) return;

        if (!props.TryGetValue(NetKeys.TaggerActorNumber, out object taggerObj)) return;
        int taggerActorNumber = (int)taggerObj;
        if (taggerActorNumber < 0 || pv.Owner == null || taggerActorNumber != pv.Owner.ActorNumber) return; // 나는 술래가 아님

        if (!props.TryGetValue(NetKeys.TaggerVariantSet, out object variantObj)) return;

        int[] baseSet = new int[4];
        for (int i = 0; i < 4; i++)
            baseSet[i] = (int)props[NetKeys.ColorPrefix + i];

        int[] variantSet = (int[])variantObj;

        int slot = TaggerColorAssigner.FindSwappedSlot(baseSet, variantSet);
        if (slot < 0) return;

        ApplyColorReplace(palette.GetColor(baseSet[slot]), palette.GetColor(variantSet[slot]));
        hasApplied = true;
    }

    // 캔버스 전체에서 oldColor로 칠해진 픽셀만 newColor로 치환 (6.3, colorReplaceMaterial)
    private void ApplyColorReplace(Color oldColor, Color newColor)
    {
        RenderTexture canvas = paintCanvas.PaintCanvas;

        colorReplaceMaterial.SetColor("_OldColor", oldColor);
        colorReplaceMaterial.SetColor("_NewColor", newColor);

        RenderTexture temp = RenderTexture.GetTemporary(canvas.width, canvas.height, 0, canvas.format);
        Graphics.Blit(canvas, temp);
        Graphics.Blit(temp, canvas, colorReplaceMaterial);
        RenderTexture.ReleaseTemporary(temp);
    }
}
