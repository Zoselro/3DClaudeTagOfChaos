using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 탈출 모드 규칙 검증(EscapePlan.md §6). 규칙은 네트워크 없이 순수 계산만, 맵 배치는 씬을 열어 검사한다(§4.6).
public class EscapeTests
{
    // §1.1 인원표: 총 인원 → 괴물·스파이, 쿠키(= 필요 재료) = 총 − 괴물 − 스파이
    [TestCase(4, 1, 0, 3)]
    [TestCase(5, 1, 1, 3)]
    [TestCase(6, 1, 1, 4)]
    [TestCase(7, 2, 1, 4)]
    [TestCase(8, 2, 1, 5)]
    public void RoleTable_MatchesPlan(int players, int monsters, int spies, int cookies)
    {
        GameSettingsSO s = ScriptableObject.CreateInstance<GameSettingsSO>();
        Assert.AreEqual(monsters, s.MonsterCountFor(players));
        Assert.AreEqual(spies, s.SpyCountFor(players));
        Assert.AreEqual(cookies, players - s.MonsterCountFor(players) - s.SpyCountFor(players));
    }

    private static EscapeRecipeSO Recipe(string map) => EscapeCatalogSO.Current.RecipeFor(map);

    // §1.6 칸 모드: 필요 칸 = N, 고정 칸은 항상 필요, 나머지 칸은 채워진 상태로 시작
    [TestCase("CursedCandyCarnival", 3)]
    [TestCase("CursedCandyCarnival", 5)]
    [TestCase("HauntedBakery", 4)]
    [TestCase("ChocolateFactory", 5)]
    public void Planner_SlotMode_RequiredEqualsCookies(string map, int cookies)
    {
        EscapeRecipeSO recipe = Recipe(map);
        Assert.NotNull(recipe, map);
        for (int seed = 0; seed < 30; seed++)
        {
            List<RecipePlanner.PlannedSlot> slots = RecipePlanner.Plan(recipe, cookies, new System.Random(seed));
            Assert.AreEqual(cookies, slots.Count(s => s.Required), $"{map} seed {seed}");
            int index = 0;
            foreach (EscapeRecipeSO.SlotGroup g in recipe.Groups)
            {
                for (int k = 0; k < g.fixedRequired; k++) Assert.IsTrue(slots[index + k].Required, $"{map} fixed {g.item.ItemId}");
                index += g.slotCount;
            }
        }
    }

    [Test]
    public void Planner_CounterMode_MakesNSlotsAcceptingAnyColor()
    {
        EscapeRecipeSO recipe = Recipe("CandyForest");
        List<RecipePlanner.PlannedSlot> slots = RecipePlanner.Plan(recipe, 4, new System.Random(1));
        Assert.AreEqual(4, slots.Count);
        Assert.IsTrue(slots.All(s => s.Required && s.Accepts.Length == recipe.CounterItems.Length));
    }

    [Test]
    public void State_EncodeDecode_RoundTrips()
    {
        var s = new EscapeState { RequiredCount = 3 };
        s.Items.Add(new EscapeState.Item { Id = "Gear", Loc = ItemLocation.Held, A = 2, B = 1, Pos = new Vector3(1, 2, 3), Charges = 3, StolenFromDevice = true });
        s.Chests.Add(new EscapeState.Chest { Anchor = 7, Spy = true, Opened = true });
        s.DeviceSlots.Add(new EscapeState.Slot { Label = "Gear", Accepts = new[] { "Gear" }, Prefilled = false, ItemIndex = 0 });
        s.RocketSlots.Add(new EscapeState.Slot { Label = "Oil", Accepts = new[] { "ChocolateOil" }, RocketFilled = true, ItemIndex = -1 });
        s.Escaped.Add(5);
        s.Boarded.Add(6);

        EscapeState d = EscapeState.Decode(s.Encode());
        Assert.NotNull(d);
        Assert.AreEqual(3, d.RequiredCount);
        Assert.AreEqual("Gear", d.Items[0].Id);
        Assert.AreEqual(ItemLocation.Held, d.Items[0].Loc);
        Assert.AreEqual(1, d.Items[0].B);
        Assert.IsTrue(d.Items[0].StolenFromDevice);
        Assert.AreEqual(7, d.Chests[0].Anchor);
        Assert.IsTrue(d.Chests[0].Spy && d.Chests[0].Opened);
        Assert.IsTrue(d.DeviceSlots[0].Filled && d.RocketSlots[0].Filled);
        Assert.AreEqual(5, d.Escaped[0]);
        Assert.AreEqual(6, d.Boarded[0]);
        Assert.AreEqual(0, d.HeldItemAt(2, 1));
    }

