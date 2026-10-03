using UnityEngine;

// 괴물 시점(GameFixPlan.md F6). 테스트를 위해 두 방식 모두 남긴다.
public enum MonsterViewMode
{
    FirstPerson,
    ThirdPerson,
}

// 게임 규칙 수치를 한 곳에 모은 전역 설정(CLAUDE.md 폴더 규칙: 전역 SO → Assets/Resources/GameSettings).
// 인원·괴물 수·시간·색 슬롯 수가 여러 클래스에 상수로 흩어져 있어(4명, 60초, 600초, 30초, 슬롯 4개 …)
// 인원을 늘리려면 코드를 여러 곳 고쳐야 했다. 이제 이 에셋의 값만 바꾸면 된다(Bug-fix-plan.md §27).
[CreateAssetMenu(menuName = "TagOfChaos/GameSettings", fileName = "GameSettings")]
public class GameSettingsSO : ScriptableObject
{
    [Header("Room")]
    [Tooltip("방 최대 인원(방 만들기에서 고를 수 있는 최댓값, EscapePlan.md §1.7). 시작 조건은 '정원이 모두 찼을 때'다.")]
    [SerializeField, Min(2)] private int maxPlayers = 8;
    [Tooltip("방 만들기에서 고를 수 있는 최소 인원.")]
    [SerializeField, Min(2)] private int minPlayers = 4;
    [Tooltip("방 만들기의 기본 인원.")]
    [SerializeField, Min(2)] private int defaultPlayers = 4;

    [Tooltip("총 인원별 괴물 수(EscapePlan.md §1.1). 인원이 늘면 괴물이 늘어난다. 표에 없는 인원은 monsterCount를 쓴다.")]
    [SerializeField] private RoleRow[] roleTable =
    {
        new RoleRow(4, 1),
        new RoleRow(5, 1),
        new RoleRow(6, 1),
        new RoleRow(7, 2),
        new RoleRow(8, 2),
    };

    [Tooltip("이 인원 이상이면 스파이가 정확히 1명 나온다(그 미만은 0명). 스파이는 인원이 늘어도 늘지 않는다(2026-10-03).")]
    [SerializeField, Min(2)] private int spyMinPlayers = 5;

    [Tooltip("한 판의 괴물 수. 항상 (현재 인원 - 1) 이하로 제한된다(쿠키가 최소 1명은 있어야 함).")]
    [SerializeField, Min(1)] private int monsterCount = 1;

    [Header("Escape — Spy")]
    [Tooltip("스파이가 탈출 장치에서 재료를 뺀 뒤 이 시간(초)이 지나면 괴물을 뺀 모두에게 '스파이가 OO를 가져갔다' 알림을 띄운다. " +
             "그사이 재료가 다시 장치에 끼워지면 띄우지 않는다(Request1003bPlan.md §2).")]
    [SerializeField, Min(0f)] private float stealNoticeDelaySeconds = 5f;
    public float StealNoticeDelaySeconds => stealNoticeDelaySeconds;

    [Header("Paint Phase")]
    [SerializeField, Min(1f)] private float paintPhaseDuration = 60f;
    [Tooltip("시작 버튼 시점부터 색칠 종료 시각을 계산할 때 더하는 여유(쿠키들의 GameScene 로딩 시간 보정).")]
    [SerializeField, Min(0f)] private float sceneLoadGrace = 3f;
    [Tooltip("쿠키 한 명이 등록할 수 있는 최대 색 슬롯 수.")]
    [SerializeField, Min(1)] private int maxColorSlots = 4;
    [Tooltip("색칠 중 몸 콜라이더를 현재 포즈로 다시 굽는 최소 간격(초). 짧을수록 정확하지만 비용이 크다(Bug-fix-plan.md §30.5).")]
    [SerializeField, Min(0.02f)] private float paintColliderRefreshInterval = 0.2f;

    [Header("Maps")]
    [Tooltip("판마다 무작위로 고르는 게임 맵 씬 이름(빌드 목록에 있어야 함). 비어 있으면 게임을 시작할 수 없다(시작 시 오류 로그). 직전 판 맵은 다시 고르지 않는다(맵이 2개 이상일 때).")]
    [SerializeField] private string[] gameMapScenes = new string[0];

