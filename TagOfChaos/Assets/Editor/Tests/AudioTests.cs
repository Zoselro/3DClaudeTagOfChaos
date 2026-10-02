using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

// 소리 기반(SoundPlan.md §6, S1). 카탈로그 구조, 재생 칸 배정 규칙, 쿨다운, 음량 변환·저장.
public class AudioTests
{
    // ---- 카탈로그 ----

    [Test]
    public void SoundCatalog_HasOneEntryPerId()
    {
        var catalog = Resources.Load<SoundCatalogSO>(SoundCatalogSO.ResourcePath);
        Assert.NotNull(catalog, "Run Tools/TagOfChaos/Audio/Build Catalogs.");
        foreach (SoundId id in AudioCatalogBuilder.SoundIds())
            Assert.AreEqual(1, catalog.Entries.Count(e => e != null && e.id == id), $"{id} must have exactly one catalog entry.");
        Assert.IsFalse(catalog.Entries.Any(e => e == null || e.id == SoundId.None || !Enum.IsDefined(typeof(SoundId), e.id)), "Catalog has an empty or unknown entry.");
    }

    [Test]
    public void MusicCatalog_HasOneEntryPerId()
    {
        var catalog = Resources.Load<MusicCatalogSO>(MusicCatalogSO.ResourcePath);
        Assert.NotNull(catalog, "Run Tools/TagOfChaos/Audio/Build Catalogs.");
        foreach (MusicId id in AudioCatalogBuilder.MusicIds())
        {
            MusicCatalogSO.Entry[] found = catalog.Entries.Where(e => e != null && e.id == id).ToArray();
            Assert.AreEqual(1, found.Length, $"{id} must have exactly one catalog entry.");
            Assert.AreEqual(MusicCatalogSO.IsLoopById(id), found[0].loop, $"{id}: loops loop, stingers and jingles do not.");
        }
    }

    // 3D 소리는 들리는 거리가 있어야 하고, 쿠키 발소리는 괴물 발소리보다 작게 들린다(D3).
    [Test]
    public void SoundCatalog_DistancesFollowHideAndSeekRules()
    {
        var catalog = Resources.Load<SoundCatalogSO>(SoundCatalogSO.ResourcePath);
        Assert.IsTrue(catalog.TryGet(SoundId.CookieStep, out var cookie));
        Assert.IsTrue(catalog.TryGet(SoundId.MonsterStep, out var monster));
        Assert.Less(cookie.maxDistance, monster.maxDistance);
        Assert.IsTrue(catalog.TryGet(SoundId.UiClick, out var ui));
        Assert.AreEqual(SoundSpace.Flat2D, ui.space);
        Assert.AreEqual(SoundBus.Ui, ui.bus);
    }

    // 음원은 S8에서 모두 채운다. 그 전까지는 빠진 수를 알려 주기만 한다(실패 아님).
    [Test]
    public void AllIds_HaveClips()
    {
        var sounds = Resources.Load<SoundCatalogSO>(SoundCatalogSO.ResourcePath);
        var music = Resources.Load<MusicCatalogSO>(MusicCatalogSO.ResourcePath);
        string[] missing = sounds.Entries.Where(e => !e.HasClip).Select(e => e.id.ToString())
            .Concat(music.Entries.Where(e => e.clip == null).Select(e => e.id.ToString())).ToArray();
        if (missing.Length > 0)
            Assert.Inconclusive($"{missing.Length} ids have no clip yet (SoundPlan S8): {string.Join(", ", missing.Take(15))}…");
    }

    // ---- 재생 칸 배정 ----

    [Test]
    public void Pool_UsesFreeVoicesFirst()
    {
        var pool = new AudioVoicePool(2);
        Assert.AreEqual(0, pool.Acquire(SoundId.UiClick, SoundImportance.Normal, 0f, out bool s1));
        Assert.AreEqual(1, pool.Acquire(SoundId.UiClick, SoundImportance.Normal, 1f, out bool s2));
        Assert.IsFalse(s1 || s2);
        Assert.AreEqual(2, pool.CountPlaying(SoundId.UiClick));
    }

    [Test]
    public void Pool_WhenFull_StealsOldestOfLowestImportance()
    {
        var pool = new AudioVoicePool(3);
        pool.Acquire(SoundId.CookieStep, SoundImportance.Low, 0f, out _);     // 0: 낮음, 가장 오래됨
        pool.Acquire(SoundId.ChestOpen, SoundImportance.Normal, 1f, out _);   // 1
        pool.Acquire(SoundId.CookieStep, SoundImportance.Low, 2f, out _);     // 2: 낮음, 나중
        int voice = pool.Acquire(SoundId.MonsterStep, SoundImportance.High, 3f, out bool stolen);
        Assert.AreEqual(0, voice);
        Assert.IsTrue(stolen);
        Assert.AreEqual(SoundId.MonsterStep, pool.SoundAt(0));
    }

