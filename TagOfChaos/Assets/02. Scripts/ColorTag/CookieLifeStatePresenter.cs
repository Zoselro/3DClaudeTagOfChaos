using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// HideOrSeekPlayer.prefab에 부착 — 쿠키의 생존/파괴 상태를 그 캐릭터의 모습·물리에 반영한다(GameRule.md §4.4).
// (예전 이름 PlayerCrackDisplay, .meta GUID 유지로 프리팹 참조 보존.)
//
// 파괴 여부는 "속성 변경 통지 한 번"이 아니라 소유자의 현재 HitCount 값으로 계산하고, 여러 시점에 반복해서 맞춘다
// (idempotent). 예전에는 OnPlayerPropertiesUpdate 한 번에만 의존해, 통지를 놓치거나 통지 이후에 생긴 복사본은
// 콜라이더가 켜진 채 남아 파괴된 쿠키 자리에 보이지 않는 벽이 생길 수 있었다(Bug-fix-plan.md §28.3).
// 파괴 시에는 콜라이더를 끄는 것에 더해 몸 전체를 어떤 레이어와도 충돌하지 않는 BrokenCookie 레이어로 옮긴다.
// 표시물도 몸통 하나가 아니라 캐릭터의 모든 렌더러(이름표 TextMeshPro 포함)를 숨긴다 — 예전에는 몸통만 꺼서
// 파괴된 쿠키의 이름표가 공중에 남았다(Bug-fix-plan.md §30.3). 새 표시물을 붙여도 코드 수정 없이 함께 숨겨진다.
public class CookieLifeStatePresenter : MonoBehaviourPunCallbacks
{
    private const float ReconcileInterval = 0.5f; // 쿠키 수만큼 정수 비교 한 번 — 인원이 늘어도 비용이 작다

    [SerializeField] private PhotonView pv;
    [SerializeField] private Renderer[] keepVisibleWhenBroken = new Renderer[0]; // 파괴 후에도 보여야 하는 표시물(기본 없음)
    [SerializeField] private GameObject breakVfxPrefab; // 로컬 Instantiate
    [SerializeField] private string brokenLayerName = "BrokenCookie"; // ProjectSettings 충돌 매트릭스에서 모든 레이어와 충돌 해제된 레이어

    private enum LifeState { Unknown, Alive, Broken }

    private LifeState appliedState = LifeState.Unknown;
    private float nextReconcileTime;
    private int brokenLayer = -1;

    // 파괴 전 상태를 기억해 복원할 수 있게 한다(판 초기화 등으로 HitCount가 지워지는 경우).
    private readonly List<(Collider collider, bool enabled)> savedColliders = new List<(Collider, bool)>();
    private readonly List<(GameObject go, int layer)> savedLayers = new List<(GameObject, int)>();
    private readonly List<(Renderer renderer, bool enabled)> savedRenderers = new List<(Renderer, bool)>();

    public bool IsBroken => appliedState == LifeState.Broken;

    private void Awake()
    {
        brokenLayer = LayerMask.NameToLayer(brokenLayerName);
        if (brokenLayer < 0)
            Debug.LogWarning($"[CookieLife] Layer '{brokenLayerName}' not found. Broken cookies will only disable colliders.");
    }

    private void Start()
    {
        Reconcile("start"); // 이미 파괴된 상태로 생성된 복사본(통지 이후 생성)도 즉시 맞춘다
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (pv == null || targetPlayer != pv.Owner) return;
        if (!changedProps.ContainsKey(NetKeys.HitCount)) return;
        Reconcile("callback");
    }

    public override void OnJoinedRoom()
    {
        Reconcile("rejoin");
    }

    private void Update()
    {
        if (Time.unscaledTime < nextReconcileTime) return;
        nextReconcileTime = Time.unscaledTime + ReconcileInterval;
        Reconcile("periodic");
    }

    private void Reconcile(string reason)
    {
        if (pv == null || pv.Owner == null) return;
        LifeState desired = RoomState.IsBroken(pv.Owner) ? LifeState.Broken : LifeState.Alive;
        if (desired == appliedState) return;

        if (desired == LifeState.Broken) ApplyBroken();
        else if (appliedState == LifeState.Broken) RestoreAlive();

        appliedState = desired;
        if (desired == LifeState.Broken || reason != "start")
            Debug.Log($"[CookieLife] view={pv.ViewID} owner={pv.Owner.ActorNumber} -> {desired} (via {reason})");
    }

    private void ApplyBroken()
    {
        // 몸통·이름표 등 모든 표시물을 감추고 파편 연출로 대체한다.
        savedRenderers.Clear();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (keepVisibleWhenBroken != null && System.Array.IndexOf(keepVisibleWhenBroken, r) >= 0) continue;
            savedRenderers.Add((r, r.enabled));
            r.enabled = false;
        }

        // 모든 콜라이더를 끄고, 몸 전체를 비충돌 레이어로 옮긴다 — 둘 중 하나만으로도 벽이 되지 않도록 이중으로 막는다.
        savedColliders.Clear();
        foreach (var col in GetComponentsInChildren<Collider>(true))
        {
            savedColliders.Add((col, col.enabled));
            col.enabled = false;
        }

        savedLayers.Clear();
        if (brokenLayer >= 0)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                savedLayers.Add((t.gameObject, t.gameObject.layer));
                t.gameObject.layer = brokenLayer;
            }
        }

        if (breakVfxPrefab != null)
            Instantiate(breakVfxPrefab, transform.position, transform.rotation); // 전원 로컬 재생, 정밀 동기화 불필요
    }

    private void RestoreAlive()
    {
        foreach (var (r, enabled) in savedRenderers)
            if (r != null) r.enabled = enabled;
        foreach (var (col, enabled) in savedColliders)
            if (col != null) col.enabled = enabled;
        foreach (var (go, layer) in savedLayers)
            if (go != null) go.layer = layer;
        savedColliders.Clear();
        savedLayers.Clear();
        savedRenderers.Clear();
    }
}