    [Header("Room Time Settings (EscapePlan.md §1.7, 분 단위)")]
    [SerializeField, Min(1)] private int minTimeLimitMinutes = 10;
    [SerializeField, Min(1)] private int maxTimeLimitMinutes = 40;
    [SerializeField, Min(1)] private int defaultTimeLimitMinutes = 10;
    [SerializeField, Min(1)] private int minTimeAttackMinutes = 1;
    [SerializeField, Min(1)] private int maxTimeAttackMinutes = 10;
    [SerializeField, Min(1)] private int defaultTimeAttackMinutes = 1;

    [Header("Survival")]
    [SerializeField, Min(1f)] private float survivalDuration = 600f;
    [Tooltip("괴물이 전원 나간 뒤 대기실로 돌아가기까지의 경고 시간(초).")]
    [SerializeField, Min(0f)] private float monsterDepartureReturnDelay = 5f;

    [Header("Monster Tentacle Dash (GameRule.md §4.3)")]
    [SerializeField, Min(0f)] private float tentacleDashDistance = 20f;
    [SerializeField, Min(0.01f)] private float tentacleDashDuration = 0.25f;
    [SerializeField, Min(0f)] private float tentacleDashCooldown = 15f;
    [Tooltip("돌진이 따라 올라가거나 내려가는 최대 경사(도). 더 가파른 면은 벽처럼 막혀 돌진 이동이 끝난다(Bug-fix-plan.md §41 ㊳).")]
    [SerializeField, Range(0f, 89f)] private float tentacleDashMaxSlope = 45f;
    [Tooltip("돌진 중 지면 붙이기 여유(m). 한 스텝 동안 최대 경사(TentacleDashMaxSlope)로 떨어질 수 있는 높이에 이 값을 더한 거리 안에 지면이 있으면 붙이고(언덕 꼭대기·내리막), 더 먼 낭떠러지는 그대로 떨어진다.")]
    [SerializeField, Min(0f)] private float tentacleDashGroundSnap = 0.6f;

    [Header("Monster Grab (GameFixPlan.md F1)")]
    [Tooltip("괴물 몸 앞면에서 조준한 쿠키를 잡을 수 있는 거리(m). 몸 앞면은 중심에서 약 1.87m(MonsterPlayer 프리팹 측정).")]
    [SerializeField, Min(0.5f)] private float grabReachFromFront = 2f;
    [Tooltip("조준 판정 구의 반지름(m). 조준이 조금 빗나가도 잡히게 한다.")]
    [SerializeField, Min(0.05f)] private float grabAimRadius = 0.4f;

    [Header("Monster View (GameFixPlan.md F6)")]
    [Tooltip("괴물이 판을 시작할 때의 시점.")]
    [SerializeField] private MonsterViewMode monsterDefaultView = MonsterViewMode.FirstPerson;
    [Tooltip("true면 괴물이 전환 키(InputBindings.ToggleViewKey)로 1인칭/3인칭을 바꿀 수 있다(테스트용). 출시 전에 끌 수 있다.")]
    [SerializeField] private bool allowMonsterViewToggle = true;

    [Header("Network Load (research.md §12.4)")]
    [Tooltip("캐릭터 위치 동기화 횟수(초당, PhotonNetwork.SerializationRate). PUN 기본 10.")]
    [SerializeField, Range(1, 30)] private int characterSyncRate = 10;
    [Tooltip("색칠 스탬프 묶음 전송 횟수(초당) — 기준 인원일 때의 값.")]
    [SerializeField, Min(1f)] private float paintStrokeSendRate = 15f;
    [Tooltip("이 인원까지는 위 전송 횟수를 그대로 쓰고, 넘으면 받는 사람 수에 반비례해 줄여 방 전체 메시지 수를 비슷하게 유지한다.")]
    [SerializeField, Min(2)] private int paintStrokeReferencePlayers = 4;
    [Tooltip("인원이 많아도 이 값 아래로는 줄이지 않는다(색칠이 너무 뚝뚝 끊겨 보이지 않도록).")]
    [SerializeField, Min(1f)] private float minPaintStrokeSendRate = 5f;