    [Test]
    public void Pool_WhenAllMoreImportant_DropsNewSound()
    {
        var pool = new AudioVoicePool(2);
        pool.Acquire(SoundId.WitchSlam, SoundImportance.Critical, 0f, out _);
        pool.Acquire(SoundId.RocketIgnite, SoundImportance.High, 0f, out _);
        Assert.AreEqual(-1, pool.Acquire(SoundId.CookieStep, SoundImportance.Normal, 1f, out _));
    }

    [Test]
    public void Pool_GenerationChanges_SoOldHandlesCannotStopNewSounds()
    {
        var pool = new AudioVoicePool(1);
        int v = pool.Acquire(SoundId.UiClick, SoundImportance.Normal, 0f, out _);
        int first = pool.GenerationOf(v);
        Assert.Greater(first, 0, "Generation 0 is reserved for empty handles.");
        pool.Release(v);
        pool.Acquire(SoundId.UiHover, SoundImportance.Normal, 1f, out _);
        Assert.AreNotEqual(first, pool.GenerationOf(v));
        Assert.IsFalse(new AudioHandle(false, 0, 0).IsValid);
    }

    [Test]
    public void Cooldown_BlocksRepeatsInsideWindow()
    {
        var cd = new SoundCooldowns();
        Assert.IsTrue(cd.TryUse(SoundId.UiClick, 0.05f, 1.00f));
        Assert.IsFalse(cd.TryUse(SoundId.UiClick, 0.05f, 1.03f));
        Assert.IsTrue(cd.TryUse(SoundId.UiHover, 0.05f, 1.03f), "Cooldown is per sound.");
        Assert.IsTrue(cd.TryUse(SoundId.UiClick, 0.05f, 1.06f));
    }

    // ---- UI 소리(S2) ----

    [TestCase(11, SoundId.None)]
    [TestCase(10, SoundId.UiTick)]
    [TestCase(4, SoundId.UiTick)]
    [TestCase(3, SoundId.UiTickFinal)]
    [TestCase(1, SoundId.UiTickFinal)]
    [TestCase(0, SoundId.None)]
    public void CountdownTick_LastTenSeconds_FinalThree(int seconds, SoundId expected)
    {
        Assert.AreEqual(expected, UiSoundCues.TickSound(seconds));
    }

    [TestCase("StartGameButton", SoundId.UiStart)]
    [TestCase("YesButton", SoundId.UiConfirm)]
    [TestCase("NoButton", SoundId.UiCancel)]
    [TestCase("Swatch7", SoundId.PaintPickColor)]
    [TestCase("Increase", SoundId.UiClick)]
    public void ButtonClickSound_ChosenByName(string name, SoundId expected)
    {
        Assert.AreEqual(expected, UiSoundInstaller.ClickFor(name));
    }

    [Test]
    public void UiPrefabs_EveryButtonHasUiSound()
    {
        foreach (string path in UiSoundInstaller.UiPrefabPaths())
        {
            var root = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            string[] missing = root.GetComponentsInChildren<UnityEngine.UI.Button>(true).Where(b => b.GetComponent<UiSound>() == null).Select(b => b.name).ToArray();
            Assert.IsEmpty(missing, $"{path}: buttons without UiSound — run Tools/TagOfChaos/Audio/Attach UI Sounds.");
        }
    }

