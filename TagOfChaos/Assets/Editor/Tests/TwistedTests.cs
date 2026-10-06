using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

// 뒤틀린 과자 동화 개편(TwistedCandyPlan.md V1~V4) 회귀 방지: 분위기 값, 소품 배치·충돌체, 숨을 곳 출입(괴물 기준 출입구 2곳), 실내 소리 영역.
public class TwistedTests
{
    private static readonly string[] Maps = MapSceneBuilder.MapNames;
    private static readonly string[] ShelterMaps = { "GingerbreadVillage", "CursedCandyCarnival" };
    private SceneSetup[] savedSetup;

    [OneTimeSetUp]
    public void SaveSetup()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) Assert.Ignore("Save the open scenes before running map tests.");
        savedSetup = EditorSceneManager.GetSceneManagerSetup();
    }

    [OneTimeTearDown]
    public void RestoreSetup()
    {
        if (savedSetup != null && savedSetup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(savedSetup);
    }

    private static void Open(string map) => EditorSceneManager.OpenScene(MapSceneBuilder.ScenePath(map), OpenSceneMode.Single);

    // ---- V1 분위기 ----

    [TestCaseSource(nameof(Maps))]
    public void Map_HasTwistedAtmosphere(string map)
    {
        Open(map);
        TwistedAtmosphere.Look look = TwistedAtmosphere.Maps[map];
        Assert.IsTrue(RenderSettings.fog);
        Assert.AreEqual(look.FogDensity, RenderSettings.fogDensity, 1e-4f, $"{map}: run Tools/TagOfChaos/Maps/Apply Twisted Atmosphere (All).");
        var volume = Object.FindFirstObjectByType<PostProcessVolume>();
        Assert.IsTrue(volume.sharedProfile.TryGetSettings(out ColorGrading grading));
        Assert.AreEqual(look.Saturation, grading.saturation.value, 0.01f);
        Assert.AreEqual(look.PostExposure, grading.postExposure.value, 0.01f);
        Assert.IsTrue(volume.sharedProfile.TryGetSettings(out Vignette _), $"{map}: vignette missing");
        Assert.Greater(Object.FindObjectsByType<FlickerLight>(FindObjectsSortMode.None).Length, 0, $"{map}: no broken (flickering) lights");
    }

    [Test]
    public void Lobbies_HaveTwistedLook()
    {
        EditorSceneManager.OpenScene(TwistedAtmosphere.GameLobbyScene, OpenSceneMode.Single);
        var volume = Object.FindFirstObjectByType<PostProcessVolume>();
        Assert.NotNull(volume, "Waiting room needs its PostFX volume.");
        Assert.AreEqual(TwistedAtmosphere.GameLobbyProfile, AssetDatabase.GetAssetPath(volume.sharedProfile));
        Assert.NotNull(Camera.main.GetComponent<PostProcessLayer>());

        EditorSceneManager.OpenScene(TwistedAtmosphere.LobbyScene, OpenSceneMode.Single);
        var bg = Object.FindObjectsByType<UnityEngine.UI.Image>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(i => i.name == "BackGround");
        Assert.AreEqual(TwistedAtmosphere.LobbyBackground, AssetDatabase.GetAssetPath(bg.sprite));
    }

    // ---- V3 소품 ----

    [Test]
    public void PaletteMaterials_MatchPaintPalette()
    {
        var palette = AssetDatabase.LoadAssetAtPath<ColorPaletteSO>(PaletteCoverageMeter.PalettePath);
        for (int i = 0; i < TwistedPropBuilder.PaletteMaterials.Length; i++)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>($"{TwistedModelImportPostprocessor.MaterialFolder}/{TwistedPropBuilder.PaletteMaterials[i]}.mat");
            Assert.NotNull(m, TwistedPropBuilder.PaletteMaterials[i]);
            Color a = m.color, b = palette.GetColor(i);
            Assert.That(Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b), Is.LessThan(0.01f), $"{m.name} drifted from palette colour {i}");
        }
    }

    [Test]
    public void TwistedPrefabs_HaveCollidersCookiesCannotClimb()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { TwistedPropBuilder.PrefabFolder });
        Assert.GreaterOrEqual(guids.Length, 21, "Run Tools/TagOfChaos/Maps/Build Twisted Props.");
        foreach (string guid in guids)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            var boxes = go.GetComponentsInChildren<BoxCollider>(true);
            Assert.Greater(boxes.Length, 0, $"{go.name} has no collider");
            foreach (BoxCollider b in boxes)
                if (!b.name.StartsWith(TwistedPropBuilder.CollisionPrefix))
                    Assert.GreaterOrEqual(b.size.y, TwistedPropBuilder.MinColliderHeight - 0.01f, $"{go.name}/{b.name} is low enough for cookies to stand on");
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                Assert.IsTrue(r.sharedMaterials.All(m => m != null && AssetDatabase.GetAssetPath(m).StartsWith(TwistedModelImportPostprocessor.MaterialFolder)),
                    $"{go.name}/{r.name} uses a material outside {TwistedModelImportPostprocessor.MaterialFolder}");
        }
    }

    [TestCaseSource(nameof(Maps))]
    public void Map_HasAllTwistedProps(string map)
    {
        Open(map);
        GameObject root = GameObject.Find(TwistedPropPlacer.RootName);
        Assert.NotNull(root, $"{map}: run Tools/TagOfChaos/Maps/Place Twisted Props (All).");
        TwistedPropPlacer.SceneProps sp = TwistedPropPlacer.Scenes.First(s => s.ScenePath == MapSceneBuilder.ScenePath(map));
        foreach (TwistedPropPlacer.Spot spot in sp.Spots)
            Assert.AreEqual(spot.Count, root.transform.Cast<Transform>().Count(t => t.name.StartsWith(spot.Prefab + "_")), $"{map}: {spot.Prefab} count");
    }

    // ---- V4 숨을 곳 ----

    private static IEnumerable<Transform> Shelters() =>
        GameObject.Find(TwistedPropPlacer.RootName).transform.Cast<Transform>().Where(t => t.name.StartsWith("TW_Shelter_"));

    private static Vector3 Ground(Vector3 p) => TwistedPropPlacer.FloorBelow(p);

    // 문마다(출구 등 Light_Exit_*) 바깥 6 m에서 실내까지 괴물 경로가 있고, 그 경로가 건물을 돌아가지 않을 만큼 짧다 → 출입구 2곳 이상.
    [TestCaseSource(nameof(ShelterMaps))]
    public void Shelters_MonsterEntersThroughEveryDoor(string map)
    {
        Open(map);
        Physics.SyncTransforms();
        var shelters = Shelters().ToList();
        Assert.AreEqual(2, shelters.Count, $"{map}: expected 2 hiding spots");
        foreach (Transform s in shelters)
        {
            var exits = s.GetComponentsInChildren<Light>().Where(l => l.name.StartsWith("Light_Exit")).ToList();
            Assert.GreaterOrEqual(exits.Count, 2, $"{s.name}: fewer than 2 doorways");
            Assert.IsTrue(TwistedPropPlacer.DoorsOpen(s.gameObject), $"{s.name}: a doorway is blocked for the monster (no short grid path from 6 m outside to the inside).");
        }
    }

    [TestCaseSource(nameof(ShelterMaps))]
    public void Shelters_IndoorZoneCoversInsideOnly(string map)
    {
        Open(map);
        foreach (Transform s in Shelters())
        {
            var zone = s.GetComponent<IndoorZone>();
            Assert.NotNull(zone, $"{s.name}: no IndoorZone");
            foreach (Light l in s.GetComponentsInChildren<Light>().Where(l => l.name.StartsWith("Light_Inside")))
                Assert.IsTrue(zone.Contains(Ground(l.transform.position) + Vector3.up * 1.6f), $"{s.name}: {l.name} floor is not indoors");
            foreach (Light exit in s.GetComponentsInChildren<Light>().Where(l => l.name.StartsWith("Light_Exit")))
            {
                Vector3 dir = exit.transform.position - s.position;
                dir.y = 0f;
                Assert.IsFalse(zone.Contains(Ground(exit.transform.position + dir.normalized * 4f) + Vector3.up * 1.6f), $"{s.name}: outside {exit.name} counts as indoors");
            }
        }
    }

    [Test]
    public void IndoorZone_StepFadesTowardsTarget()
    {
        float v = IndoorZone.Step(0f, true, IndoorZone.FadeSeconds * 0.5f);
        Assert.AreEqual(0.5f, v, 1e-4f);
        Assert.AreEqual(1f, IndoorZone.Step(v, true, 10f));
        Assert.AreEqual(0f, IndoorZone.Step(v, false, 10f));
        Assert.Less(IndoorZone.WallCutoffHz, SpatialGain.FarCutoffHz, "Through-wall muffle must be darker than plain distance muffle.");
    }
}
