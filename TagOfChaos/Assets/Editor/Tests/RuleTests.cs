using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Photon.Pun;
using UnityEditor;
using UnityEngine;

// 네트워크·씬 없이 검증할 수 있는 게임 규칙의 EditMode 테스트(research.md §12 E10).
// 오프라인 Play Mode 검증으로는 재현이 어려운 규칙(방장 정책, 인원별 수치, 키 수명 분류, 단계 해석, 직렬화 순서)을
// 회귀 방지용으로 고정한다. Test Runner(Window > General > Test Runner)의 EditMode에서 실행한다.
public class RuleTests
{
    // ---- 방장 정책(Bug-fix-plan.md §26.8) ----

    [Test]
    public void DesiredMaster_IsEarliestJoinedNonMonster()
    {
        Assert.AreEqual(1, RoomState.DesiredMasterActor(new[] { 3, 1, 2, 4 }, new int[0]));
        Assert.AreEqual(2, RoomState.DesiredMasterActor(new[] { 1, 2, 3, 4 }, new[] { 1 }));
        Assert.AreEqual(3, RoomState.DesiredMasterActor(new[] { 1, 2, 3, 4 }, new[] { 1, 2 }));
        Assert.AreEqual(2, RoomState.DesiredMasterActor(new[] { 4, 2, 7 }, null));
    }

    [Test]
    public void DesiredMaster_IsNone_WhenOnlyMonstersRemain()
    {
        Assert.AreEqual(-1, RoomState.DesiredMasterActor(new[] { 5 }, new[] { 5 }));
        Assert.AreEqual(-1, RoomState.DesiredMasterActor(new int[0], new int[0]));
    }

    // 호스트(시작 버튼 주인, Bug-fix-plan.md §33)는 괴물 여부와 무관하게 가장 먼저 들어온 사람이다.
    [Test]
    public void Host_IsEarliestJoined_EvenIfMonster()
    {
        int[] actors = { 1, 2, 3, 4 };
        Assert.AreEqual(1, RoomState.HostActor(actors));
        Assert.AreEqual(2, RoomState.DesiredMasterActor(actors, new[] { 1 })); // 진행 권한은 괴물이 아닌 사람에게
        Assert.AreEqual(1, RoomState.HostActor(actors));                       // 시작 버튼은 그대로 방 생성자
        Assert.AreEqual(3, RoomState.HostActor(new[] { 4, 3 }));               // 앞선 입장자들이 나가면 다음 입장자
        Assert.AreEqual(-1, RoomState.HostActor(new int[0]));
    }

    // ---- 인원별 설정 계산 ----

    private static GameSettingsSO CreateSettings(int maxPlayers, int monsterCount)
    {
        var settings = ScriptableObject.CreateInstance<GameSettingsSO>();
        var so = new SerializedObject(settings);
        so.FindProperty("maxPlayers").intValue = maxPlayers;
        so.FindProperty("monsterCount").intValue = monsterCount;
        so.ApplyModifiedPropertiesWithoutUndo();
        return settings;
    }

    [Test]
    public void MonsterCount_LeavesAtLeastOneCookie()
    {
        GameSettingsSO settings = CreateSettings(8, 2);
        Assert.AreEqual(2, settings.MonsterCountFor(8));
        Assert.AreEqual(1, settings.MonsterCountFor(2)); // 쿠키가 최소 1명
        Assert.AreEqual(1, settings.MonsterCountFor(1));
        Object.DestroyImmediate(settings);
    }

    [Test]
    public void PaintStrokeSendInterval_UnchangedUpToReference_ScalesBeyond()
    {
        GameSettingsSO settings = CreateSettings(8, 1); // 기본: 15Hz, 기준 4명, 최소 5Hz
        Assert.AreEqual(1f / 15f, settings.PaintStrokeSendIntervalFor(2), 1e-5f);
        Assert.AreEqual(1f / 15f, settings.PaintStrokeSendIntervalFor(4), 1e-5f);
        Assert.AreEqual(1f / (15f * 3f / 7f), settings.PaintStrokeSendIntervalFor(8), 1e-5f);
        Assert.AreEqual(1f / 5f, settings.PaintStrokeSendIntervalFor(20), 1e-5f); // 최소 전송 횟수 하한
        Object.DestroyImmediate(settings);
    }

    // ---- Props 키 수명 분류(research.md §12 E8) ----

    [Test]
    public void EveryNetKey_IsDeclaredInScopesExactlyOnce()
    {
        var declared = new Dictionary<string, int>();
        foreach (NetKeys.Scope scope in NetKeys.Scopes)
        {
            declared.TryGetValue(scope.Key, out int count);
            declared[scope.Key] = count + 1;
        }

        foreach (FieldInfo field in typeof(NetKeys).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (!field.IsLiteral || field.FieldType != typeof(string)) continue;
            string key = (string)field.GetRawConstantValue();
            Assert.IsTrue(declared.TryGetValue(key, out int count), $"NetKeys.{field.Name} is missing from NetKeys.Scopes (declare its target and lifetime).");
            Assert.AreEqual(1, count, $"NetKeys.{field.Name} is declared more than once in NetKeys.Scopes.");
        }
    }

