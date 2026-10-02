// 효과음 ID(SoundPlan.md §3.2). 값은 SO에 숫자로 저장되므로 묶음마다 백 단위 구간을 두고, 새 소리는 구간 끝에 덧붙인다
// (기존 값을 바꾸거나 중간에 끼우지 않는다). 새 소리 = 항목 추가 + Tools/TagOfChaos/Audio/Build Catalogs(파일 이름 = ID).
public enum SoundId
{
    None = 0,

    // UI(2D)
    UiHover = 100,
    UiClick = 101,
    UiConfirm = 102,
    UiCancel = 103,
    UiStart = 104,
    UiError = 105,
    UiOpen = 106,
    UiClose = 107,
    UiSaved = 108,
    UiTick = 109,
    UiTickFinal = 110,
    UiSlotSelect = 111,
    UiToastInfo = 112,
    UiToastAlert = 113,
    UiChat = 114,

    // 캐릭터(3D)
    CookieStep = 200,
    CookieJump = 201,
    CookieLand = 202,
    CookieDodge = 203,
    CookieGrab = 204,
    CookieRelease = 205,
    CookieCarried = 206,
    MonsterStep = 207,
    MonsterDash = 208,
    MonsterGrab = 209,
    MonsterSquash = 210,
    CookieCrumble = 211,
    MonsterAimLock = 212,
    StunStars = 213,
    Respawn = 214,
    SpectateSwitch = 215,
    DoorOpen = 216,
    DoorClose = 217,

    // 색칠(2D, 본인)
    PaintStroke = 300,
    PaintPickColor = 301,
    PaintSlotRegistered = 302,
    PaintErase = 303,
    PaintReset = 304,
    PaintForceFill = 305,

    // 대기실
    CauldronBubble = 400,
    CauldronSplash = 401,
    MonsterWaitTick = 402,
    MonsterDeparted = 403,

    // 탈출 모드
    ChestOpen = 500,
    ChestRespawn = 501,
    ItemPickup = 502,
    ItemDrop = 503,
    DeviceInsert = 504,
    DeviceComplete = 505,
    BoardHop = 506,
    BoardSuck = 507,
    RocketInsert = 508,
    RocketHatch = 509,
    RocketIgnite = 510,
    RocketLiftoff = 511,
    WitchAppear = 512,
    WitchSlam = 513,
    HeartBeat = 514,
    EscapeSuccessSelf = 515,

    // 도구
    StunAimHum = 600,
    StunFire = 601,
    StunHit = 602,
    BalloonThrow = 603,
    BalloonSplash = 604,
    HammerSwing = 605,
    HammerBonk = 606,

    // 맵 연출(3D)
    CakeRumble = 700,
    CakeCrack = 701,
    CakeBurst = 702,
    CakeRocketRise = 703,
    CakeIgnite = 704,
    CakeLaunch = 705,
    CoasterBulbOn = 710,
    CoasterSparks = 711,
    CoasterStartup = 712,
    CoasterLapBar = 713,
    CoasterDepart = 714,
    OvenGears = 720,
    OvenPiston = 721,
    OvenDoorRoll = 722,
    OvenFlash = 723,
    TrainPuff = 730,
    TrainWhistle = 731,
    TrainChug = 732,
    TrainTunnel = 733,
    AltarHum = 740,
    AltarGlow = 741,
    PortalOpen = 742,
    PortalFlash = 743,
    PortalClose = 744,
    LanternFlicker = 745,

    // 환경음(반복)
    AmbCandyForest = 800,
    AmbGingerbread = 801,
    AmbFactory = 802,
    AmbCarnival = 803,
    AmbBakery = 804,
}

// 배경음·스팅어·징글 ID(SoundPlan.md §3.1). 규칙은 SoundId와 같다.
public enum MusicId
{
    None = 0,

    // 반복 곡
    Lobby = 1,
    GameLobby = 2,
    MonsterWait = 3,
    CandyForestPaint = 10,
    CandyForestHunt = 11,
    GingerbreadPaint = 12,
    GingerbreadHunt = 13,
    FactoryPaint = 14,
    FactoryHunt = 15,
    CarnivalPaint = 16,
    CarnivalHunt = 17,
    BakeryPaint = 18,
    BakeryHunt = 19,
    TimeAttack = 20,

    // 스팅어(한 번, 배경음을 잠시 낮춘다)
    StMonsterReveal = 100,
    StMonsterArrive = 101,
    StDeviceComplete = 102,
    StSpyLaunch = 103,
    StWitchAppear = 104,

    // 결과 징글
    JgEscapeSuccess = 200,
    JgEscapeFail = 201,
    JgMonsterWin = 202,
}
