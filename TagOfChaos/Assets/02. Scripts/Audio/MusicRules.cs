// 배경음 고르기 규칙(SoundPlan.md §3.1·S3). 네트워크·씬과 분리한 순수 함수라 EditMode에서 표로 시험한다.
// MusicDirector가 지금 상황(씬·단계·괴물 대기·내 결과)을 모아 여기에 묻는다.
public enum MusicScene { Other, Lobby, GameLobby, CandyForest, Gingerbread, Factory, Carnival, Bakery }

public struct MusicContext
{
    public MusicScene Scene;
    public GamePhase Phase;
    public bool LocalMonsterWaiting; // 괴물 본인이 대기실에서 쿠키 변장 시간을 기다리는 중
    public bool LocalIsMonster;
}

public static class MusicRules
{
    public const float SceneFadeSeconds = 1.5f; // 씬이 바뀌었을 때
    public const float PhaseFadeSeconds = 2.5f; // 같은 씬에서 단계가 바뀌었을 때
    public const float ResultFadeSeconds = 4f;  // 결과 징글 뒤로 로비 곡이 천천히 들어온다

    public static MusicScene SceneFromName(string sceneName)
    {
        switch (sceneName)
        {
            case SceneNames.Lobby: return MusicScene.Lobby;
            case SceneNames.GameLobby: return MusicScene.GameLobby;
            case "Game_CandyForest": return MusicScene.CandyForest;
            case "Game_GingerbreadVillage": return MusicScene.Gingerbread;
            case "Game_ChocolateFactory": return MusicScene.Factory;
            case "Game_CursedCandyCarnival": return MusicScene.Carnival;
            case "Game_HauntedBakery": return MusicScene.Bakery;
            default: return MusicScene.Other; // 시험 씬 등 — 배경음 없음
        }
    }

    public static MusicId Select(MusicContext c)
    {
        switch (c.Scene)
        {
            case MusicScene.Lobby: return MusicId.Lobby;
            case MusicScene.GameLobby: return IsMonsterWaiting(c) ? MusicId.MonsterWait : MusicId.GameLobby;
            case MusicScene.Other: return MusicId.None;
        }

        // 맵: 변장 → 추격 → 타임어택 → 결과(징글 뒤 로비 곡)
        switch (c.Phase)
        {
            case GamePhase.Hunt: return MapHunt(c.Scene);
            case GamePhase.TimeAttack: return MusicId.TimeAttack;
            case GamePhase.Result: return MusicId.Lobby;
            default: return MapPaint(c.Scene); // 판 시작 전·변장·괴물 합류 대기
        }
    }

    // 괴물 본인이 대기실에 남아 있는 판 진행 중 — 대기가 끝나 맵으로 넘어가는 동안에도 대기 곡을 이어 간다(대기실 곡으로 잠깐 돌아가지 않게).
    private static bool IsMonsterWaiting(MusicContext c) =>
        c.LocalMonsterWaiting || (c.LocalIsMonster && c.Phase != GamePhase.Lobby && c.Phase != GamePhase.Result);

    // 같은 씬 안에서 단계가 바뀐 순간 한 번 내는 스팅어. 없으면 None. 결과는 JingleFor가 따로 고른다.
    public static MusicId StingerFor(GamePhase from, GamePhase to)
    {
        if (to == GamePhase.Hunt && (from == GamePhase.Paint || from == GamePhase.AwaitingMonster)) return MusicId.StMonsterArrive;
        if (to == GamePhase.TimeAttack && from == GamePhase.Hunt) return MusicId.StSpyLaunch;
        return MusicId.None;
    }

    // 결과 징글은 "내" 결과로 고른다. survivedOrEscaped = 쿠키·스파이 본인이 살아남았거나(생존 모드) 탈출했는지(탈출 모드),
    // anyCookieEscaped = 탈출 모드에서 쿠키가 한 명이라도 탈출했는지(괴물 쪽 승패).
    public static MusicId JingleFor(GameResult result, bool localIsMonster, bool survivedOrEscaped, bool anyCookieEscaped)
    {
        if (localIsMonster)
        {
            bool monsterWon = result == GameResult.MonsterWins || (result == GameResult.EscapeEnded && !anyCookieEscaped);
            return monsterWon ? MusicId.JgMonsterWin : MusicId.JgEscapeFail;
        }
        bool won = (result == GameResult.CookiesWin || result == GameResult.EscapeEnded) && survivedOrEscaped;
        return won ? MusicId.JgEscapeSuccess : MusicId.JgEscapeFail;
    }

    private static MusicId MapPaint(MusicScene scene)
    {
        switch (scene)
        {
            case MusicScene.CandyForest: return MusicId.CandyForestPaint;
            case MusicScene.Gingerbread: return MusicId.GingerbreadPaint;
            case MusicScene.Factory: return MusicId.FactoryPaint;
            case MusicScene.Carnival: return MusicId.CarnivalPaint;
            default: return MusicId.BakeryPaint;
        }
    }

    private static MusicId MapHunt(MusicScene scene)
    {
        switch (scene)
        {
            case MusicScene.CandyForest: return MusicId.CandyForestHunt;
            case MusicScene.Gingerbread: return MusicId.GingerbreadHunt;
            case MusicScene.Factory: return MusicId.FactoryHunt;
            case MusicScene.Carnival: return MusicId.CarnivalHunt;
            default: return MusicId.BakeryHunt;
        }
    }
}
