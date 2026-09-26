using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// 파괴된 쿠키(hitCount>=2)의 관전 모드(GameRule.md §6.3). HideOrSeekPlayer.prefab에 부착, 로컬 소유자 전용 —
// HideOrSeekPlayer.RequestGrabKill()이 파괴되는 순간 EnterSpectatorMode()를 호출한다.
//
// 이 컴포넌트는 "누구를 볼지"만 정하고, 카메라 조작은 쿠키·괴물과 같은 Camera_Ctrl에 맡긴다. 예전에는
// Camera_Ctrl을 끄고 대상 등 뒤 고정 위치로 카메라를 직접 옮겨 우클릭 드래그 회전이 불가능했고, 대상 목록이
// Space를 누를 때만 갱신돼 보던 쿠키가 파괴되면 시체를 따라가거나, 퇴장으로 오브젝트가 사라지면 예외가 났다
// (Bug-fix-plan.md §28.4).
//
// 후보는 캐릭터 공통 계약(IGameCharacter)과 CharacterRegistry로만 찾는다 — 예전에는 쿠키·괴물 타입을 각각
// FindObjectsByType로 훑고 카메라 설정도 타입으로 분기했다(research.md §12 E1). 새 캐릭터 종류가 생겨도 수정할 필요가 없다.
public class SpectatorController : MonoBehaviourPunCallbacks
{
    // 관전 후보 규칙. 새 규칙이 필요하면 항목과 IsAllowedRole 조건만 추가한다.
    public enum SpectateTargets
    {
        AliveCookies,             // 살아 있는 다른 쿠키만(기본, GameRule.md §6.3)
        AliveCookiesAndMonsters,  // 괴물 시점도 관전
    }

    // 관전 대상이 바뀔 때 알린다(null = 관전 대상 없음/종료). 씬 UI(SpectatorLabel)가 구독한다 — 프리팹이 씬
    // 오브젝트를 직접 참조하지 않도록 이벤트로 분리했다.
    public static event System.Action<string> SpectateTargetChanged;

    [SerializeField] private PhotonView pv;
    [SerializeField] private SpectateTargets targets = SpectateTargets.AliveCookies;

    // 볼 대상이 하나도 없을 때 후보 재탐색 간격 — 매 프레임 후보를 다시 모으지 않도록 한다.
    private const float EmptyRetryInterval = 0.5f;
    private float nextEmptyRetryTime;

    private readonly List<IGameCharacter> candidates = new List<IGameCharacter>();
    private IGameCharacter currentTarget;
    private bool isSpectating;
    private Camera_Ctrl camCtrl;

    public void EnterSpectatorMode()
    {
        if (!pv.IsMine || isSpectating) return;
        isSpectating = true;

        camCtrl = Camera.main != null ? Camera.main.GetComponent<Camera_Ctrl>() : null;
        if (camCtrl == null)
        {
            Debug.LogError("[Spectator] Camera_Ctrl not found on Main Camera.");
            return;
        }
        camCtrl.enabled = true; // 끄지 않는다 — 우클릭 드래그 회전·커서 잠금을 그대로 쓴다
        SwitchToNext();
    }

    private void Update()
    {
        if (!pv.IsMine || !isSpectating || camCtrl == null) return;

        if (PlayerInput.SpectateNextPressed)
        {
            SwitchToNext(); // 수동 전환(키는 InputBindings)
        }
        else if (!IsCandidate(currentTarget) && Time.unscaledTime >= nextEmptyRetryTime)
        {
            SwitchToNext(); // 보던 대상이 파괴·퇴장했으면 자동 전환(대상이 없으면 0.5초마다 재시도)
            if (currentTarget == null) nextEmptyRetryTime = Time.unscaledTime + EmptyRetryInterval;
        }
    }

    private void OnDestroy()
    {
        if (isSpectating) SpectateTargetChanged?.Invoke(null);
    }

    private void SwitchToNext()
    {
        RefreshCandidates();
        if (candidates.Count == 0)
        {
            // 볼 대상이 없으면 카메라는 마지막 위치에 머문다(추적 대상 해제).
            if (currentTarget != null || camCtrl.FollowTarget != null)
            {
                currentTarget = null;
                camCtrl.SetFollowTarget(null, Camera_Ctrl.CookieTargetHeight, -1f, keepRotation: true);
                SpectateTargetChanged?.Invoke(null);
            }
            return;
        }

        int currentIndex = currentTarget != null ? candidates.IndexOf(currentTarget) : -1;
        IGameCharacter next = candidates[(currentIndex + 1) % candidates.Count];
        if (next == currentTarget) return;

        currentTarget = next;
        camCtrl.SetFollowTarget(next.gameObject, next.CameraTargetHeight, next.CameraDistance, keepRotation: true);
        SpectateTargetChanged?.Invoke(GetDisplayName(next));
    }

    private void RefreshCandidates()
    {
        candidates.Clear();
        foreach (IGameCharacter character in CharacterRegistry.All)
            if (IsCandidate(character)) candidates.Add(character);

        // 입장 순서로 정렬해 순환 순서를 모든 상황에서 일정하게 유지한다.
        candidates.Sort((a, b) => GetActorNumber(a).CompareTo(GetActorNumber(b)));
    }

    private bool IsCandidate(IGameCharacter character)
    {
        if (character == null || !CharacterRegistry.IsAlive(character)) return false; // 퇴장 등으로 파괴됨
        PhotonView view = character.View;
        if (view == null || view.Owner == null || view.IsMine) return false; // 자기 자신(이미 파괴됨) 제외
        return IsAllowedRole(character.Role) && character.IsSpectatable;
    }

    private bool IsAllowedRole(CharacterRole role)
    {
        switch (targets)
        {
            case SpectateTargets.AliveCookiesAndMonsters: return true;
            default: return role == CharacterRole.Cookie;
        }
    }

    private static int GetActorNumber(IGameCharacter character)
    {
        PhotonView view = character.View;
        return view != null && view.Owner != null ? view.Owner.ActorNumber : int.MaxValue;
    }

    private static string GetDisplayName(IGameCharacter character)
    {
        var owner = character.View != null ? character.View.Owner : null;
        if (owner == null) return string.Empty;
        return string.IsNullOrEmpty(owner.NickName) ? "#" + owner.ActorNumber : owner.NickName;
    }
}
