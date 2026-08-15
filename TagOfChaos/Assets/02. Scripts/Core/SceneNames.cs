// 씬 전환 시 쓰이는 씬 이름 상수. GameManager/ColorTag/Lobby 세 도메인이 공통으로 참조하므로
// 특정 도메인에 두지 않고 Core/에 별도로 둔다 (architecture-review.md §4/§11.2).
public static class SceneNames
{
    public const string Lobby = "LobbyScene";
    public const string GameLobby = "GameLobbyScene";
    public const string Game = "GameScene";
}