    [Header("Spawn")]
    [Tooltip("쿠키 스폰·리스폰 시 스폰 지점 기준 흩뿌림 범위(m).")]
    [SerializeField, Min(0f)] private float cookieSpawnRange = 5f;
    [Tooltip("괴물 스폰·리스폰 시 스폰 지점 기준 흩뿌림 범위(m). 괴물이 여럿일 때 서로 겹치지 않게 한다.")]
    [SerializeField, Min(0f)] private float monsterSpawnRange = 4f;
    [Tooltip("캐릭터가 이 높이 아래로 떨어지면 스폰 지점으로 되돌린다(FallGuard, VoidKillZone을 놓친 맵의 최후 방어선).")]
    [SerializeField] private float fallRespawnHeight = -100f;

    public int MaxPlayers => maxPlayers;
    public int MinPlayers => minPlayers;
    public int DefaultPlayers => defaultPlayers;
    public int MinTimeLimitMinutes => minTimeLimitMinutes;
    public int MaxTimeLimitMinutes => maxTimeLimitMinutes;
    public int DefaultTimeLimitMinutes => defaultTimeLimitMinutes;
    public int MinTimeAttackMinutes => minTimeAttackMinutes;
    public int MaxTimeAttackMinutes => maxTimeAttackMinutes;
    public int DefaultTimeAttackMinutes => defaultTimeAttackMinutes;
    public int MonsterCount => monsterCount;
    public float PaintPhaseDuration => paintPhaseDuration;
    public float SceneLoadGrace => sceneLoadGrace;
    public int MaxColorSlots => maxColorSlots;
    public float PaintColliderRefreshInterval => paintColliderRefreshInterval;
    public float SurvivalDuration => survivalDuration;
    public float MonsterDepartureReturnDelay => monsterDepartureReturnDelay;
    public float TentacleDashDistance => tentacleDashDistance;
    public float TentacleDashDuration => tentacleDashDuration;
    public float TentacleDashCooldown => tentacleDashCooldown;
    public float TentacleDashMaxSlope => tentacleDashMaxSlope;
    public float TentacleDashGroundSnap => tentacleDashGroundSnap;
    public int CharacterSyncRate => characterSyncRate;
    public float GrabReachFromFront => grabReachFromFront;
    public float GrabAimRadius => grabAimRadius;
    public MonsterViewMode MonsterDefaultView => monsterDefaultView;
    public bool AllowMonsterViewToggle => allowMonsterViewToggle;
    public float CookieSpawnRange => cookieSpawnRange;
    public float MonsterSpawnRange => monsterSpawnRange;
    public float FallRespawnHeight => fallRespawnHeight;

    // 색칠 스탬프 묶음 전송 간격(초). 받는 사람 수(인원-1)가 기준보다 많으면 비례해서 늘린다 — 4명 이하에서는
    // 예전과 같은 1/15초, 8명이면 약 1/6.4초(방 전체 색칠 메시지 수가 인원 제곱으로 늘지 않게, research.md §12.4).
    public float PaintStrokeSendIntervalFor(int playerCount)
    {
        int receivers = Mathf.Max(1, playerCount - 1);
        int referenceReceivers = Mathf.Max(1, paintStrokeReferencePlayers - 1);
        float rate = paintStrokeSendRate * Mathf.Min(1f, (float)referenceReceivers / receivers);
        return 1f / Mathf.Max(minPaintStrokeSendRate, rate);
    }

    public System.Collections.Generic.IReadOnlyList<string> GameMapScenes => gameMapScenes;

    // 이번 판 맵(GameScenePlan.md D1): 목록에서 무작위, 직전 판 맵은 제외. 목록이 비면 null — 빌드에 없는 기본 씬으로
    // 대신 가지 않도록 호출부가 시작을 막는다(Bug-fix-plan.md §41 ㊷).
    public string PickGameMap(string previousMap)
    {
        var candidates = new System.Collections.Generic.List<string>();
        foreach (string scene in gameMapScenes)
            if (!string.IsNullOrEmpty(scene) && scene != previousMap) candidates.Add(scene);
        if (candidates.Count == 0)
            foreach (string scene in gameMapScenes)
                if (!string.IsNullOrEmpty(scene)) candidates.Add(scene);
        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
    }

