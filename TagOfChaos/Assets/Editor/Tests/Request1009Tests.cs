using NUnit.Framework;
using UnityEngine;

// 2026-10-09 요청(Plan.md/Request1009Plan.md) 회귀 방지 — 순수 규칙만(씬이 필요한 검사는 TwistedTests·MapCompactTests).
public class Request1009Tests
{
    private static readonly object Chest = new object(), Other = new object();

    [Test]
    public void Hold_CompletesAfterFullTime()
    {
        var h = new HoldProgress();
        h.Begin(Chest, 2f, 10f);
        Assert.AreEqual(HoldProgress.Result.Holding, h.Tick(Chest, true, true, 11f));
        Assert.AreEqual(0.5f, h.Progress, 1e-4f);
        Assert.AreEqual(HoldProgress.Result.Completed, h.Tick(Chest, true, true, 12.01f));
        Assert.IsFalse(h.Active, "Completes once, then resets");
        Assert.AreEqual(HoldProgress.Result.Idle, h.Tick(Chest, true, true, 13f));
    }

    [Test]
    public void Hold_CancelsOnReleaseTargetChangeOrUnusable()
    {
        var h = new HoldProgress();
        h.Begin(Chest, 2f, 0f);
        Assert.AreEqual(HoldProgress.Result.Canceled, h.Tick(Chest, false, true, 1f), "released");
        h.Begin(Chest, 2f, 0f);
        Assert.AreEqual(HoldProgress.Result.Canceled, h.Tick(Other, true, true, 1f), "focus moved to another object");
        h.Begin(Chest, 2f, 0f);
        Assert.AreEqual(HoldProgress.Result.Canceled, h.Tick(Chest, true, false, 1f), "someone else opened it / caught");
        Assert.AreEqual(0f, h.Progress);
    }

    // §9 관전 대상 표(결정 Q1): 탈출 = 일반 쿠키만, 부서짐 = 스파이 포함, 괴물은 부서진 쿠키 + 설정이 허락할 때만
    [TestCase(SpectatorKind.Escaped, CharacterRole.Cookie, false, true)]
    [TestCase(SpectatorKind.Escaped, CharacterRole.Cookie, true, false)]
    [TestCase(SpectatorKind.Escaped, CharacterRole.Monster, false, false)]
    [TestCase(SpectatorKind.Broken, CharacterRole.Cookie, false, true)]
    [TestCase(SpectatorKind.Broken, CharacterRole.Cookie, true, true)]
    [TestCase(SpectatorKind.Broken, CharacterRole.Monster, false, false)]
    public void Spectate_CandidateTable(SpectatorKind viewer, CharacterRole role, bool spy, bool expected)
    {
        Assert.AreEqual(expected, SpectateRules.IsCandidate(viewer, role, spy, true, false));
    }

    [Test]
    public void Spectate_MonstersOnlyForBrokenWhenAllowed_AndNeverHiddenTargets()
    {
        Assert.IsTrue(SpectateRules.IsCandidate(SpectatorKind.Broken, CharacterRole.Monster, false, true, true));
        Assert.IsFalse(SpectateRules.IsCandidate(SpectatorKind.Escaped, CharacterRole.Monster, false, true, true));
        Assert.IsFalse(SpectateRules.IsCandidate(SpectatorKind.Broken, CharacterRole.Cookie, false, false, true), "escaped/hidden targets are never shown");
    }

    [Test]
    public void Spectate_BrokenWinsOverEscaped()
    {
        Assert.AreEqual(SpectatorKind.Broken, SpectateRules.Merge(SpectatorKind.Escaped, SpectatorKind.Broken), "killed by the witch while waiting in the ride");
        Assert.AreEqual(SpectatorKind.Broken, SpectateRules.Merge(SpectatorKind.Broken, SpectatorKind.Escaped));
        Assert.AreEqual(SpectatorKind.Escaped, SpectateRules.Merge(SpectatorKind.Escaped, SpectatorKind.Escaped));
    }

    // §11 잡기 거리: 수평은 몸 중심에서 reach 안, 높이는 MaxGrabHeight까지(아래쪽은 제한 없음)
    [Test]
    public void Grab_ReachIsHorizontalWithHeightCap()
    {
        Vector3 m = Vector3.zero;
        Assert.IsTrue(MonsterGrabKillTrigger.InReach(m, new Vector3(3.5f, 1.7f, 0f), 3.87f, 6f), "cookie on a 1.7 m counter in front");
        Assert.IsTrue(MonsterGrabKillTrigger.InReach(m, new Vector3(0f, 5.9f, 3f), 3.87f, 6f));
        Assert.IsFalse(MonsterGrabKillTrigger.InReach(m, new Vector3(0f, 6.1f, 3f), 3.87f, 6f), "too high");
        Assert.IsFalse(MonsterGrabKillTrigger.InReach(m, new Vector3(4f, 0f, 0f), 3.87f, 6f), "too far");
        Assert.IsTrue(MonsterGrabKillTrigger.InReach(m, new Vector3(2f, -3f, 0f), 3.87f, 6f), "below the monster (stairs) is not capped");
    }
}

