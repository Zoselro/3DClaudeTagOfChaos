// 씬 전환 시 쓰이는 씬 이름 상수. GameManager/ColorTag/Lobby 세 도메인이 공통으로 참조하므로
// 특정 도메인에 두지 않고 Core/에 별도로 둔다 (architecture-review.md §4/§11.2).
public static class SceneNames
{
    public const string Lobby = "LobbyScene";
    public const string GameLobby = "GameLobbyScene";
    // 게임 씬은 판마다 GameSettings.gameMapScenes에서 고른다(예전 상수 Game = "GameScene"은 빌드에서 빠져 제거, Bug-fix-plan.md §41 ㊷).
}