    // 맵 키를 받지 못했을 때 쓰는 대체 맵(목록의 첫 유효 항목, 없으면 null).
    public string FirstGameMap
    {
        get
        {
            foreach (string scene in gameMapScenes)
                if (!string.IsNullOrEmpty(scene)) return scene;
            return null;
        }
    }

    // 현재 방 인원에서 실제로 뽑을 괴물 수(EscapePlan.md §1.1 인원표). 표에 없는 인원(오프라인 개발 방 등)은
    // monsterCount를 쓰되 쿠키가 최소 1명 남도록 제한한다.
    public int MonsterCountFor(int playerCount) =>
        TryGetRoleRow(playerCount, out RoleRow row) ? row.monsters : Mathf.Clamp(monsterCount, 1, Mathf.Max(1, playerCount - 1));

    // 한 판에 나올 수 있는 가장 많은 쿠키 수(= 필요 재료 수의 최댓값) — 인원표에서 (인원 − 괴물 − 스파이)의 최댓값.
    // 탈출 장치는 이 수만큼 칸을 만든다(EscapeRecipeSO.FillsCapacity).
    public int MaxCookieCount
    {
        get
        {
            int max = 0;
            if (roleTable != null)
                foreach (RoleRow r in roleTable) max = Mathf.Max(max, r.players - r.monsters - SpyCountFor(r.players));
            return max > 0 ? max : Mathf.Max(1, maxPlayers - monsterCount);
        }
    }

    // 현재 방 인원에서 뽑을 스파이 수: spyMinPlayers 이상이면 1명, 아니면 0명. 쿠키가 최소 1명은 남아야 한다.
    public int SpyCountFor(int playerCount) => playerCount >= spyMinPlayers && playerCount - MonsterCountFor(playerCount) >= 2 ? 1 : 0;
    public int SpyMinPlayers => spyMinPlayers;

    private bool TryGetRoleRow(int playerCount, out RoleRow row)
    {
        if (roleTable != null)
            foreach (RoleRow r in roleTable)
                if (r.players == playerCount) { row = r; return true; }
        row = default;
        return false;
    }

    [System.Serializable]
    public struct RoleRow
    {
        public int players;
        public int monsters;
        [HideInInspector] public int spies; // 예전 인원별 스파이 수 — 더 이상 읽지 않는다(스파이는 spyMinPlayers 규칙으로 1명). 에셋 호환용

        public RoleRow(int players, int monsters)
        {
            this.players = players;
            this.monsters = monsters;
            spies = 0;
        }
    }

    private void OnValidate()
    {
        monsterCount = Mathf.Clamp(monsterCount, 1, Mathf.Max(1, maxPlayers - 1));
        minPlayers = Mathf.Clamp(minPlayers, 2, maxPlayers);
        defaultPlayers = Mathf.Clamp(defaultPlayers, minPlayers, maxPlayers);
        maxTimeLimitMinutes = Mathf.Max(minTimeLimitMinutes, maxTimeLimitMinutes);
        defaultTimeLimitMinutes = Mathf.Clamp(defaultTimeLimitMinutes, minTimeLimitMinutes, maxTimeLimitMinutes);
        maxTimeAttackMinutes = Mathf.Max(minTimeAttackMinutes, maxTimeAttackMinutes);
        defaultTimeAttackMinutes = Mathf.Clamp(defaultTimeAttackMinutes, minTimeAttackMinutes, maxTimeAttackMinutes);
#if UNITY_EDITOR
        // 저장된 설정 에셋만 검사한다(테스트가 만드는 빈 임시 인스턴스는 제외).
        if (FirstGameMap == null && UnityEditor.EditorUtility.IsPersistent(this))
            Debug.LogWarning($"[GameSettings] gameMapScenes is empty on {name}. Games cannot start.", this);
#endif
    }
}