    // D32: 1·3·4번에 아이템, 2번이 빔 → 1번을 떨어뜨리면 3번을 든다. 모두 비면 그대로(빈손).
    [Test]
    public void Inventory_AutoEquip_FindsNextOccupied()
    {
        Assert.AreEqual(2, PlayerInventory.NextOccupied(new[] { -1, -1, 5, 6 }, 0));
        Assert.AreEqual(0, PlayerInventory.NextOccupied(new[] { 4, -1, -1, -1 }, 3));
        Assert.AreEqual(1, PlayerInventory.NextOccupied(new[] { -1, -1, -1, -1 }, 1));
    }

    [TestCase("기어", false)]          // 기어
    [TestCase("레드 버튼", true)] // 레드 버튼
    [TestCase("룬", true)]                  // 룬
    [TestCase("Gear", false)]
    public void KoreanText_FinalConsonant(string word, bool expected) => Assert.AreEqual(expected, KoreanText.HasFinalConsonant(word));

    // §1.7 방 설정: 범위 밖은 가장 가까운 허용값 + 경고, 문자는 기본값
    [Test]
    public void RoomSettingStepper_ClampsAndWarns()
    {
        var players = new RoomSettingStepper(4, 8, 4);
        Assert.AreEqual(RoomSettingStepper.Outcome.OutOfRange, players.Step(-1));
        Assert.AreEqual(4, players.Value);
        Assert.AreEqual(RoomSettingStepper.Outcome.OutOfRange, players.SetFromText("9"));
        Assert.AreEqual(8, players.Value);
        Assert.AreEqual(RoomSettingStepper.Outcome.Empty, players.SetFromText("abc"));
        Assert.AreEqual(4, players.Value);
        var time = new RoomSettingStepper(10, 40, 10);
        Assert.AreEqual(RoomSettingStepper.Outcome.OutOfRange, time.SetFromText("41"));
        Assert.AreEqual(40, time.Value);
        Assert.AreEqual(RoomSettingStepper.Outcome.Ok, time.SetFromText("25"));
        var attack = new RoomSettingStepper(1, 10, 1);
        Assert.AreEqual(RoomSettingStepper.Outcome.OutOfRange, attack.SetFromText("11"));
        Assert.AreEqual(10, attack.Value);
    }

    private static EscapeRules.PlayerInfo P(bool monster = false, bool spy = false, bool escaped = false, bool broken = false) =>
        new EscapeRules.PlayerInfo { IsMonster = monster, IsSpy = spy, Escaped = escaped, Broken = broken };

