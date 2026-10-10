// 관전 대상 규칙(Request1009Plan.md §9, 결정 Q1). 네트워크·씬과 분리한 순수 함수라 EditMode에서 표로 시험한다.
//   탈출한 쿠키(탈출구·장치 탈것 탑승/대기, 스파이 로켓으로 탈출한 스파이 포함) → 남은 일반 쿠키만(스파이·괴물 제외)
//   부서진 쿠키(괴물에게 잡힘·마녀)                                             → 남은 쿠키 전부(스파이 포함), 괴물은 설정이 허락할 때만
public enum SpectatorKind
{
    Escaped,
    Broken,
}

public static class SpectateRules
{
    public static bool IsCandidate(SpectatorKind viewer, CharacterRole role, bool targetIsSpy, bool spectatable, bool allowMonsters)
    {
        if (!spectatable) return false;
        switch (role)
        {
            case CharacterRole.Cookie: return viewer == SpectatorKind.Broken || !targetIsSpy;
            case CharacterRole.Monster: return allowMonsters && viewer == SpectatorKind.Broken;
            default: return false;
        }
    }

    // 이미 관전 중에 다른 이유가 또 들어오면 부서짐이 이긴다 — 탈것에서 출발을 기다리다 마녀에게 죽은 쿠키(EscapeCharacterState는 탈출 완료만 마녀에서 뺀다).
    public static SpectatorKind Merge(SpectatorKind current, SpectatorKind incoming) =>
        current == SpectatorKind.Broken || incoming == SpectatorKind.Broken ? SpectatorKind.Broken : SpectatorKind.Escaped;
}
