using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// HideOrSeekPlayer.prefab에 부착. bodyRenderer는 PlayerPaintCanvas.bodyRenderer와 인스펙터에서
// 동일한 SkinnedMeshRenderer를 연결한다(GameRule.md §1.5).
//
// Awake()에서 적용하는 이유: Unity는 같은 프레임 안에서 씬(또는 Instantiate)에 있는 모든
// 컴포넌트의 Awake가 전부 끝난 뒤에야 비로소 아무 컴포넌트의 Start가 시작된다는 것을 보장한다 —
// HideOrSeekPlayer.Awake()가 networkSync를 IsMine 여부와 무관하게 최우선 생성하는 것과 정확히
// 같은 근거(research.md §5.7). 여기서 sharedMaterial을 미리 바꿔두면, 나중에 실행되는
// PlayerPaintCanvas.Start()의 InitPaintCanvas()가 bodyRenderer.sharedMaterial의 _MainTex를
// 읽어 합성 머티리얼을 만드는 시점에는 이미 올바른 스킨이 반영돼 있다.
public class PlayerSkinApplier : MonoBehaviourPunCallbacks
{
    [SerializeField] private PhotonView pv;
    [SerializeField] private Renderer bodyRenderer;
    [SerializeField] private Material[] skins; // 인덱스 0=A/1=B/2=C

    private void Awake()
    {
        ApplySkin();
    }

    // 다른 클라이언트 관점에서 이 캐릭터가 스폰된 시점에 소유자의 SkinIndex가 아직 서버에
    // 반영되기 전이었을 가능성에 대한 방어 — 값이 늦게 도착하면 이 콜백이 재적용한다.
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (targetPlayer != pv.Owner) return;
        if (!changedProps.ContainsKey(NetKeys.SkinIndex)) return;
        ApplySkin();
    }

    private void ApplySkin()
    {
        if (bodyRenderer == null || skins == null || skins.Length == 0 || pv.Owner == null) return;

        int index = pv.Owner.CustomProperties.TryGetValue(NetKeys.SkinIndex, out object v) ? (int)v : 0;
        index = Mathf.Clamp(index, 0, skins.Length - 1);
        bodyRenderer.sharedMaterial = skins[index];
    }
}
