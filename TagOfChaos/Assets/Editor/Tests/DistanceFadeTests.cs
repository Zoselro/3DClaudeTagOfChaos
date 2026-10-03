using NUnit.Framework;
using UnityEngine;

// 거리 기반 음량 감쇠와 술래 접근 추격음(DistanceFadePlan.md §6·§9.4).
public class DistanceFadeTests
{
    // ---- 감쇠 곡선 ----

    [Test]
    public void Falloff_IsFullInsideMin_ZeroBeyondMax_AndOnlyDecreases()
    {
        foreach (SoundFalloff f in (SoundFalloff[])System.Enum.GetValues(typeof(SoundFalloff)))
        {
            Assert.AreEqual(1f, SpatialGain.Distance(f, 1f, 2f, 20f), 1e-5f, $"{f}: inside min distance is full volume.");
            Assert.AreEqual(0f, SpatialGain.Distance(f, 20f, 2f, 20f), 1e-5f, $"{f}: at max distance it is silent.");
            Assert.AreEqual(0f, SpatialGain.Distance(f, 30f, 2f, 20f), 1e-5f, $"{f}: beyond max distance it is silent.");
            float previous = 1f;
            for (float d = 2f; d <= 20f; d += 0.25f)
            {
                float g = SpatialGain.Distance(f, d, 2f, 20f);
                Assert.LessOrEqual(g, previous + 1e-5f, $"{f}: gain must not rise with distance ({d} m).");
                previous = g;
            }
        }
    }

    // 예전 공통 곡선에서는 12 m 발소리가 3 m에서 0.33이라 가까이 있어도 잘 안 들렸다(§1.2).
    [Test]
    public void Footstep_IsStillClearAtThreeMeters()
    {
        Assert.GreaterOrEqual(SpatialGain.Distance(SoundFalloff.Footstep, 3f, 1.5f, 12f), 0.5f);
        // 괴물 발소리는 최대 거리의 절반에서도 존재감이 있다
        Assert.GreaterOrEqual(SpatialGain.Distance(SoundFalloff.Heavy, 15f, 3f, 30f), 0.3f);
    }

    [Test]
    public void Floor_SameFloorUnchanged_OtherFloorMuchQuieter()
    {
        Assert.AreEqual(1f, SpatialGain.Floor(3f), 1e-5f);
        Assert.AreEqual(1f, SpatialGain.Floor(-4f), 1e-5f);
        Assert.AreEqual(SpatialGain.OtherFloorGain, SpatialGain.Floor(8f), 1e-5f);
        Assert.AreEqual(SpatialGain.OtherFloorGain, SpatialGain.Floor(-12f), 1e-5f);
        float mid = SpatialGain.Floor(5.5f);
        Assert.Less(mid, 1f); Assert.Greater(mid, SpatialGain.OtherFloorGain);
    }

    [Test]
    public void Cutoff_OpenNear_MuffledFar_AndThroughFloors()
    {
        Vector3 ear = Vector3.zero;
        Assert.AreEqual(SpatialGain.OpenCutoffHz, SpatialGain.Cutoff(new Vector3(1f, 0f, 0f), ear, 2f, 20f), 1f);
        Assert.AreEqual(SpatialGain.FarCutoffHz, SpatialGain.Cutoff(new Vector3(20f, 0f, 0f), ear, 2f, 20f), 1f);
        Assert.LessOrEqual(SpatialGain.Cutoff(new Vector3(1f, 9f, 0f), ear, 2f, 20f), SpatialGain.OtherFloorCutoffHz + 1f);
    }

    // ---- 재생 칸 ----

    [Test]
    public void Pool_WhenFull_StealsQuietestOfSameImportance_AndKeepsLouderSounds()
    {
        var pool = new AudioVoicePool(3);
        pool.Acquire(SoundId.CookieStep, SoundImportance.Low, 0f, 0.9f, out _);
        pool.Acquire(SoundId.CookieStep, SoundImportance.Low, 1f, 0.1f, out _);   // 멀리서 나는 발소리
        pool.Acquire(SoundId.CookieStep, SoundImportance.Low, 2f, 0.6f, out _);

        int v = pool.Acquire(SoundId.CookieLand, SoundImportance.Low, 3f, 0.5f, out bool stolen);
        Assert.AreEqual(1, v, "The quietest voice is stolen, not the oldest.");
        Assert.IsTrue(stolen);

        Assert.AreEqual(-1, pool.Acquire(SoundId.CookieLand, SoundImportance.Low, 4f, 0.05f, out _),
            "A new sound quieter than every same-importance voice is dropped.");
        Assert.AreNotEqual(-1, pool.Acquire(SoundId.StunHit, SoundImportance.Normal, 5f, 0.05f, out _),
            "A more important sound still takes a less important voice.");
    }

