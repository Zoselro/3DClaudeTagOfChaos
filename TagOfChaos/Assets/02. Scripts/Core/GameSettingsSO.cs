using UnityEngine;

// 게임 규칙 수치를 한 곳에 모은 전역 설정(CLAUDE.md 폴더 규칙: 전역 SO → Assets/Resources/GameSettings).
// 인원·괴물 수·시간·색 슬롯 수가 여러 클래스에 상수로 흩어져 있어(4명, 60초, 600초, 30초, 슬롯 4개 …)
// 인원을 늘리려면 코드를 여러 곳 고쳐야 했다. 이제 이 에셋의 값만 바꾸면 된다(Bug-fix-plan.md §27).
[CreateAssetMenu(menuName = "TagOfChaos/GameSettings", fileName = "GameSettings")]
public class GameSettingsSO : ScriptableObject
{
    [Header("Room")]
    [Tooltip("방 최대 인원. 방을 만들 때 RoomOptions.MaxPlayers로 쓰이며, 시작 조건은 '정원이 모두 찼을 때'다.")]
    [SerializeField, Min(2)] private int maxPlayers = 4;

    [Tooltip("한 판의 괴물 수. 항상 (현재 인원 - 1) 이하로 제한된다(쿠키가 최소 1명은 있어야 함).")]
    [SerializeField, Min(1)] private int monsterCount = 1;

    [Header("Monster Selection")]
    [Tooltip("정원이 찬 뒤 가마솥 입장자가 모자라면 이 시간(초) 후 남은 자리를 무작위로 채운다.")]
    [SerializeField, Min(1f)] private float monsterSelectTimeout = 30f;

    [Header("Paint Phase")]
    [SerializeField, Min(1f)] private float paintPhaseDuration = 60f;
    [Tooltip("시작 버튼 시점부터 색칠 종료 시각을 계산할 때 더하는 여유(쿠키들의 GameScene 로딩 시간 보정).")]
    [SerializeField, Min(0f)] private float sceneLoadGrace = 3f;
    [Tooltip("쿠키 한 명이 등록할 수 있는 최대 색 슬롯 수.")]
    [SerializeField, Min(1)] private int maxColorSlots = 4;
    [Tooltip("색칠 중 몸 콜라이더를 현재 포즈로 다시 굽는 최소 간격(초). 짧을수록 정확하지만 비용이 크다(Bug-fix-plan.md §30.5).")]
    [SerializeField, Min(0.02f)] private float paintColliderRefreshInterval = 0.2f;

    [Header("Survival")]
    [SerializeField, Min(1f)] private float survivalDuration = 600f;
    [Tooltip("괴물이 전원 나간 뒤 대기실로 돌아가기까지의 경고 시간(초).")]
    [SerializeField, Min(0f)] private float monsterDepartureReturnDelay = 5f;

    [Header("Monster Tentacle Dash (GameRule.md §4.3)")]
    [SerializeField, Min(0f)] private float tentacleDashDistance = 20f;
    [SerializeField, Min(0.01f)] private float tentacleDashDuration = 0.25f;
    [SerializeField, Min(0f)] private float tentacleDashCooldown = 15f;
    [Tooltip("돌진 경로 장애물 검사(SphereCast) 반경 — 괴물 몸 두께 정도.")]
    [SerializeField, Min(0f)] private float tentacleDashRadius = 0.4f;

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
    public int MonsterCount => monsterCount;
    public float MonsterSelectTimeout => monsterSelectTimeout;
    public float PaintPhaseDuration => paintPhaseDuration;
    public float SceneLoadGrace => sceneLoadGrace;
    public int MaxColorSlots => maxColorSlots;
    public float PaintColliderRefreshInterval => paintColliderRefreshInterval;
    public float SurvivalDuration => survivalDuration;
    public float MonsterDepartureReturnDelay => monsterDepartureReturnDelay;
    public float TentacleDashDistance => tentacleDashDistance;
    public float TentacleDashDuration => tentacleDashDuration;
    public float TentacleDashCooldown => tentacleDashCooldown;
    public float TentacleDashRadius => tentacleDashRadius;
    public int CharacterSyncRate => characterSyncRate;
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

    // 현재 방 인원에서 실제로 뽑을 괴물 수 — 쿠키가 최소 1명 남도록 제한한다.
    public int MonsterCountFor(int playerCount) => Mathf.Clamp(monsterCount, 1, Mathf.Max(1, playerCount - 1));

    private void OnValidate()
    {
        monsterCount = Mathf.Clamp(monsterCount, 1, Mathf.Max(1, maxPlayers - 1));
    }
}
