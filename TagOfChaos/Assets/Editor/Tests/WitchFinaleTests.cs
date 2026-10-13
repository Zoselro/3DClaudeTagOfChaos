using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

// 마녀 연출 다시 짜기(Request1009Plan.md §23) — 마녀가 걷는 길(Ground 경계에 닿는 자리), 넓힌 배경, 박살 대상(맵을 바꿔도 동작하는 규칙).
public class WitchFinaleTests
{
    private static readonly string[] Maps = { "CandyForest", "GingerbreadVillage", "ChocolateFactory", "CursedCandyCarnival", "HauntedBakery" };

    // ---- 내리칠 자리: 변에 닿든 모서리에 닿든 발끝이 Ground 경계에 닿는 곳(§23.4 Q6) ----

    [Test]
    public void StrikePoint_StopsWhereTheFootTouchesAnEdge()
    {
        var ground = Rect.MinMaxRect(-71f, -71f, 71f, 71f);
        Vector3 outward = Vector3.forward;                        // 마녀는 +Z 쪽에서 온다
        Vector3 start = outward * 450f;
        Vector3 feet = WitchPath.StrikePoint(ground, start, outward, 11f, out Vector3 contact);
        Assert.AreEqual(71f, contact.z, 1e-3f, "touches the +Z edge");
        Assert.AreEqual(0f, contact.x, 1e-3f);
        Assert.AreEqual(71f + 11f, feet.z, 1e-3f, "feet stand one foot-reach outside");
    }

    [Test]
    public void StrikePoint_StopsAtACornerWhenComingDiagonally()
    {
        var ground = Rect.MinMaxRect(-71f, -71f, 71f, 71f);
        Vector3 outward = new Vector3(-1f, 0f, -1f).normalized;
        Vector3 feet = WitchPath.StrikePoint(ground, outward * 450f, outward, 11f, out Vector3 contact);
        Assert.AreEqual(-71f, contact.x, 1e-2f);
        Assert.AreEqual(-71f, contact.z, 1e-2f, "the corner");
        Assert.AreEqual(11f, (feet - contact).magnitude, 1e-2f);
        Assert.IsFalse(ground.Contains(new Vector2(feet.x, feet.z)), "the witch never steps inside the Ground");
    }

    [Test]
    public void StrikePoint_WorksForAnOffCentreRectangle()
    {
        var ground = Rect.MinMaxRect(10f, -20f, 90f, 40f);       // 새 맵: 중심이 원점이 아니어도
        var center = new Vector3(ground.center.x, 0f, ground.center.y);
        Vector3 outward = WitchPath.OutwardDirection(center, center + Vector3.right * 100f);
        WitchPath.StrikePoint(ground, center + outward * 450f, outward, 5f, out Vector3 contact);
        Assert.AreEqual(90f, contact.x, 1e-3f);
    }

    // ---- 맵 5개(씬) ----

    [TestCaseSource(nameof(Maps))]
    public void Map_HasGroundPathBackdropAndBreakables(string map)
    {
        var scene = EditorSceneManager.OpenScene(MapSceneBuilder.ScenePath(map), OpenSceneMode.Single);
        Assert.IsTrue(WitchPath.TryGetGroundRect(scene, out Rect ground), $"{map}: Map/Ground");
        WitchPresenter witch = Object.FindFirstObjectByType<WitchPresenter>(FindObjectsInactive.Include);
        Assert.NotNull(witch, $"{map}: WITCH_Anchor");
        var center = new Vector3(ground.center.x, 0f, ground.center.y);
        Vector3 outward = WitchPath.OutwardDirection(center, witch.transform.position);
        Vector3 start = center + outward * WitchPath.StartDistance;
        Vector3 feet = WitchPath.StrikePoint(ground, start, outward, 90f * WitchPath.FootReachRatio, out Vector3 contact);
        Assert.IsFalse(ground.Contains(new Vector2(feet.x, feet.z)), $"{map}: strikes from outside the Ground");
        Assert.Less(Vector3.Distance(feet, center), Vector3.Distance(start, center), $"{map}: walks toward the map");

        // 넓힌 배경: 콜라이더 없음, 걷는 길에 소품 없음
        Transform backdrop = scene.GetRootGameObjects().First(g => g.name == "Map").transform.Find(WitchBackdropBuilder.RootName);
        Assert.NotNull(backdrop, $"{map}: Map/{WitchBackdropBuilder.RootName}");
        Assert.IsEmpty(backdrop.GetComponentsInChildren<Collider>(true), $"{map}: far backdrop has no colliders");
        Bounds far = backdrop.GetComponentsInChildren<Renderer>(true).Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
        Assert.Greater(far.extents.x, WitchPath.StartDistance, $"{map}: ground reaches past where the witch appears");
        foreach (Transform prop in backdrop.Cast<Transform>().Where(t => t.name != "FarGround"))
            Assert.IsFalse(WitchBackdropBuilder.OnWitchPath(prop.position, center, outward, WitchBackdropBuilder.PathHalfWidth), $"{map}: {prop.name} stands on the witch path");

        // 박살 대상: 있고, 바닥·배경·캐릭터는 아니다
        var groups = WitchShatter.FindTargetGroups(scene, ground);
        Assert.Greater(groups.Count, 20, $"{map}: something to smash");
        foreach (var g in groups)
            foreach (Renderer r in g.renderers)
            {
                for (Transform t = r.transform; t != null; t = t.parent)
                    Assert.That(t.name, Is.Not.EqualTo(WitchPath.GroundName).And.Not.EqualTo("Background").And.Not.EqualTo(WitchBackdropBuilder.RootName), $"{map}: {r.name}");
                Assert.IsNull(r.GetComponentInParent<WitchPresenter>(), $"{map}: {r.name}");
            }
    }
}