    [Test]
    public void Pool_OldOverload_StillStealsOldest()
    {
        var pool = new AudioVoicePool(2);
        pool.Acquire(SoundId.UiClick, SoundImportance.Normal, 0f, out _);
        pool.Acquire(SoundId.UiClick, SoundImportance.Normal, 1f, out _);
        Assert.AreEqual(0, pool.Acquire(SoundId.UiClick, SoundImportance.Normal, 2f, out _));
    }

    // ---- 환경음 ----

    [Test]
    public void AmbientEmitter_StartsInsideRange_StopsWithMargin()
    {
        const float max = 28f;
        Assert.IsTrue(AmbientEmitter.ShouldPlay(false, max + AmbientEmitter.StartMargin - 0.1f, max));
        Assert.IsFalse(AmbientEmitter.ShouldPlay(false, max + AmbientEmitter.StartMargin + 0.1f, max));
        Assert.IsTrue(AmbientEmitter.ShouldPlay(true, max + AmbientEmitter.StopMargin - 0.1f, max), "Keeps playing a little beyond the start range (no flicker).");
        Assert.IsFalse(AmbientEmitter.ShouldPlay(true, max + AmbientEmitter.StopMargin + 0.1f, max));
    }

    // ---- 카탈로그 ----

    [Test]
    public void Catalog_World3DEntries_HaveValidRanges_AndPlannedDefaults()
    {
        var catalog = Resources.Load<SoundCatalogSO>(SoundCatalogSO.ResourcePath);
        Assert.NotNull(catalog);
        foreach (SoundCatalogSO.Entry e in catalog.Entries)
            if (e != null && e.space == SoundSpace.World3D)
                Assert.Less(e.minDistance, e.maxDistance, $"{e.id}: min distance must be inside max distance.");

        Expect(catalog, SoundId.CookieStep, SoundFalloff.Footstep, 1.5f, 12f);
        Expect(catalog, SoundId.MonsterStep, SoundFalloff.Heavy, 3f, 30f);
        Expect(catalog, SoundId.StunFire, SoundFalloff.Action, 2f, 25f);
        Expect(catalog, SoundId.BalloonSplash, SoundFalloff.Action, 2f, 22f);
        Expect(catalog, SoundId.HammerBonk, SoundFalloff.Action, 2f, 20f);
        Expect(catalog, SoundId.CarouselSpin, SoundFalloff.Landmark, 8f, 40f);
    }

    private static void Expect(SoundCatalogSO catalog, SoundId id, SoundFalloff falloff, float min, float max)
    {
        Assert.IsTrue(catalog.TryGet(id, out SoundCatalogSO.Entry e), id.ToString());
        Assert.AreEqual(falloff, e.falloff, $"{id} falloff");
        Assert.AreEqual(min, e.minDistance, 1e-4f, $"{id} min");
        Assert.AreEqual(max, e.maxDistance, 1e-4f, $"{id} max");
    }

    // ---- 추격음 ----

    private static ChaseAudioSettingsSO Settings(float radius)
    {
        var s = ScriptableObject.CreateInstance<ChaseAudioSettingsSO>();
        s.EditorSetup(null, new[]
        {
            new ChaseAudioSettingsSO.DrumLevel { startRatio = 0.25f }, // 일부러 거꾸로 — 저장할 때 정렬된다
            new ChaseAudioSettingsSO.DrumLevel { startRatio = 1f },
        });
        s.EditorSetRadius(radius);
        return s;
    }

    [Test]
    public void Chase_Intensity_ZeroOutside_FullInside_RisesAsMonsterApproaches()
    {
        ChaseAudioSettingsSO s = Settings(20f);
        try
        {
            Assert.AreEqual(0f, ChaseIntensity.Evaluate(20f, 0f, s), 1e-5f);
            Assert.AreEqual(0f, ChaseIntensity.Evaluate(35f, 0f, s), 1e-5f);
            Assert.AreEqual(1f, ChaseIntensity.Evaluate(5f, 0f, s), 1e-5f);
            Assert.AreEqual(1f, ChaseIntensity.Evaluate(1f, 0f, s), 1e-5f);
            float previous = 0f;
            for (float d = 20f; d >= 5f; d -= 0.5f)
            {
                float g = ChaseIntensity.Evaluate(d, 0f, s);
                Assert.GreaterOrEqual(g, previous - 1e-5f, $"Intensity must not drop as the monster gets closer ({d} m).");
                previous = g;
            }
            Assert.AreEqual(s.OtherFloorGain, ChaseIntensity.Evaluate(3f, 8f, s), 1e-5f, "Monster on another floor is much quieter.");
        }
        finally { Object.DestroyImmediate(s); }
    }