    // §1.2 게임 끝
    [Test]
    public void Rules_EndConditions()
    {
        var hunt = new[] { P(monster: true), P(), P(escaped: true), P(spy: true) };
        Assert.AreEqual(EscapeRules.Decision.None, EscapeRules.Evaluate(hunt, false, 10, 100, true, 0, -1));
        Assert.AreEqual(EscapeRules.Decision.EndTimeUp, EscapeRules.Evaluate(hunt, false, 100, 100, true, 0, -1)); // D8

        var done = new[] { P(monster: true), P(broken: true), P(escaped: true), P(spy: true, broken: true) };
        Assert.AreEqual(EscapeRules.Decision.EndAllResolved, EscapeRules.Evaluate(done, false, 10, 100, true, 0, -1));

        var spyLeft = new[] { P(monster: true), P(broken: true), P(spy: true) }; // 쿠키는 끝, 스파이만 남음 → 계속
        Assert.AreEqual(EscapeRules.Decision.None, EscapeRules.Evaluate(spyLeft, false, 10, 100, true, 0, -1));

        // 타임어택: 방 시간은 멈춘다(D15) — 제한시간이 지나도 끝나지 않는다
        var ta = new[] { P(monster: true), P(), P(spy: true, escaped: true) };
        Assert.AreEqual(EscapeRules.Decision.None, EscapeRules.Evaluate(ta, true, 500, 100, true, 600, -1));
        Assert.AreEqual(EscapeRules.Decision.StrikeWitch, EscapeRules.Evaluate(ta, true, 600, 100, true, 600, -1));
        Assert.AreEqual(EscapeRules.Decision.None, EscapeRules.Evaluate(ta, true, 601, 100, true, 600, 1f));
        Assert.AreEqual(EscapeRules.Decision.EndWitchStrike, EscapeRules.Evaluate(ta, true, 603, 100, true, 600, EscapeRules.WitchSlamSeconds));

        var caughtAll = new[] { P(monster: true), P(broken: true), P(spy: true, escaped: true) }; // 괴물이 다 잡음
        Assert.AreEqual(EscapeRules.Decision.EndAllResolved, EscapeRules.Evaluate(caughtAll, true, 500, 100, true, 600, -1));
    }

    [Test]
    public void Phase_TimeAttackAfterSpyEscapes()
    {
        Assert.AreEqual(GamePhase.TimeAttack, GamePhaseState.Evaluate(true, 0, true, false, 10, spyEscaped: true));
        Assert.AreEqual(GamePhase.Hunt, GamePhaseState.Evaluate(true, 0, true, false, 10, spyEscaped: false));
        Assert.AreEqual(GamePhase.Result, GamePhaseState.Evaluate(true, 0, true, true, 10, spyEscaped: true));
    }

    // D38: 5초에 걸쳐 알파 0 → 1
    [Test]
    public void Witch_FadesInOverFiveSeconds()
    {
        Assert.AreEqual(0f, WitchPresenter.FadeAlpha(100, 100, 5f), 1e-4f);
        Assert.AreEqual(0.5f, WitchPresenter.FadeAlpha(100, 102.5, 5f), 1e-4f);
        Assert.AreEqual(1f, WitchPresenter.FadeAlpha(100, 110, 5f), 1e-4f);
    }

    [Test]
    public void Catalog_HasRecipeForEveryMap()
    {
        foreach (string map in MapSceneBuilder.MapNames) Assert.NotNull(EscapeCatalogSO.Current.RecipeFor(map), map);
    }

