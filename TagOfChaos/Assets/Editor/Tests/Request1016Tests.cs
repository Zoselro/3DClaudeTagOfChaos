using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// 2026-10-16 요청(Request1009Plan.md §26·§27) 시험.
public class Request1016Tests
{
    // §27 K1: 겹침은 같은 묶음 안에서만 경고 — 쿠키 달리기(Shift)와 괴물 돌진(Shift)은 괜찮고, 쿠키 점프를 E로 바꾸면 상호작용과 겹친다.
    [Test]
    public void KeyBindings_ConflictsOnlyWithinTheSameGroup()
    {
        var keys = new Dictionary<KeyAction, KeyCode>();
        foreach (KeyAction a in KeyBindings.All) keys[a] = KeyBindings.Default(a);
        foreach (KeyAction a in KeyBindings.All)
            Assert.IsFalse(KeyBindings.HasConflict(a, x => keys[x]), "defaults must be clean: " + a);

        Assert.AreEqual(keys[KeyAction.Run], keys[KeyAction.MonsterDash], "same Shift, different groups");
        keys[KeyAction.Jump] = KeyCode.E;
        Assert.IsTrue(KeyBindings.HasConflict(KeyAction.Jump, x => keys[x]));
        Assert.IsTrue(KeyBindings.HasConflict(KeyAction.Interact, x => keys[x]));
        Assert.IsFalse(KeyBindings.HasConflict(KeyAction.MonsterSkill, x => keys[x]));

        keys[KeyAction.Jump] = KeyCode.Q; // 괴물 스킬과 같지만 쿠키 전용 키라 괜찮다
        Assert.IsFalse(KeyBindings.HasConflict(KeyAction.Jump, x => keys[x]));
        keys[KeyAction.SpectateNext] = KeyCode.Mouse1; // 관전에서는 카메라 회전과 겹친다
        Assert.IsTrue(KeyBindings.HasConflict(KeyAction.SpectateNext, x => keys[x]));
    }

    // §26.9 Q24: 판 중 탈출·부서진 사람의 채팅은 같은 처지에게만, 판이 끝나면 모두에게
    [TestCase(true, true, false, false)]
    [TestCase(true, true, true, true)]
    [TestCase(true, false, false, true)]
    [TestCase(true, false, true, true)]
    [TestCase(false, true, false, true)]
    public void Chat_OutPlayersTalkOnlyToEachOtherDuringTheRound(bool round, bool senderOut, bool viewerOut, bool visible)
    {
        Assert.AreEqual(visible, ChatRules.IsVisible(round, senderOut, viewerOut));
    }

    [Test]
    public void KeyBindings_EscapeIsNeverBindable_AndDefaultsMatchThePlan()
    {
        Assert.IsFalse(KeyBindings.CanBind(KeyCode.Escape));
        Assert.IsFalse(KeyBindings.CanBind(KeyCode.None));
        Assert.IsTrue(KeyBindings.CanBind(KeyCode.Mouse0));
        Assert.AreEqual(KeyCode.W, KeyBindings.Default(KeyAction.MoveForward));
        Assert.AreEqual(KeyCode.D, KeyBindings.Default(KeyAction.MoveRight));
        Assert.AreEqual(KeyCode.Mouse0, KeyBindings.Default(KeyAction.UseTool));
        Assert.AreEqual(KeyCode.Mouse1, KeyBindings.Default(KeyAction.CameraRotate));
        Assert.AreEqual(KeyCode.T, KeyBindings.Default(KeyAction.PushToTalk));
        Assert.AreEqual(KeyCode.Alpha3, KeyBindings.Default(KeyAction.Slot3));
        Assert.AreEqual("LMB", KeyBindings.DisplayName(KeyCode.Mouse0));
        Assert.AreEqual("2", KeyBindings.DisplayName(KeyCode.Alpha2));
    }
}