    [Test]
    public void Chase_Levels_FollowRatios_WithHysteresis_AndScaleWithRadius()
    {
        ChaseAudioSettingsSO s = Settings(20f);
        try
        {
            Assert.AreEqual(20f, s.LevelRadius(0), 1e-4f, "Levels are sorted far → near.");
            Assert.AreEqual(5f, s.LevelRadius(1), 1e-4f);
            Assert.AreEqual(-1, ChaseIntensity.Level(21f, -1, s));
            Assert.AreEqual(0, ChaseIntensity.Level(15f, -1, s));
            Assert.AreEqual(1, ChaseIntensity.Level(4.9f, 0, s), "Goes up right at the boundary.");
            Assert.AreEqual(1, ChaseIntensity.Level(5.5f, 1, s), "Stays near until 1 m beyond the boundary.");
            Assert.AreEqual(0, ChaseIntensity.Level(6.1f, 1, s));
            Assert.AreEqual(-1, ChaseIntensity.Level(25f, 1, s));

            s.EditorSetRadius(10f); // audibleRadius 하나만 바꾸면 전체가 같은 비율로
            Assert.AreEqual(10f, s.LevelRadius(0), 1e-4f);
            Assert.AreEqual(2.5f, s.LevelRadius(1), 1e-4f);
            Assert.AreEqual(2.5f, s.FullRadius, 1e-4f);
            Assert.AreEqual(0.5f, s.LevelHysteresis, 1e-4f);
            Assert.AreEqual(0f, ChaseIntensity.Evaluate(12f, 0f, s), 1e-5f, "12 m is outside a 10 m chase radius.");
        }
        finally { Object.DestroyImmediate(s); }
    }

    [Test]
    public void Chase_OnlyForMyCookie_DuringHunt()
    {
        var ok = new ChaseContext { Phase = GamePhase.Hunt, LocalHasCookie = true };
        Assert.IsTrue(ChaseIntensity.CanListen(ok));
        Assert.IsTrue(ChaseIntensity.CanListen(With(ok, c => c.Phase = GamePhase.TimeAttack)));
        Assert.IsFalse(ChaseIntensity.CanListen(With(ok, c => c.Phase = GamePhase.Paint)), "Not while disguising.");
        Assert.IsFalse(ChaseIntensity.CanListen(With(ok, c => c.Phase = GamePhase.Lobby)));
        Assert.IsFalse(ChaseIntensity.CanListen(With(ok, c => c.Phase = GamePhase.Result)));
        Assert.IsFalse(ChaseIntensity.CanListen(With(ok, c => c.LocalIsMonster = true)), "The monster never hears it.");
        Assert.IsFalse(ChaseIntensity.CanListen(With(ok, c => c.LocalBroken = true)), "Not while spectating (C3).");
        Assert.IsFalse(ChaseIntensity.CanListen(With(ok, c => c.LocalLeftMap = true)), "Not after escaping or boarding.");
        Assert.IsFalse(ChaseIntensity.CanListen(With(ok, c => c.LocalHasCookie = false)));
    }

    private static ChaseContext With(ChaseContext c, System.Action<ChaseContextBox> change)
    {
        var box = new ChaseContextBox { Value = c };
        change(box);
        return box.Value;
    }

    private class ChaseContextBox
    {
        public ChaseContext Value;
        public GamePhase Phase { set => Value.Phase = value; }
        public bool LocalIsMonster { set => Value.LocalIsMonster = value; }
        public bool LocalHasCookie { set => Value.LocalHasCookie = value; }
        public bool LocalBroken { set => Value.LocalBroken = value; }
        public bool LocalLeftMap { set => Value.LocalLeftMap = value; }
    }

    [Test]
    public void ChaseSettingsAsset_HasLayersOfEqualLength()
    {
        var s = Resources.Load<ChaseAudioSettingsSO>(ChaseAudioSettingsSO.ResourcePath);
        Assert.NotNull(s, "Run Tools/TagOfChaos/Audio/Build Chase Settings.");
        Assert.NotNull(s.BaseLayer, "Base layer clip.");
        Assert.GreaterOrEqual(s.LevelCount, 1);
        for (int i = 0; i < s.LevelCount; i++)
        {
            Assert.NotNull(s.LevelClip(i), $"Level {i} clip.");
            Assert.AreEqual(s.BaseLayer.samples, s.LevelClip(i).samples, $"Level {i} must be as long as the base so the beat stays aligned.");
            if (i > 0) Assert.Less(s.LevelRadius(i), s.LevelRadius(i - 1), "Levels go far → near.");
        }
        Assert.LessOrEqual(s.LevelRadius(0), s.AudibleRadius + 1e-4f);
    }
}