    [Test]
    public void RoundKeys_MatchDeclaredLifetimes()
    {
        CollectionAssert.Contains(NetKeys.RoundRoomKeys, NetKeys.MonsterActorNumbers);
        CollectionAssert.Contains(NetKeys.RoundRoomKeys, NetKeys.PaintPhaseEndTime);
        CollectionAssert.Contains(NetKeys.RoundRoomKeys, NetKeys.GameResult);
        CollectionAssert.Contains(NetKeys.RoundPlayerKeys, NetKeys.HitCount);
        CollectionAssert.Contains(NetKeys.RoundPlayerKeys, NetKeys.RegisteredSlotCount);
        CollectionAssert.DoesNotContain(NetKeys.RoundPlayerKeys, NetKeys.SkinIndex); // 스킨은 판이 바뀌어도 유지
        Assert.AreEqual(10, NetKeys.RoundRoomKeys.Length); // 예전 수동 목록과 같은 10개
    }

    // ---- 게임 단계 해석(research.md §12 E2) ----

    [Test]
    public void GamePhase_IsDerivedFromRoomProps()
    {
        Assert.AreEqual(GamePhase.Lobby, GamePhaseState.Evaluate(false, 0, false, false, 100));
        Assert.AreEqual(GamePhase.Paint, GamePhaseState.Evaluate(true, 160, false, false, 100));
        Assert.AreEqual(GamePhase.AwaitingMonster, GamePhaseState.Evaluate(true, 160, false, false, 160));
        Assert.AreEqual(GamePhase.Hunt, GamePhaseState.Evaluate(true, 160, true, false, 170));
        Assert.AreEqual(GamePhase.Result, GamePhaseState.Evaluate(true, 160, true, true, 900));
    }

    // ---- 스킨 목록 ----

    [Test]
    public void SkinCatalog_ClampsOutOfRangeIndex()
    {
        var catalog = ScriptableObject.CreateInstance<SkinCatalogSO>();
        var so = new SerializedObject(catalog);
        SerializedProperty skins = so.FindProperty("skins");
        skins.arraySize = 3;
        for (int i = 0; i < 3; i++) skins.GetArrayElementAtIndex(i).FindPropertyRelative("label").stringValue = ((char)('A' + i)).ToString();
        so.ApplyModifiedPropertiesWithoutUndo();

        Assert.AreEqual(3, catalog.Count);
        Assert.AreEqual("A", catalog.GetLabel(-5));
        Assert.AreEqual("C", catalog.GetLabel(99));
        Object.DestroyImmediate(catalog);
    }

    // ---- 캐릭터 동기화 직렬화(research.md §12 E1 — 공용 제네릭으로 바꿔도 순서·값이 같아야 기존 빌드와 호환) ----

    // 쓰기 스트림의 버퍼는 PUN 내부(SetWriteStream, internal)에서만 설정되므로 테스트에서는 리플렉션으로 빈 버퍼를 넣는다.
    private static PhotonStream CreateWriteStream()
    {
        var stream = new PhotonStream(true, null);
        typeof(PhotonStream).GetField("writeData", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(stream, new List<object>());
        return stream;
    }

    [Test]
    public void PlayerSync_RoundTripsStateAndCarryFlag()
    {
        var go = new GameObject("SyncTest");
        try
        {
            go.transform.SetPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(0, 90, 0));
            PhotonStream writer = CreateWriteStream();
            new PlayerNetworkSync().Write(writer, go.transform, PlayerMoveState.Dodge, true);

            object[] sent = writer.ToArray();
            Assert.AreEqual(4, sent.Length);
            Assert.AreEqual((int)PlayerMoveState.Dodge, sent[2]); // 상태는 int로 전송

            var reader = new PhotonStream(false, sent);
            var sync = new PlayerNetworkSync();
            var target = new GameObject("SyncTarget");
            try
            {
                sync.Read(reader, target.transform);
                Assert.AreEqual(PlayerMoveState.Dodge, sync.RemoteState);
                Assert.IsTrue(sync.RemoteIsCarrying);
                Assert.AreEqual(new Vector3(1, 2, 3), target.transform.position); // 첫 수신은 스냅
            }
            finally { Object.DestroyImmediate(target); }
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void MonsterSync_RoundTripsState()
    {
        var go = new GameObject("MonsterSyncTest");
        try
        {
            PhotonStream writer = CreateWriteStream();
            new NetworkTransformSync<MonsterMoveState>().Write(writer, go.transform, MonsterMoveState.GrabKill);
            var sync = new NetworkTransformSync<MonsterMoveState>();
            sync.Read(new PhotonStream(false, writer.ToArray()), go.transform);
            Assert.AreEqual(MonsterMoveState.GrabKill, sync.RemoteState);
        }
        finally { Object.DestroyImmediate(go); }
    }

    // ---- UI 배치(Bug-fix-plan.md §28 R2의 검사 도구를 회귀 테스트로) ----

    [Test]
    public void UiLayout_HasNoOffscreenElements()
    {
        Assert.AreEqual(0, UILayoutValidator.ValidateBuildScenesAndPrefabs(), "See [UILayoutValidator] warnings in the Console.");
    }
}