    // P1: 모든 아이템에 Blender 모델이 연결돼 있다(바닥·손·장치·로켓 칸이 같은 모델을 쓴다).
    [Test]
    public void Items_AllHaveBlenderModels()
    {
        string[] guids = AssetDatabase.FindAssets("t:ItemSO", new[] { "Assets/03. SO/Escape/Items" });
        Assert.Greater(guids.Length, 0);
        foreach (string guid in guids)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemSO>(AssetDatabase.GUIDToAssetPath(guid));
            Assert.NotNull(item.ModelPrefab, item.ItemId);
        }
    }

    // §4.6: 맵마다 탈출 장치·로켓·상자 자리 20곳·마녀가 이름 규칙대로 있고 모델을 쓴다.
    [TestCase("CandyForest")]
    [TestCase("GingerbreadVillage")]
    [TestCase("ChocolateFactory")]
    [TestCase("CursedCandyCarnival")]
    [TestCase("HauntedBakery")]
    public void MapScene_EscapeSetupComplete(string map)
    {
        EditorSceneManager.OpenScene(MapSceneBuilder.ScenePath(map), OpenSceneMode.Single);
        GameObject root = GameObject.Find(EscapeMapSetup.RootName);
        Assert.NotNull(root, "EscapeSetup");

        Transform device = root.transform.Find($"ESC_{map}_Root");
        Assert.NotNull(device, "device");
        Assert.NotNull(device.GetComponent<EscapeDevice>());
        Assert.NotNull(device.Find("Body"), "device model");
        Assert.NotNull(EscapeSequence.FindDeep(device, "Slot_00"), "device slot"); // 탈것 칸은 차 아래에 있다
        Assert.Greater(device.GetComponentsInChildren<Collider>(true).Length, 0, "device collider");

        Transform rocket = root.transform.Find($"SPY_Rocket_{map}_Root");
        Assert.NotNull(rocket, "rocket");
        Assert.NotNull(rocket.GetComponent<SpyRocket>());
        for (int i = 0; i < 2; i++) // 칸 위치: 빈 앵커(Slot_nn) 또는 빈/채움 모양(Slot_nn_Empty·Filled)
            Assert.IsTrue(rocket.Find($"Slot_{i:00}") != null
                          || (rocket.Find($"Slot_{i:00}_Empty") != null && rocket.Find($"Slot_{i:00}_Filled") != null), $"rocket slot {i}");

        int chests = 0;
        foreach (Transform t in root.transform)
        {
            if (!t.name.StartsWith("CHEST_Slot_")) continue;
            chests++;
            Assert.NotNull(t.GetComponent<MaterialChest>(), t.name);
            Assert.NotNull(t.Find("Lid"), t.name + " lid");
        }
        Assert.AreEqual(EscapeMapSetup.ChestSlotCount, chests);

        Transform witch = root.transform.Find("WITCH_Anchor");
        Assert.NotNull(witch);
        Assert.NotNull(witch.Find("Model"), "witch model");
    }

    // §4.6: 쿠키 스폰에서 탈출 장치와 로켓까지 걸어갈 수 있다(상호작용 거리 안의 한 곳이라도).
    [TestCase("CandyForest")]
    [TestCase("GingerbreadVillage")]
    [TestCase("ChocolateFactory")]
    [TestCase("CursedCandyCarnival")]
    [TestCase("HauntedBakery")]
    public void MapScene_DeviceAndRocketReachableByCookie(string map)
    {
        EditorSceneManager.OpenScene(MapSceneBuilder.ScenePath(map), OpenSceneMode.Single);
        Vector3 spawn = GameObject.Find(SceneSpawnPoints.Cookie).transform.position;
        GameObject root = GameObject.Find(EscapeMapSetup.RootName);
        // 큰 장치(케이크·오븐)는 중심이 몸 안이라 재료를 끼우는 칸 자리에서 잰다
        Transform device = root.transform.Find($"ESC_{map}_Root");
        Transform slot = EscapeSequence.FindDeep(device, "Slot_00");
        Assert.IsTrue(ReachableNear(spawn, device.position, 3.5f) || (slot != null && ReachableNear(spawn, slot.position, 1.5f)), "device");
        Assert.IsTrue(ReachableNear(spawn, root.transform.Find($"SPY_Rocket_{map}_Root").position, 2.2f), "rocket");
    }

    // EscapeVisualPlan.md §5.5: 시계탑 나선 경사로로 쿠키·괴물 모두 지하 유적의 제단까지 내려가고, 추락 처리는 지하보다 아래다
    [Test]
    public void Gingerbread_UndergroundAltarReachable()
    {
        EditorSceneManager.OpenScene(MapSceneBuilder.ScenePath("GingerbreadVillage"), OpenSceneMode.Single);
        Transform device = GameObject.Find(EscapeMapSetup.RootName).transform.Find("ESC_GingerbreadVillage_Root");
        Transform board = EscapeSequence.FindDeep(device, "Board");
        Assert.NotNull(board);
        Assert.Less(board.position.y, -5f, "altar is underground");
        Assert.NotNull(MapPassabilityCheck.FindPath(GameObject.Find(SceneSpawnPoints.Cookie).transform.position, board.position, cookie: true), "cookie path");
        Assert.NotNull(MapPassabilityCheck.FindPath(GameObject.Find(SceneSpawnPoints.Monster).transform.position, board.position + Vector3.left * 2f, cookie: false), "monster path");
        Assert.Less(GameObject.Find("VoidKillZone").transform.position.y, -25f, "kill zone below the ruins");
    }

    // 다른 층의 칸에는 닿지 않는다: 지상에서 지하 제단 칸을 만질 수 없다
    [Test]
    public void Device_DistanceIgnoresOtherFloors()
    {
        var root = new GameObject("DeviceTest");
        try
        {
            var slot = new GameObject("Slot_00").transform;
            slot.SetParent(root.transform, false);
            slot.localPosition = new Vector3(0f, -10f, 0f);
            var device = root.AddComponent<EscapeDevice>();
            Assert.Greater(device.DistanceTo(new Vector3(0.5f, 0f, 0f)), 100f, "from the surface");
            Assert.Less(device.DistanceTo(new Vector3(0.5f, -10f, 0f)), 1f, "on the same floor");
        }
        finally { Object.DestroyImmediate(root); }
    }

    // EscapeVisualPlan.md §5.3: 쿠키가 경사 발판을 올라 오븐 문 앞(탑승 지점)까지 갈 수 있다
    [Test]
    public void Bakery_RampReachesOvenDoor()
    {
        EditorSceneManager.OpenScene(MapSceneBuilder.ScenePath("HauntedBakery"), OpenSceneMode.Single);
        Vector3 spawn = GameObject.Find(SceneSpawnPoints.Cookie).transform.position;
        Transform device = GameObject.Find(EscapeMapSetup.RootName).transform.Find("ESC_HauntedBakery_Root");
        Transform board = EscapeSequence.FindDeep(device, "Board");
        Assert.NotNull(board);
        Assert.Greater(board.position.y, 2f, "board is up on the oven ledge");
        Assert.NotNull(MapPassabilityCheck.FindPath(spawn, board.position, cookie: true), "ramp path");
        Assert.IsNull(GameObject.Find("HAU_MagicOven_Door").GetComponent<InteractableDoor>(), "oven door is not a normal door");
    }

    private static bool ReachableNear(Vector3 from, Vector3 target, float radius)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 p = target + Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * radius;
            if (MapPassabilityCheck.FindPath(from, p, cookie: true) != null) return true;
        }
        return false;
    }

    // EscapeVisualPlan.md §4.3: 탈것이 출발 연출 중이면 끝 판정을 미루고, 연출이 끝나면 정상 판정
    [Test]
    public void Rules_HoldEndWhileVehicleDeparts()
    {
        var players = new List<EscapeRules.PlayerInfo>
        {
            new EscapeRules.PlayerInfo { IsMonster = true },
            new EscapeRules.PlayerInfo { Escaped = true },
        };
        Assert.AreEqual(EscapeRules.Decision.None, EscapeRules.Evaluate(players, false, 10, 100, true, 0, -1f, departing: true));
        Assert.AreEqual(EscapeRules.Decision.EndAllResolved, EscapeRules.Evaluate(players, false, 10, 100, true, 0, -1f, departing: false));
    }

    // 상태 형식 2: 탑승 대기·완성 시각·출발 시각도 그대로 오간다
    [Test]
    public void State_EncodeDecode_KeepsBoardingFields()
    {
        var s = new EscapeState { RequiredCount = 2, CompletedAt = 12.5, DepartedAt = 30.25 };
        s.Waiting.Add(3); s.Waiting.Add(5);
        s.Escaped.Add(3);
        EscapeState d = EscapeState.Decode(s.Encode());
        Assert.NotNull(d);
        CollectionAssert.AreEqual(new[] { 3, 5 }, d.Waiting);
        CollectionAssert.AreEqual(new[] { 3 }, d.Escaped);
        Assert.AreEqual(12.5, d.CompletedAt, 1e-9);
        Assert.AreEqual(30.25, d.DepartedAt, 1e-9);
    }
}