    [Test]
    public void BuildScenes_EveryButtonHasUiSound()
    {
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) Assert.Ignore("Save the open scenes before running scene tests.");
        var setup = UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (UnityEditor.EditorBuildSettingsScene s in UnityEditor.EditorBuildSettings.scenes)
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(s.path, UnityEditor.SceneManagement.OpenSceneMode.Single);
                string[] missing = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                                        .Where(b => b.GetComponent<UiSound>() == null).Select(b => b.name).ToArray();
                Assert.IsEmpty(missing, $"{s.path}: buttons without UiSound — run Tools/TagOfChaos/Audio/Attach UI Sounds.");
            }
        }
        finally
        {
            if (setup.Length > 0) UnityEditor.SceneManagement.EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }

    // ---- 배경음 규칙(S3) ----

    [TestCase("LobbyScene", MusicScene.Lobby)]
    [TestCase("GameLobbyScene", MusicScene.GameLobby)]
    [TestCase("Game_CandyForest", MusicScene.CandyForest)]
    [TestCase("Game_GingerbreadVillage", MusicScene.Gingerbread)]
    [TestCase("Game_ChocolateFactory", MusicScene.Factory)]
    [TestCase("Game_CursedCandyCarnival", MusicScene.Carnival)]
    [TestCase("Game_HauntedBakery", MusicScene.Bakery)]
    [TestCase("PlayerTestScene", MusicScene.Other)]
    public void Music_SceneFromName(string scene, MusicScene expected)
    {
        Assert.AreEqual(expected, MusicRules.SceneFromName(scene));
    }

    // 빌드에 들어가는 맵 씬은 모두 자기 곡을 가져야 한다(새 맵을 넣고 규칙을 빠뜨리지 않게).
    [Test]
    public void Music_EveryBuildMapHasItsOwnMusic()
    {
        foreach (UnityEditor.EditorBuildSettingsScene s in UnityEditor.EditorBuildSettings.scenes)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(s.path);
            if (!name.StartsWith("Game_")) continue;
            Assert.AreNotEqual(MusicScene.Other, MusicRules.SceneFromName(name), $"{name} has no music rule.");
        }
    }

    // 씬 8종 × 단계 6종 × 괴물 대기 여부 × 내가 괴물인지 전체 표
    [Test]
    public void Music_SelectTable()
    {
        var mapPaint = new System.Collections.Generic.Dictionary<MusicScene, MusicId>
        {
            { MusicScene.CandyForest, MusicId.CandyForestPaint }, { MusicScene.Gingerbread, MusicId.GingerbreadPaint },
            { MusicScene.Factory, MusicId.FactoryPaint }, { MusicScene.Carnival, MusicId.CarnivalPaint }, { MusicScene.Bakery, MusicId.BakeryPaint },
        };
        var mapHunt = new System.Collections.Generic.Dictionary<MusicScene, MusicId>
        {
            { MusicScene.CandyForest, MusicId.CandyForestHunt }, { MusicScene.Gingerbread, MusicId.GingerbreadHunt },
            { MusicScene.Factory, MusicId.FactoryHunt }, { MusicScene.Carnival, MusicId.CarnivalHunt }, { MusicScene.Bakery, MusicId.BakeryHunt },
        };
        foreach (MusicScene scene in (MusicScene[])Enum.GetValues(typeof(MusicScene)))
        foreach (GamePhase phase in (GamePhase[])Enum.GetValues(typeof(GamePhase)))
        foreach (bool waiting in new[] { false, true })
        foreach (bool monster in new[] { false, true })
        {
            MusicId expected;
            bool monsterInGame = monster && phase != GamePhase.Lobby && phase != GamePhase.Result;
            if (scene == MusicScene.Other) expected = MusicId.None;
            else if (scene == MusicScene.Lobby) expected = MusicId.Lobby;
            else if (scene == MusicScene.GameLobby) expected = waiting || monsterInGame ? MusicId.MonsterWait : MusicId.GameLobby;
            else if (phase == GamePhase.Hunt) expected = mapHunt[scene];
            else if (phase == GamePhase.TimeAttack) expected = MusicId.TimeAttack;
            else if (phase == GamePhase.Result) expected = MusicId.Lobby;
            else expected = mapPaint[scene];
            var c = new MusicContext { Scene = scene, Phase = phase, LocalMonsterWaiting = waiting, LocalIsMonster = monster };
            Assert.AreEqual(expected, MusicRules.Select(c), $"{scene} {phase} waiting={waiting} monster={monster}");
        }
    }

    // 같은 맵의 변장 곡과 추격 곡은 짝(교차 전환이 매끄럽게 같은 조성·템포로 만든다 — §3.1).
    [Test]
    public void Music_PaintAndHuntArePairs()
    {
        foreach (MusicScene scene in new[] { MusicScene.CandyForest, MusicScene.Gingerbread, MusicScene.Factory, MusicScene.Carnival, MusicScene.Bakery })
        {
            MusicId paint = MusicRules.Select(new MusicContext { Scene = scene, Phase = GamePhase.Paint });
            MusicId hunt = MusicRules.Select(new MusicContext { Scene = scene, Phase = GamePhase.Hunt });
            Assert.AreEqual(paint.ToString().Replace("Paint", ""), hunt.ToString().Replace("Hunt", ""));
        }
    }

    [TestCase(GamePhase.Paint, GamePhase.Hunt, MusicId.StMonsterArrive)]
    [TestCase(GamePhase.AwaitingMonster, GamePhase.Hunt, MusicId.StMonsterArrive)]
    [TestCase(GamePhase.Hunt, GamePhase.TimeAttack, MusicId.StSpyLaunch)]
    [TestCase(GamePhase.Lobby, GamePhase.Paint, MusicId.None)]
    [TestCase(GamePhase.Paint, GamePhase.AwaitingMonster, MusicId.None)]
    [TestCase(GamePhase.Hunt, GamePhase.Result, MusicId.None)]
    public void Music_StingerOnPhaseChange(GamePhase from, GamePhase to, MusicId expected)
    {
        Assert.AreEqual(expected, MusicRules.StingerFor(from, to));
    }

    // 결과 징글: 내 역할·결과로 고른다.
    [TestCase(GameResult.CookiesWin, false, true, false, MusicId.JgEscapeSuccess)]
    [TestCase(GameResult.CookiesWin, false, false, false, MusicId.JgEscapeFail)]   // 이겼지만 나는 부서짐
    [TestCase(GameResult.MonsterWins, false, false, false, MusicId.JgEscapeFail)]
    [TestCase(GameResult.EscapeEnded, false, true, true, MusicId.JgEscapeSuccess)]
    [TestCase(GameResult.EscapeEnded, false, false, true, MusicId.JgEscapeFail)]
    [TestCase(GameResult.MonsterWins, true, false, false, MusicId.JgMonsterWin)]
    [TestCase(GameResult.CookiesWin, true, false, false, MusicId.JgEscapeFail)]
    [TestCase(GameResult.EscapeEnded, true, false, false, MusicId.JgMonsterWin)]  // 아무도 못 나감
    [TestCase(GameResult.EscapeEnded, true, false, true, MusicId.JgEscapeFail)]   // 쿠키가 탈출함
    public void Music_JingleByLocalOutcome(GameResult result, bool monster, bool survived, bool anyEscaped, MusicId expected)
    {
        Assert.AreEqual(expected, MusicRules.JingleFor(result, monster, survived, anyEscaped));
    }

    // ---- 음량 ----

    [Test]
    public void Volume_DecibelConversion_RoundTrips()
    {
        Assert.AreEqual(0f, AudioVolumeSettings.ToDecibels(1f), 1e-4f);
        Assert.AreEqual(-6.0206f, AudioVolumeSettings.ToDecibels(0.5f), 1e-3f);
        Assert.AreEqual(AudioVolumeSettings.SilentDb, AudioVolumeSettings.ToDecibels(0f));
        Assert.AreEqual(0f, AudioVolumeSettings.FromDecibels(AudioVolumeSettings.SilentDb));
        foreach (float v in new[] { 0.1f, 0.33f, 0.8f })
            Assert.AreEqual(v, AudioVolumeSettings.FromDecibels(AudioVolumeSettings.ToDecibels(v)), 1e-4f);
    }

    [Test]
    public void Volume_GainMultipliesSlidersAndBusBase_AndPersists()
    {
        var all = (AudioVolumeSettings.Slider[])Enum.GetValues(typeof(AudioVolumeSettings.Slider));
        float[] saved = all.Select(AudioVolumeSettings.Get).ToArray();
        try
        {
            AudioVolumeSettings.Set(AudioVolumeSettings.Slider.Master, 0.5f);
            AudioVolumeSettings.Set(AudioVolumeSettings.Slider.Music, 0.5f);
            AudioVolumeSettings.Set(AudioVolumeSettings.Slider.Sfx, 1f);
            float musicBase = AudioVolumeSettings.FromDecibels(AudioVolumeSettings.BaseDb(SoundBus.Music));
            Assert.AreEqual(0.25f * musicBase, AudioVolumeSettings.Gain(SoundBus.Music), 1e-4f);
            // 환경음은 효과음 막대를 따른다
            float ambBase = AudioVolumeSettings.FromDecibels(AudioVolumeSettings.BaseDb(SoundBus.Ambience));
            Assert.AreEqual(0.5f * ambBase, AudioVolumeSettings.Gain(SoundBus.Ambience), 1e-4f);

            AudioVolumeSettings.Reload(); // 기기에 저장된 값을 다시 읽어도 같다
            Assert.AreEqual(0.5f, AudioVolumeSettings.Get(AudioVolumeSettings.Slider.Music), 1e-4f);

            AudioVolumeSettings.Set(AudioVolumeSettings.Slider.Ui, 2f);
            Assert.AreEqual(1f, AudioVolumeSettings.Get(AudioVolumeSettings.Slider.Ui), "Sliders clamp to 0..1.");
        }
        finally
        {
            for (int i = 0; i < all.Length; i++) AudioVolumeSettings.Set(all[i], saved[i]);
        }
    }
}
