using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 2026-10-11 추가 요청 8건(Request1009Plan.md §16) 시험.
public class Request1011Tests
{
    // ---- §16.1 상자 ----

    [TestCase(GamePhase.Lobby, true)]
    [TestCase(GamePhase.Paint, false)]
    [TestCase(GamePhase.AwaitingMonster, true)]
    [TestCase(GamePhase.Hunt, true)]
    [TestCase(GamePhase.TimeAttack, true)]
    [TestCase(GamePhase.Result, true)]
    public void Chests_HiddenOnlyWhileDisguising(GamePhase phase, bool visible)
    {
        Assert.AreEqual(visible, EscapeManager.ChestsVisibleIn(phase));
    }

    [Test]
    public void ChestAppear_GrowsOvershootsAndSettles()
    {
        Assert.AreEqual(0f, MaterialChest.AppearScale(0f), 1e-4f);
        Assert.AreEqual(1.08f, MaterialChest.AppearScale(0.7f), 1e-4f);
        Assert.AreEqual(1f, MaterialChest.AppearScale(1f), 1e-4f);
        Assert.Less(MaterialChest.AppearScale(0.3f), MaterialChest.AppearScale(0.6f));
    }

    // ---- §16.2 아이콘 ----

    [Test]
    public void EveryItem_HasBakedIcon()
    {
        string[] guids = AssetDatabase.FindAssets("t:ItemSO");
        Assert.Greater(guids.Length, 0);
        foreach (string guid in guids)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemSO>(AssetDatabase.GUIDToAssetPath(guid));
            Assert.NotNull(item.Icon, $"{item.ItemId}: run Tools/Escape/Bake Item Icons");
            Assert.AreEqual(ItemIconBaker.IconPath(item), AssetDatabase.GetAssetPath(item.Icon));
            Assert.AreEqual(256, item.Icon.texture.width, item.ItemId);
        }
    }

    // ---- §16.3 쿠키 들기 ----

    [Test]
    public void CarriedBody_LiesAcrossCarrierArms()
    {
        Quaternion carrier = Quaternion.Euler(0f, 37f, 0f);
        Quaternion body = CarryPose.CarriedRotation(carrier);
        Assert.Greater(Vector3.Dot(body * Vector3.up, carrier * Vector3.left), 0.99f, "head toward carrier's left");
        Assert.Greater(Vector3.Dot(body * Vector3.forward, Vector3.up), 0.85f, "face looks up");
        Assert.Greater(Vector3.Dot(body * Vector3.forward, carrier * Vector3.back), 0f, "face turns slightly toward carrier");
    }

    [Test]
    public void CarriedRoot_PutsHipsAboveHands()
    {
        Quaternion carrier = Quaternion.Euler(0f, 90f, 0f);
        Vector3 hands = new Vector3(3f, 1.05f, -2f);
        Vector3 hips = CarryPose.CarriedHips(hands, carrier);
        Assert.AreEqual(hands.y + CarryPose.CarriedLift, hips.y, 1e-4f);
        Assert.AreEqual(CarryPose.CarriedForward, Vector3.Dot(hips - hands, carrier * Vector3.forward), 1e-4f);

        Quaternion rot = CarryPose.CarriedRotation(carrier);
        Vector3 hipsLocal = new Vector3(0f, 0.81f, 0f);
        Vector3 root = CarryPose.RootFor(hips, rot, hipsLocal);
        Assert.That((root + rot * hipsLocal - hips).magnitude, Is.LessThan(1e-4f));
    }

    [Test]
    public void ReleasedBody_StandsFacingCarrier()
    {
        Quaternion q = CarryPose.UprightFacing(Vector3.zero, new Vector3(0f, 2f, -5f));
        Assert.Greater(Vector3.Dot(q * Vector3.up, Vector3.up), 0.999f);
        Assert.Greater(Vector3.Dot(q * Vector3.forward, Vector3.back), 0.999f);
    }

    [Test]
    public void CarriedState_IsAppendedAndWiredInAnimator()
    {
        Assert.AreEqual(7, (int)PlayerMoveState.Carried, "append only — older values are sent over the network");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CookieAnimatorSetup.ControllerPath);
        AnimatorState state = controller.layers[0].stateMachine.states.Select(s => s.state).FirstOrDefault(s => s.name == CookieAnimatorSetup.CarriedState);
        Assert.NotNull(state, "run Tools/Cookie/Setup Carry Animator States");
        Assert.AreEqual("Cookie_Hanging_Idle", state.motion.name);
        Assert.IsTrue(controller.parameters.Any(p => p.name == CookieAnimatorSetup.CarriedState && p.type == AnimatorControllerParameterType.Trigger));
    }

    [Test]
    public void NetworkSync_DefaultsToNotCarried()
    {
        Assert.AreEqual(-1, new PlayerNetworkSync().RemoteCarrierViewId);
    }

    // ---- §16.4 아이템 들기 ----

    [Test]
    public void CarryPose_StaysOnWhileAnyReasonRemains()
    {
        var driver = new PlayerAnimationDriver(null);
        driver.SetCarry(CarryReason.Cookie, true);
        driver.SetCarry(CarryReason.TwoHandedItem, true);
        driver.SetCarry(CarryReason.Cookie, false);
        Assert.IsTrue(driver.IsCarryPoseOn);
        driver.SetCarry(CarryReason.TwoHandedItem, false);
        Assert.IsFalse(driver.IsCarryPoseOn);
    }

    [Test]
    public void OneHandedGrip_HoldsNearTheBottom()
    {
        // 망치: 가운데 0.04, 반높이 0.285 → 아래 끝(-0.245)에서 0.12 위(-0.125)가 손에 오도록 0.125 올린다.
        Assert.AreEqual(0.125f, HeldItemPresenter.GripLift(0.04f, 0.285f), 1e-4f);
        Assert.AreEqual(0f, HeldItemPresenter.GripLift(0f, 0.12f), 1e-4f);
    }

    // ---- §16.6 출력 장치 ----

    [Test]
    public void OutputProfiles_HeadphonesKeepOldSound()
    {
        OutputProfile phones = OutputProfile.For(OutputMode.Headphones);
        Assert.AreEqual(0f, phones.Spread); Assert.AreEqual(0f, phones.LowCutHz); Assert.AreEqual(0f, phones.SfxDb);
        Assert.Greater(OutputProfile.For(OutputMode.Speakers).Spread, OutputProfile.For(OutputMode.Headset).Spread);
        Assert.Greater(OutputProfile.For(OutputMode.Headset).LowCutHz, 0f);
        Assert.AreEqual(OutputMode.Headphones, AudioVolumeSettings.DefaultOutputMode);
    }

    [Test]
    public void OutputMode_IsSavedAndChangesSfxGain()
    {
        OutputMode before = AudioVolumeSettings.Output;
        try
        {
            AudioVolumeSettings.SetOutput(OutputMode.Headphones);
            float phones = AudioVolumeSettings.Gain(SoundBus.Sfx);
            float music = AudioVolumeSettings.Gain(SoundBus.Music);
            AudioVolumeSettings.SetOutput(OutputMode.Headset);
            AudioVolumeSettings.Reload();
            Assert.AreEqual(OutputMode.Headset, AudioVolumeSettings.Output);
            Assert.AreEqual(phones * AudioVolumeSettings.FromDecibels(-1f), AudioVolumeSettings.Gain(SoundBus.Sfx), 1e-4f);
            Assert.AreEqual(music, AudioVolumeSettings.Gain(SoundBus.Music), 1e-6f); // 배경음은 그대로
        }
        finally { AudioVolumeSettings.SetOutput(before); }
    }

    [Test]
    public void SoundPanel_HasThreeOutputButtons()
    {
        var panel = AssetDatabase.LoadAssetAtPath<SoundSettingsPanel>(SoundSettingsInstaller.PrefabPath);
        Assert.AreEqual(3, panel.OutputButtonCount);
        Assert.NotNull(panel.transform.Find("Box/" + SoundSettingsInstaller.OutputRowName));
    }

    // ---- §16.7 베이커리 고장 소리 ----

    [Test]
    public void BrokenOven_HasClipInCatalog()
    {
        Assert.AreEqual(724, (int)SoundId.OvenBroken);
        var catalog = AssetDatabase.LoadAssetAtPath<SoundCatalogSO>("Assets/Resources/Audio/SoundCatalog.asset");
        Assert.IsTrue(catalog.TryGet(SoundId.OvenBroken, out SoundCatalogSO.Entry e));
        Assert.Greater(e.clips.Length, 0, "run Tools/TagOfChaos/Audio/Build Catalogs");
        Assert.Greater(e.clips[0].length, 7.9f);
    }
}
