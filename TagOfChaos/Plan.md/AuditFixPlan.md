# 감사 결과 수정 계획 (research.md 6차 F-01~F-25 + 클라이언트 조작 보안)

> 작성 2026-10-12. 근거: `Plan.md/research.md` 6차 판(2026-10-11)과 **현재 소스(2026-10-11 작업 트리)를 다시 읽은 결과**. 경로는 따로 적지 않으면 `Assets/02. Scripts/` 기준, 줄 번호는 작성 시점 기준.
> 상태: ⬜ 대기 · 🔄 진행 · ✅ 완료 · ⏸ 보류(결정 대기). **이 문서는 계획이다 — 사용자 승인(14장 질문) 전에는 코드를 고치지 않는다.**
> 진행 규칙(기존 요청과 같음): 단계를 마칠 때마다 이 문서에 완료로 표시 · 코드 변경 후 C# 컴파일 오류·Console 예외/경고 확인 · 구현 후 Play Mode로 실제 동작 확인 · 코드에 한글 금지(문구는 씬·프리팹·SO에 `\uXXXX`) · 객체지향·최적화.

## 목차
0. 요약
1. 클라이언트 조작 위협 모델 — PUN이 실제로 허용하는 것(소스 근거)
2. 조작 경로 전수표 S-01~S-15 (F-01·F-06 포함)
3. 설계 원칙 — 무엇을 막고, 무엇을 감지·복구하고, 무엇은 받아들이는가
4. 단계 계획 개요(P0~P9)
5. P0 사전 검증(스파이크)
6. P1 기준선 회복 — F-02
7. P2 보안 기반 — 방장 회수(S-01), 속성 감시·복구(S-02·S-03)
8. P3 권위 이동 — 사망·탈출·잡은 수를 방장이 확정(S-04·S-05·S-06·S-14)
9. P4 받는 쪽 검증 — F-01, F-06, 채팅(S-11), 지역 이동(S-12), F-17
10. P5 네트워크 경합·정합성 — F-03, F-04, F-05, F-09, F-10, F-18, F-19
11. P6 정리·성능 — F-11, F-12, F-13, F-15, F-16
12. P7 구조 — F-07+F-14, F-08 / P8 Info 항목 F-20~F-25
13. P9 시험·Play Mode·빌드·문서, 회귀 위험
14. 결정이 필요한 질문(Q1~Q9)
15. F-ID ↔ 단계 대응표
16. 멀티 시험 절차(사용자 — PC 4대)

---

## 0. 요약

- **감사 때보다 위험이 크다.** 감사(research.md)는 "받는 쪽이 보낸 사람을 확인하는가"만 봤다. 이번에 PUN 소스를 직접 읽어 보니, 이 프로젝트가 쓰는 Photon Cloud(서버 플러그인 없음)에서는 **어떤 클라이언트든**
  ① **스스로 방장이 될 수 있고**(`Room.SetMasterClient`가 클라이언트 쪽 권한 검사 없이 CAS만 보냄),
  ② **아무 방 속성이나**(게임 결과·괴물 목록·지역 이동·PUN 씬 키까지) 쓸 수 있으며,
  ③ **다른 사람의 플레이어 속성**(`HitCount` 등)도 쓸 수 있다(`Player.SetCustomProperties`가 대상이 본인인지 보지 않음).
  게다가 이 게임은 **쿠키의 사망을 피해자 본인이 확정**하고, **탈출 여부의 일부를 본인이 쓴 값(`Escaped`)으로 판단**한다 → "잡혀도 안 죽기", "타지 않고 탈출 처리", "남을 즉사시키기", "모두를 방에서 쫓아내기"가 수정된 클라이언트 하나로 가능하다(2장).
- **완전한 차단은 불가능하다** — 서버가 판정하지 않는 구조(Photon Cloud, 플러그인 없음)에서는 조작된 **방장** 한 명은 막을 수 없다. 그래서 계획은 세 겹이다(3장):
  1. **받는 쪽 검증**(누가 보냈는지·범위·거리·단계) — 확실히 막는다.
  2. **정직한 방장의 감지·복구** + **정직한 클라이언트의 방장 되찾기** — 남이 쓴 권위 데이터를 되돌리고, 방장을 가로채면 되찾는다.
  3. **권위 데이터를 방장 한 곳으로** — 사망·탈출·잡은 수를 방장이 검증해서 쓴다. 본인이 쓴 값은 판정에 쓰지 않는다.
- 나머지 F-항목(경합·정리·성능·구조)은 보안 작업과 겹치는 파일이 많아 **보안 단계 뒤에** 한다.
- 맨 앞에 **P0 스파이크**가 있다: 속성 변경 이벤트(코드 253)에 "바꾼 사람"이 실제로 담기는지 2클라이언트로 확인해야 P2 설계가 확정된다(안 담기면 대안 경로로 간다 — 7.3).

---

## 1. 클라이언트 조작 위협 모델 — PUN이 실제로 허용하는 것

| # | 조작 능력 | 소스 근거(프로젝트 안 PUN 2.55) | 이 게임에 주는 의미 |
|---|---|---|---|
| T1 | 어떤 RaiseEvent·RPC든 원하는 내용으로 보낸다 | 일반 PUN 동작 | 받는 쪽이 `EventData.Sender`/`PhotonMessageInfo.Sender`를 확인하지 않으면 위조 가능 |
| T2 | **방 속성 아무 키나 쓴다** | `PhotonRealtime/Code/Room.cs` `SetCustomProperties` — 온라인이면 클라이언트 쪽 검사 없이 `LoadBalancingClient.OpSetPropertiesOfRoom` 호출 | `GameResult`, `EscapeState`, `MonsterActorNumbers`, `SpyActorNumbers`, `WitchStrike`, `RegionMove`, PUN 내부 `curScn`(씬 강제 이동) 모두 위조 가능 |
| T3 | **다른 사람의 플레이어 속성을 쓴다** | `PhotonRealtime/Code/Player.cs:377-405` — 온라인이면 `OpSetPropertiesOfActor(this.actorNumber, …)`, 대상이 `IsLocal`인지 검사 없음 | 남의 `HitCount=2`(즉사), `Escaped=1`, `CatchCount` 등 |
| T4 | **스스로 방장이 된다** | `PhotonUnityNetworking/Code/PhotonNetwork.cs:1526-1535` → `Room.cs:496-505`: `{MasterClientId: 나}`를 `{MasterClientId: 현재 방장}` 조건(CAS)으로 보낼 뿐, "지금 방장인가" 검사 없음 | 방장 권위 코드 전부(판정·탈출 상태·씬 이동)를 그 클라이언트가 실행 |
| T5 | 자기 캐릭터의 위치·회전·상태를 마음대로 보낸다 | 이동은 소유자 권위(`HideOrSeekPlayer`/`MonsterController` → `NetworkTransformSync.Write`) | 순간이동·속도 핵. 방장의 거리 검사(`EscapeAuthority.IsNear`)는 이 위치를 믿는다 |
| T6 | 속성 변경은 서버를 거쳐 모두에게 **변경 이벤트(코드 253)**로 온다 | `LoadBalancingClient.cs:3492-3513`(캐시 반영) 뒤 `:3577-3580`에서 `EventReceived`로 **모든 이벤트를 게임 코드에 전달** | 정직한 클라이언트가 "누가 바꿨는지"(`EventData.Sender`)를 볼 수 있으면 감지·복구 가능 — **P0에서 실측 확인** |

**방어의 한계(받아들일 것)**: 조작된 클라이언트가 **방장인 동안**에는 그 클라이언트의 코드가 판정을 하므로 막을 수 없다. 할 수 있는 것은 (a) 방장 가로채기를 정직한 클라이언트가 즉시 되찾고(S-01), (b) 방장이 정직할 때 다른 사람의 위조를 되돌리는 것(S-02·S-03)이다. 근본 해결은 서버 판정(Photon Server Plugin — Enterprise Cloud 또는 자체 서버)뿐이며 이번 범위 밖이다(13.4).

---

## 2. 조작 경로 전수표 (S-01~S-15)

> 감사 F-번호와 겹치면 함께 적었다. "현재"는 지금 코드에서 확인한 사실, "수정"은 7~9장의 구체 계획.

| ID | 조작 | 현재 코드(근거) | 피해 | 심각 | 수정(단계) |
|---|---|---|---|---|---|
| **S-01** | 스스로 방장이 됨(T4) | `Monster/MasterClientPolicy.cs` `EnforcePolicy` 50-63은 **현재 방장만** 정책을 적용 — 가로챈 방장은 정책을 안 돌리고, 정직한 클라이언트는 아무것도 안 함 | 판정·결과·탈출 상태·씬 이동을 마음대로 | **Critical** | 정직한 "원래 방장 자격자"가 되찾기(7.1) — P2 |
| **S-02** | 방 속성 위조(T2): `GameResult`·`EscapeState`·`MonsterActorNumbers`·`SpyActorNumbers`·`WitchStrike`·`TimeAttackEndTime`·`GameEndTime`·`PaintPhaseEndTime`·`DoorStates` 등 | 모든 읽는 쪽이 `RoomState`로 그대로 믿음(`Core/RoomState.cs`), 쓴 사람 확인 없음 | 판 결과 조작, 남을 괴물·스파이로, 마녀 즉시 내리침 | **Critical** | 방장 감시·복구 + 핵심 소비자의 쓴 사람 확인(7.2) — P2 |
| **S-03** | 남의 플레이어 속성 위조(T3): `HitCount=2`(즉사)·`HitCount=0`(부활)·`Escaped=1` | `RoomState.IsBroken`(55)·`HasEscaped`(62)를 판정·관전·소리가 그대로 믿음 | 남을 즉사·부활 | **Critical** | 감시·복구(7.2) + 권위 이동(8장) — P2·P3 |
| **S-04** | **불사**: 잡혀도 자기 `HitCount`를 안 씀 | `Unit/HideOrSeekPlayer.cs` `RequestGrabKill` 137-177 — 파괴 확정을 **피해자 본인**이 함(154-159). 마녀 사망도 본인(`KillByWitch` 254-266, `Escape/EscapeCharacterState.cs` `DieByWitch` 89-95) | 괴물이 이길 수 없음 | **High** | 방장이 검증 후 피해자의 `HitCount`를 씀(8.1) — P3 |
| **S-05** | **탈출 위조**: 자기 `Escaped=1`만 씀 | `Monster/GameRuleController.cs:53`, `Monster/ResultScreenController.cs:132`, `Audio/MusicDirector.cs:94` — `RoomState.HasEscaped(p) ‖ 상태 목록`으로 판단 | 타지 않고 탈출 성공·판 조기 종료 | **High** | 판정은 `EscapeState`(방장 소유)만 사용(8.2) — P3 |
| **S-06** | 괴물이 거리·단계 무시하고 잡기 | `RequestGrabKill`은 "보낸 사람 = 괴물 주인"만 확인(140-146), 거리·단계·쿨다운 검사 없음(조준은 괴물 클라이언트 `MonsterGrabKillTrigger.FindAimTarget`) | 맵 어디서든 즉시 처형 | **High** | 방장이 거리·단계·쿨다운 검증(8.1) — P3 |
| **S-07** | 들기 RPC 위조·중복 들기(F-01) | `HideOrSeekPlayer.OnGrabbedByOwner` 103-108·`OnReleased` 110-115 발신자 미검증, `PlayerGrabController.TryGrab` 51-70 대상 상태 미확인 | 남을 강제로 들어 올리거나 떨어뜨림, 연쇄 캐리 | **High** | 9.1 — P4 |
| **S-08** | 남의 위장 색 칠하기·지우기(F-06①) | `ColorTag/PlayerPaintCanvas.cs` `OnEvent` 477-499: 내용의 `viewId`만 비교 | 위장 망치기 | Medium | 9.2 — P4 |
| **S-09** | 남을 괴물로 지원(F-06②) | `Monster/MonsterAssignmentAuthority.cs:34` 내용의 번호 사용 | 원치 않는 사람을 괴물로 | Medium | 9.2 — P4 |
| **S-10** | 멀리서 문 열기·닫기(F-06③) | `Environment/InteractableDoor.cs` `OnEvent` 149-156 | 문 괴롭히기 | Low | 9.2 — P4 |
| **S-11** | 채팅 위조·서식 주입 | `GameManager/GameManager.cs` `LogMsg` 105-129: **보낸 쪽이 만든 문자열**(닉네임·색 태그 포함)을 그대로 표시, 길이 제한 없음, `RoomExitController.cs:36` 나가기 문구도 보낸 쪽이 만든 문자열 | 다른 사람·시스템 사칭, 거대 글자로 화면 가리기 | Medium | 9.3 — P4 |
| **S-12** | 지역 이동 위조로 전원 추방 | `Lobby/GameLobbyController.cs` `OnRoomPropertiesUpdate` 124-129: `RegionMove` 키가 오면 쓴 사람 확인 없이 `RegionMove.Begin` → 전원 `LeaveRoom` | 대기실 전원 강제 퇴장 | **High** | 쓴 사람 = 방장일 때만(7.2·9.4) — P2·P4 |
| **S-13** | PUN `curScn` 위조로 씬 강제 이동 | PUN 자동 씬 동기화(`AutomaticallySyncScene`, `Lobby/LobbyController.cs:64`)는 PUN 내부에서 바로 `LoadLevel` — 게임 코드가 끼어들 틈 없음 | 진행 중 판을 엉뚱한 씬으로 | High | 결정 Q2(자체 씬 이동으로 교체 vs 위험 수용) — P2 후보 |
| **S-14** | 결과 화면 수치 위조(`CatchCount`·`DeathCause`) | 본인 쓰기(`MonsterGrabKillTrigger.cs:153-154`, `MonsterEscapeState.cs:29`) | 결과 표시만 | Low | 방장이 씀(8.1) — P3 |
| **S-15** | 위치 조작(순간이동·속도, T5) | 방장 거리 검사가 보고된 위치를 믿음(`EscapeAuthority.TrackPositions` 123-130, `IsNear` 584-591) | 장치·로켓·상자 원격 조작 | Medium | 비정상 이동 감지 → 요청 잠시 거부(10.8, 결정 Q3) — P5 |

검증이 이미 있는 경로(변경 없음): `EscapeRequest`(`EscapeManager.cs:251`), `EscapeNotice`·`ToolHit`(방장 발신만, `:271-278`), 도구 명중 거리(`EscapeAuthority.ValidTargets` 364-385), 시작(`GameStartAuthority.cs:49`), 방 설정(`RoomSettingsAuthority.cs:38`), 강퇴(`LobbyKick.Handle`·`IsForMe`), `RequestGrabKill`의 괴물 주인 확인(140-146).

---

## 3. 설계 원칙

1. **받는 쪽이 판단한다.** 이벤트·RPC는 `Sender`로 보낸 사람을 정하고, 내용 속 번호를 믿지 않는다(이미 `EscapeRequest`가 쓰는 원칙을 모든 경로로).
2. **권위 데이터는 방장만 쓴다.** 판정에 쓰는 값(사망·탈출·결과·역할·시각)은 방장이 검증해서 쓴 값만 믿는다. 본인이 쓰는 플레이어 속성은 "요청"이나 "표시"로만 쓴다.
3. **방장이 정직하면 남의 위조를 되돌린다.** 권위 키를 방장이 아닌 사람이 바꾸면 방장이 직전 값으로 복구하고 기록한다.
4. **방장 자격은 정책으로 정해져 있다**(`RoomState.DesiredMasterActor`: 괴물이 아닌 최선 입장자). 정책과 다른 사람이 방장이 되면 **자격자 본인이 즉시 되찾는다.**
5. **새 구조를 크게 들이지 않는다.** 지금 있는 방장 권위·Room Prop·순수 함수 규칙 구조를 그대로 쓰고, 검증은 **순수 함수**로 만들어 EditMode로 시험한다(예: `NetSecurityRules`).
6. **네트워크 형식이 바뀌면 `GameVersion`을 올린다**(`Lobby/LobbyController.cs:10`, 지금 `"1"`) — 옛 빌드와 같은 방에 섞이지 않게.

---

## 4. 단계 계획 개요

| 단계 | 내용 | F/S 항목 | 상태 |
|---|---|---|---|
| **P0** | 사전 검증: 변경 이벤트의 쓴 사람 확인, 2클라이언트 시험 환경 | (P2 설계 확정) | 🔁 설계로 대체 — 16장 M-2·M-3·M-7에서 확인 |
| **P1** | 기준선: 출시 씬의 개발용 부트스트랩 제거 → 시험 전부 통과 | F-02 | ✅ 2026-10-12 |
| **P2** | 보안 기반: 방장 되찾기, 권위 속성 감시·복구, 지역 이동·핵심 키의 쓴 사람 확인 | S-01, S-02, S-03, S-12, (S-13 결정) | ✅ 2026-10-12 (멀티 확인은 P9) |
| **P3** | 권위 이동: 사망·잡은 수·사망 원인을 방장이 확정, 탈출 판정은 탈출 상태만 | S-04, S-05, S-06, S-14 | ✅ 2026-10-12 (멀티 확인은 P9) |
| **P4** | 받는 쪽 검증: 들기, 칠하기, 괴물 지원, 문, 채팅, 강퇴용 전역 설정 | F-01(S-07), F-06(S-08~10), S-11, F-17 | ✅ 2026-10-12 (멀티 확인은 P9) |
| **P5** | 경합·정합성: 가마솥, 괴물 목록, 로켓, 첫 프레임, 위치 기록, 커밋 순서, 도달 불가 코드, 위치 조작 감지 | F-03, F-04, F-05, F-09, F-10, F-18, F-19, S-15 | ✅ 2026-10-12 (멀티 확인은 P9) |
| **P6** | 정리·성능: 할당, 미사용 UI·멤버, 머티리얼, 콜백 상속 | F-11, F-12, F-13, F-15, F-16 | ✅ 2026-10-12 |
| **P7** | 구조: 탈출 판정 하나로·중복 도우미, (선택) 캐릭터 프리팹 구성 | F-07, F-14, F-08 | ✅ 2026-10-12 (F-08 ⏸ 결정 Q6) |
| **P8** | Info 항목 처리(문서·작은 수정) | F-20~F-25 | ✅ 2026-10-12 |
| **P9** | 전체 시험·Play Mode·2클라이언트 시험·빌드·문서 | 전체 | ✅ 2026-10-12 (멀티 시험은 16장 — 사용자) |

순서 이유: P1이 먼저여야 이후 실패가 모두 "새 실패"로 보인다. P2·P3이 `HideOrSeekPlayer`·`GameRuleController`·`EscapeAuthority`를 크게 고치므로, 같은 파일을 만지는 P4·P5를 그 뒤에 둔다. P7(구조)은 동작이 바뀐 뒤 마지막에.

---

## 5. P0 사전 검증(스파이크) 🔁 설계로 대체
- **결과**: 이 환경에서는 2클라이언트를 띄울 수 없어 스파이크 대신, PUN 소스로 확인한 사실(`EventData.Sender`는 속성 콜백 **뒤에** 도착)에 맞춰 **두 방식을 함께** 넣었다 — ① 보낸 사람을 몰라도 동작하는 "방장 기댓값 대조·복구"(`AuthorityProps`), ② 보낸 사람을 알면 한 프레임 미뤄 확인(`RoomPropertyGuard.WrittenByMaster`, 지역 이동·결과 화면). P0-1·P0-2의 실제 동작은 16장 M-2·M-3·M-7이 확인한다. P0-3은 16장.

| # | 확인 | 방법 | 결과에 따른 분기 |
|---|---|---|---|
| P0-1 | 속성 변경 이벤트(253)의 `EventData.Sender`가 "바꾼 사람"인가 | 임시 스크립트(커밋하지 않음): `PhotonNetwork.NetworkingClient.EventReceived += e => { if (e.Code == 253) Debug.Log(e.Sender + " " + e[(byte)253]); }` — 빌드 1개 + 에디터 1개로 같은 방, 비방장이 `CurrentRoom.SetCustomProperties` 1회·`PlayerList[0].SetCustomProperties` 1회 | **된다** → 7.2 A안(쓴 사람 기반 복구). **안 된다** → 7.2 B안(값 서명 없이 "방장 기록 대조" 방식) |
| P0-2 | 비방장의 `SetMasterClient(자기)`가 실제로 통과하는가 | 같은 구성에서 비방장이 `PhotonNetwork.SetMasterClient(PhotonNetwork.LocalPlayer)` | 통과 → S-01 그대로 진행(예상). 서버가 거부 → S-01을 Low로 낮춤 |
| P0-3 | 2클라이언트 시험 절차 문서화 | `Builds/R1011Check` 빌드 + 에디터, 오프라인 대신 온라인 `kr` | 이후 단계의 "다중 클라이언트 확인" 공통 절차 |

---

## 6. P1 기준선 회복 — F-02 ✅ 2026-10-12
- **결과**: 6개 씬에서 각 1개(이름 `GameObject`, 컴포넌트 `OfflineModeBootstrap`만)를 지우고 저장, 남은 0개. `BuildSceneTests` 9/9 통과.

- **현재**: `Assets/Scenes/GameLobbyScene.unity`와 맵 5개에 비활성 `GameObject`(`Dev/OfflineModeBootstrap`, `autoCreateRoom: 1`)가 저장돼 `BuildSceneTests.ReleaseScene_HasNoDevScripts` 6건 실패.
- **수정**: 에디터 스크립트로 6개 씬을 열어 `OfflineModeBootstrap`가 붙은 오브젝트를 지우고 저장(씬의 다른 변경 없이). `PlayerTestScene`(빌드 꺼짐)의 2개는 유지.
- **재발 방지**: 이 환경의 자동 시험 도구(`play_offline`)는 씬을 저장하지 않게 유지하고, 시험 뒤 `stop`이 `__OfflineBoot`를 지웠는지 확인한다. 이미 있는 `BuildSceneTests`가 감시한다.
- **검증**: 전체 EditMode → 실패 0, 보류 1(F-13의 소리 ID — P6에서 해결).

---

## 7. P2 보안 기반 ✅ 2026-10-12
- **결과**: `Core/NetKeys.cs`(키마다 쓰는 사람 Master/Self), 새 `Core/NetSecurityRules.cs`·`Core/AuthorityProps.cs`·`Core/RoomPropertyGuard.cs`, `MasterClientPolicy` 되찾기. 방장 쓰기 18곳을 `AuthorityProps.SetRoom`으로. 지역 이동·결과 화면은 `RoomPropertyGuard.WrittenByMaster`로 한 프레임 미뤄 확인. `AuditFixTests` 통과. 되돌리기·되찾기는 쓰는 사람이 둘 이상이어야 하므로 P9 멀티 시험에서 확인.

### 7.1 S-01 방장 가로채기 되찾기 — `Monster/MasterClientPolicy.cs`
- **현재**(50-63): 현재 방장만 `EnforcePolicy`. 정책과 다른 방장이 나타나도 그 방장이 정책을 안 돌리면 그대로.
- **수정**:
  1. `OnMasterClientSwitched`(27-31)에서 **모든 클라이언트**가 `NetSecurityRules.ShouldReclaimMaster(currentMaster, desired, localActor, recentRequestedTarget)`를 계산한다 — 참이면 "정책상 자격자가 나인데 방장이 아니다".
  2. 참이면 자격자 본인이 `PhotonNetwork.SetMasterClient(PhotonNetwork.LocalPlayer)`(CAS: 현재 방장 번호) — 정상 경로(현재 방장이 정책으로 넘기는 것)와 같은 결과라 정직한 판에서는 아무 차이가 없다.
  3. **정상 교체와 구별**: 현 방장이 나가 서버가 아무나 고른 경우에도 결과는 같다(자격자가 가져감) — 문제없음. 정책상 정당한 넘김(`requestedActor`)은 대상이 곧 자격자이므로 되찾기가 일어나지 않는다.
  4. **반복 방지**: 같은 판에서 되찾기가 N번(예: 3) 넘으면 "방장 다툼"으로 보고 로그 + Q8 결정에 따라 처리(로그만 / 판 무효 후 대기실 복귀 / 가로챈 사람 `CloseConnection`).
  5. 괴물이 대기실에서 메시지 큐를 멈춘 동안(`MonsterLobbyWaitController`)은 괴물은 자격자가 아니므로(정책상 괴물 제외) 영향 없음.
- **새 순수 함수**: `Core/NetSecurityRules.cs`(새 파일) `ShouldReclaimMaster(int master, int desired, int local)` → EditMode 시험 4케이스.
- **영향**: `GameStartAuthority.IsIntendedMaster`(116-117)와 같은 정책 함수를 쓰므로 판단이 일치한다.

### 7.2 S-02·S-03 권위 속성 감시·복구 — 새 `Core/RoomPropertyGuard.cs`
- **목표**: 정직한 방장이 있는 동안, 방장이 아닌 사람이 **권위 키**를 바꾸면 되돌리고 기록한다. 핵심 소비자는 쓴 사람을 확인한다.
- **권위 키 목록**(`Core/NetKeys.cs`의 범위표를 재사용 — 이미 `Scope(Key, Target, Lifetime)` 표 `NetKeys.Scopes`가 있다, 88-121행):
  - 방(Room): `PaintPhaseEndTime`, `GameMapScene`, `SpyActorNumbers`, `MonsterActorNumbers`, `MonsterRevealTime`, `MonsterJoined`, `GameEndTime`, `ForcedPaint*`, `EscapeState`, `SpyEscapedAt`, `TimeAttackEndTime`, `WitchMode`, `WitchAppearAt`, `WitchStrike`, `GameResult`, `EscapeEndReason`, `RevealedSpies`, `MonsterDepartedAt`, `DoorStates`, `RoomTimeLimit`, `TimeAttackDuration`, `RegionMove`, `RegionMoveToken`.
  - 플레이어(Player): `HitCount`, `DeathCause`, `CatchCount`, `Escaped`(P3 이후 방장 전용).
  - 본인만 쓸 수 있는 플레이어 키: `SelectedSlot`, `SkinIndex`, `RegisteredSlotCount` — 남이 쓰면 복구.
  - `NetKeys`에 `Writer` 열(`Master`/`Self`)을 추가해 표 한 곳에서 관리한다(새 키도 표에 넣으면 자동 보호).
- **A안(P0-1에서 `Sender`가 채워지면)**:
  1. `RoomPropertyGuard : MonoBehaviour, IOnEventCallback`(영구 객체가 아니라 `GameSceneCore` 프리팹과 `GameLobbyScene`에 배치 — 방 안 씬 모두).
  2. `OnEvent(e)`에서 `e.Code == EventCode.PropertiesChanged(253)`만. `TargetActorNr == 0`이면 방, 아니면 그 플레이어.
  3. 판단은 순수 함수 `NetSecurityRules.IsAllowedWriter(key, writer, master, targetActor)`.
  4. **방장(정직)**: 허용되지 않은 쓰기면 그 키를 **방장이 기억한 마지막 정상 값**으로 다시 쓴다(값이 없었으면 null로 지움). 기억 값은 허용된 쓰기를 볼 때마다 갱신. `EscapeState`는 `EscapeAuthority.State`가 원본이므로 `Commit()`으로 다시 쓴다.
  5. **모든 클라이언트**: 위험한 즉시 동작을 하는 소비자는 같은 이벤트에서 쓴 사람을 확인한 뒤에만 반응 — `RegionMove`(전원 퇴장), `GameResult`(결과 화면), `WitchStrike`(사망 시작). 구현: 가드가 "마지막으로 이 키를 쓴 사람"을 `RoomPropertyGuard.LastWriter(key)`로 제공 → `GameLobbyController.OnRoomPropertiesUpdate`(124-129) 등은 `LastWriter == MasterClientId`일 때만 실행. **주의**: PUN은 캐시 반영(3512행) 후 콜백(`OnRoomPropertiesUpdate`) → 그 뒤 `EventReceived`(3579행) 순서다. 그래서 소비자는 콜백에서 바로 실행하지 말고 "다음 프레임 실행 + 그사이 가드가 쓴 사람을 기록"으로 한 프레임 미룬다(지역 이동·결과 화면은 한 프레임 늦어도 문제없다).
- **B안(`Sender`가 없으면)**: 방장이 권위 키를 쓸 때 **같은 요청에 순번 키**(`AuthSeq`, 증가 정수)를 함께 쓰고, 방장 클라이언트는 "내가 보낸 순번과 다른 변경"을 위조로 본다(방장만 판단 가능). 다른 클라이언트의 즉시 동작 확인은 불가 → `RegionMove`·`GameResult`는 방장이 되돌릴 때까지 1 RTT 노출을 받아들인다.
- **복구 폭주 방지**: 같은 키를 1초에 3번 넘게 되돌리면 로그 + Q8 처리. 위조자 번호를 `[NetGuard] actor N wrote KEY` 로그로 남긴다.
- **오프라인 모드**: `PhotonNetwork.OfflineMode`면 가드 비활성(쓴 사람이 늘 1).
- **영향**: 방장 교체 순간(옛 방장이 보낸 변경이 새 방장 화면에 늦게 도착) — 쓴 사람이 "직전 방장"이면 1초 유예(`NetSecurityRules`에 `previousMaster` 인자). 이 유예가 없으면 정상 결과 기록을 되돌릴 수 있다.

### 7.3 S-13 PUN 씬 키(`curScn`) — 결정 Q2
- PUN은 `curScn` 변경을 받으면 내부에서 바로 씬을 불러 게임 코드가 막을 수 없다.
- **선택지**: (A) 위험 수용 + 가드가 위조 기록만 남김 / (B) `AutomaticallySyncScene`을 끄고 방장 발신 이벤트(`NetEventCodes.LoadScene`, 새 코드)로 씬 이동을 직접 구현 — `RoomSceneTransition`(유일한 이동 경로)·`MonsterLobbyWaitController`(괴물만 동기화 끄기, 70-73)·`MonsterJoinController.RestoreSceneSyncWhenCaughtUp`(33-42)을 함께 바꿔야 해 규모가 크다.
- **추천: A(이번 범위)**, B는 다음 작업으로.

---

## 8. P3 권위 이동 ✅ 2026-10-12
- **결과**: 새 `Monster/GrabKillAuthority.cs`(방장이 거리·단계·간격 검증 후 HitCount·DeathCause·CatchCount를 씀). 쿠키·괴물 본인 쓰기 삭제, 마녀 사망은 `GameRuleController`가 씀, 탈출은 `EscapeState.HasEscaped/HasLeftMap`만. 오프라인 Play Mode 처단: 11.1초 파괴 → 13초 결과 → 대기실에서 판 키 정리 확인.

### 8.1 S-04·S-06·S-14 사망·잡은 수를 방장이 확정
- **현재**: 괴물 클라이언트 → `RequestGrabKill`(All) → 피해자 본인이 `HitCount=2`·`DeathCause`(`HideOrSeekPlayer.cs:154-159`), 괴물 본인이 `CatchCount+1`(`MonsterGrabKillTrigger.cs:153-154`). 마녀 사망은 각자 본인(`KillByWitch` 254-266, `MonsterEscapeState.DieByWitch` 24-32).
- **수정**:
  1. **잡기**: `RequestGrabKill`은 지금처럼 `RpcTarget.All`(연출용) 유지. 연출(`CookieLifeStatePresenter.BeginGrabKill`)은 모든 화면이 그대로.
  2. **방장이 확정**: 방장 클라이언트의 `RequestGrabKill` 처리에서 `NetSecurityRules.ValidateGrabKill(...)`로 검사 —
     - 보낸 사람 = 괴물 주인(기존 140-146),
     - 단계가 `Hunt` 또는 `TimeAttack`(`GamePhaseState.Current`),
     - 피해자가 살아 있고 탈출·탑승하지 않음(`EscapeManager.HasLeftMap` 대신 방장의 `EscapeAuthority` 상태),
     - **거리**: 방장 화면의 괴물·쿠키 위치 차이 ≤ `GrabReachFromFront + bodyFrontOffset + 여유 3 m`(`MonsterGrabKillTrigger.InReach` 89-95 재사용, 높이 `MaxGrabHeight`),
     - **쿨다운**: 같은 괴물의 직전 확정에서 `GrabKillCooldownSeconds` 이상(설정 SO에 이미 있는 처형 길이 사용, 없으면 추가).
     통과하면 **방장이** `victim.SetCustomProperties({HitCount:2, DeathCause:Monster})`, `monsterOwner.SetCustomProperties({CatchCount: n+1})`(방장이 세는 값 — 방장 로컬 사전으로 누적, 캐시 지연 무관).
  3. **피해자 본인**: `HitCount`를 쓰지 않는다. 대신 자기 `HitCount`가 2가 된 것을 보고(`CookieLifeStatePresenter.OnPlayerPropertiesUpdate` 195행 경로가 이미 있음) 물리 정지·아이템 떨구기·관전 전환을 한다(지금 154-176행에서 하던 "본인 처리"를 속성 수신 쪽으로 옮김). 아이템 떨구기(`RequestDropAll`)는 방장이 확정과 함께 `EscapeAuthority.DropAll`을 직접 부르면 더 단순하다(본인 요청이 필요 없어짐).
  4. **마녀**: `GameRuleController`가 `WitchStrike`를 쓴 뒤, 충격 시각(`WitchPresenter.StrikeImpactSeconds` + `ImpactGraceSeconds`)이 지나면 **방장이** 죽을 대상(처단이면 스파이, 타임어택이면 남은 쿠키·스파이, 괴물은 처단·타임어택 모두 — `WitchTimeline.CookieDiesInStrike` 재사용)을 계산해 각자의 `HitCount`/`DeathCause=Witch`를 쓴다. 본인 쪽 `DieByWitch`는 연출(숨김·관전)만.
  5. 가드(7.2)가 `HitCount`·`DeathCause`·`CatchCount`를 "방장 전용"으로 보호 → 남이 쓴 값은 되돌린다.
- **효과**: 불사(S-04)·원거리 처형(S-06)·즉사 위조(S-03)·결과 수치 위조(S-14)가 모두 막힌다(방장이 정직한 한).
- **비용**: 처형 확정이 방장 왕복만큼 늦다(지금도 연출이 먼저라 체감 차이 없음). 방장이 바뀌는 순간 처리 중이던 잡기는 새 방장이 다시 검사하지 않아 사라질 수 있다 → 괴물 화면은 확정을 못 받으면 1.5초 뒤 쿨다운을 풀어 다시 잡을 수 있게(`MonsterGrabKillTrigger.ResetTrigger` 159).
- **시험**: `NetSecurityRules.ValidateGrabKill` 표 시험(단계·거리·높이·쿨다운·탈출·이미 파괴).

### 8.2 S-05 탈출 판정은 `EscapeState`만
- **현재**: `GameRuleController.cs:53`, `ResultScreenController.cs:132`, `MusicDirector.cs:94`가 `HasEscaped(p) ‖ 상태 목록`.
- **수정**: 판정·결과·징글은 **`EscapeState`(방장 소유)의 `Escaped`/`Boarded`만** 본다(P7의 `EscapeState.HasLeftMap(actor)` 하나로 — 10줄 이내라 P3에서 먼저 만든다). `Escaped` 플레이어 속성은 숨김·소리 같은 표시용으로만 남기고, 가드가 방장 전용으로 보호한다. 본인 쓰기(`EscapeCharacterState.cs:54`)는 방장이 출발·탑승 확정 때 대신 쓰도록 `EscapeAuthority.TryDepart`(307-317)·`RocketBoard`(279-287)로 옮긴다.
- **영향**: `HideOrSeekPlayer.IsMovementLocked`(59-60)·`IsSpectatable`(72-74)·`EscapeCharacterState.ApplyHidden`(68-77)·`CookieAudio`(87)·`ChaseDirector`(87)는 표시라 그대로 둬도 되지만, 같은 함수를 쓰게 맞춘다.

---

## 9. P4 받는 쪽 검증 ✅ 2026-10-12
- **결과**: 들기(보낸 사람·거리·이미 들림·들고 있음·자기 자신 거절 + `OnGrabRejected` 되돌림), 칠하기(주인만·색칠 시간+2초·스탬프 상한), 괴물 지원(`Sender`·가마솥 3 m), 문(요청자 거리), 채팅(새 `GameManager/ChatText.cs` — 받는 쪽이 닉네임으로 꾸밈, `<noparse>`, 120자, 초당 2개), 강퇴 설정은 대기실을 떠날 때 끔, `GameVersion` "2". 오프라인 Play Mode: `<size=200>`·`</noparse>` 주입이 글자 그대로 표시, 초당 3번째 메시지 버림, 퇴장 문구는 인스펙터 한글 문구 사용, 자기 자신·없는 View로 든 요청 거절 확인. 시험 14/14.

### 9.1 F-01(S-07) 쿠키 들기
- `Unit/PlayerGrabController.cs` `TryGrab` 51-70: 대상 건너뛰기 조건 추가 — `target.CurrentCarrierViewId >= 0`(이미 들림), 대상이 들고 있음(`target` 쪽 `PlayerGrabController.IsCarrying` — 동기화 값 `PlayerNetworkSync.RemoteIsCarrying` 사용), 대상이 탈것·탈출(`HasLeftMap`), 대상 기절 중은 허용(현재와 같음).
- `Unit/HideOrSeekPlayer.cs`:
  - `OnGrabbedByOwner(int newCarrierViewId, PhotonMessageInfo info)`(103-108): ① `info.Sender`가 `PhotonView.Find(newCarrierViewId).Owner`와 같은지 ② 이미 들려 있으면 무시 ③ 내가 누군가를 들고 있으면(`grabController.IsCarrying`) 무시 ④ 드는 쪽과 거리 ≤ `grabRange + 1.5 m`(동기화 지연 여유) ⑤ 단계가 `Paint`가 아님(현재 규칙과 같게 — 확인 필요: 색칠 시간에 들기가 허용되는지 `PlayerInput.GrabPressed` 경로 확인 후 맞춤).
  - **거절 통지**: 무시했으면 드는 쪽에게 `OnGrabRejected` RPC(새, 대상: 보낸 사람) → 드는 쪽 `PlayerGrabController`가 `carriedPlayer`를 비우고 자세·충돌 무시를 되돌린다(`Release`의 로컬 부분만). 드는 쪽은 **응답 전 0.6초 동안만 임시로** 들고 있는 것처럼 보이고(지금의 `SetLocalCarrierOverride`와 같은 시간), 거절이 오면 되돌린다.
  - `OnReleased(PhotonMessageInfo info)`(110-115): `info.Sender`가 지금 나를 든 사람(`carryFollower.CarrierViewId`의 Owner)일 때만.
- 순수 함수: `NetSecurityRules.CanAcceptCarry(isBroken, isCarried, isCarrying, senderOwnsCarrier, distance, maxDistance)`.
- 시험: 표 시험 + 2클라이언트(14장 E-2: A가 C를 든 상태에서 B가 C 잡기 → 거절, A 내려놓기 → B와 무관).
- RPC 서명이 바뀌므로 `GameVersion` "1" → "2".

### 9.2 F-06(S-08~S-10) 이벤트 발신자
1. **칠하기** `ColorTag/PlayerPaintCanvas.cs` `OnEvent` 477-499 — 26행 뒤에 `if (!PhotonNetwork.OfflineMode && (pv.Owner == null || photonEvent.Sender != pv.Owner.ActorNumber)) return;`. 또 색칠 단계가 아닐 때(강제 도포 `ForceFill` 제외) 받은 획은 무시(`GamePhaseState.Current == GamePhase.Paint`), 한 이벤트의 스탬프 수가 상한(같은 클래스의 `maxStampsPerEvent`, 32행, 기본 64)을 넘으면 무시.
2. **괴물 지원** `Monster/MonsterAssignmentAuthority.cs:34` — `int claimantActorNumber = photonEvent.Sender;`로 바꾸고 `CustomData`는 무시(`Cauldron.cs:31`은 그대로 둬도 무방, 정리 차원에서 `null` 전송으로). 추가로 지원자가 **가마솥 근처**인지(방장 화면의 그 사람 캐릭터와 가마솥 거리 ≤ 가마솥 콜라이더 반경 + 2 m) 확인 — `Cauldron`을 `[SerializeField]`로 참조.
3. **문** `Environment/InteractableDoor.cs` `OnEvent` 149-156 — 요청자 캐릭터(`CharacterRegistry`에서 `View.Owner.ActorNumber == photonEvent.Sender`)와 문(`leafCenter`) 거리 ≤ 상호작용 거리 + 2 m일 때만 `WriteState`. 요청자 캐릭터가 없으면 무시.

### 9.3 S-11 채팅
- `GameManager/GameManager.cs`:
  - 보내기(`BroadcastingChat` 132-146): **본문만** 보낸다 — `pv.RPC(RpcLogMsg, RpcTarget.All, kind, text)` (`kind`: 0 채팅, 1 접속, 2 나감).
  - 받기(`LogMsg` 105-129): 표시 문자열을 **받는 쪽이** 만든다 — 이름은 `info.Sender.NickName`(없으면 `#번호`), 색·형식은 인스펙터 문구(`[SerializeField]` 형식 문자열 3개 — 지금 코드의 `"<color=#ffffff>[{0}] {1}</color>"` 등을 인스펙터로 옮김), 본문은 `<noparse>`로 감싸 TMP 서식 주입 차단(`</noparse>` 문자열은 제거), 길이 상한 120자, 사람당 초당 2개 제한(넘치면 무시).
  - 접속·나감 문구(`SendConnectedMessageWhenInRoom` 44-54, `RoomExitController.cs:36`)도 `kind`만 보낸다.
- 순수 함수: `ChatText.Sanitize(string, int max)` + 시험.
- RPC 서명 변경 → `GameVersion`(9.1과 함께 한 번).

### 9.4 S-12 지역 이동
- `Lobby/GameLobbyController.cs` `OnRoomPropertiesUpdate` 124-129: `RegionMove`는 7.2의 `RoomPropertyGuard.LastWriter(NetKeys.RegionMove) == MasterClientId`일 때만, 한 프레임 미뤄 실행(7.2 A안 주의 참고). B안이면 방장이 위조를 되돌리기 전 1 RTT 동안 `Begin`이 실행될 수 있으므로, **추가로** `RegionMove` 값에 포함된 표(token)를 방장이 같은 요청에서 쓰는 `RegionMoveIssuedBy=방장 번호`와 대조(위조자는 자기 번호를 쓰게 되므로 `MasterClientId`와 다르면 무시).

### 9.5 F-17 강퇴용 전역 설정
- `Lobby/GameLobbyController.cs:42` `Awake`에서 켜는 `PhotonNetwork.EnableCloseConnection`을 `OnDestroy`에서 `false`로. 강퇴 대상은 대기실에 있을 때만 끊기면 된다.

---

## 10. P5 네트워크 경합·정합성 ✅ 2026-10-12
- **결과**: F-03·F-04는 대기열 대신 **방장이 마지막으로 보낸 목록**(`AuthorityProps.TryGetExpectedRoom`)에서 이어 계산하는 새 `Monster/MonsterRoster.cs`로 해결(응답 전 두 번째 지원·두 번째 퇴장도 바로 반영, 클라이언트 변경 없음). F-05 `launchRequested`, F-09 `SceneLoadGrace` 대기, F-10 모든 클라이언트 0.2초 간격 기록, F-18 `EscapeState.Revision`(FormatVersion 4), F-19 `Give` void·훔침 순서, S-15 불가능한 이동 → `[NetGuard]` 로그 + 3초 요청 거부. 시험 18/18, `EscapeTests` 52/52. 오프라인 Play Mode: 40 m 순간이동 감지, Revision 1→2, 로켓 두 번 호출 → 발사 로그 1회.
- **알려진 한계**: 낙하 복귀(`FallGuard`) 순간이동도 감지되어 3초간 탈출 요청이 거부된다(로그·일시 거부만이라 판에는 영향이 작다, 결정 Q8).

### 10.1 F-03 가마솥 동시 지원 — `Monster/MonsterAssignmentAuthority.cs`
- `confirmRequested`(19행) 중에 온 지원을 버리지 않고 `pendingClaims`(`HashSet<int>`)에 모은다(9.2에서 `Sender` 기반으로 바뀐 번호).
- `OnRoomPropertiesUpdate`(104-107)에서 `MonsterActorNumbers`가 갱신되면 `confirmRequested=false` 후 `pendingClaims`에서 아직 자리가 남았고(`RoomState.CanSelectMonster`) 방에 있고 괴물이 아닌 사람부터 다시 `ConfirmMonsters(AppendMonsters(...))`.
- `AppendMonsters`(109-113)는 캐시가 아니라 **방장이 마지막으로 보낸 목록**(로컬 필드)을 기준으로 — 응답 전 캐시 문제 제거.
- 결정 Q7(대안: 가마솥이 안에 있는 동안 2초마다 재전송) — 추천은 방장 쪽 대기열(클라이언트 변경 없음).

### 10.2 F-04 괴물 목록 — `Monster/RoomLifecycleWatcher.cs` 13-36, `Monster/MonsterAssignmentAuthority.cs` 61-75
- 남은 괴물 = `MonsterRoster.Remaining(lastSent ?? cached, presentActors)` — **현재 방 인원**(`CurrentRoom.Players.Keys`)과의 교집합. 퇴장한 사람이 여럿이어도 한 번에 정확한 목록.
- 방장 로컬 `lastSentMonsters`를 두어 응답 전에 두 번째 퇴장이 와도 이어서 계산.
- 순수 함수 `MonsterRoster.Remaining(int[] monsters, IEnumerable<int> present)` + 시험(동시 2명 퇴장 케이스).

### 10.3 F-05 로켓 발사 — `Escape/EscapeAuthority.cs` `TryLaunchRocket` 424-448
- `launchRequested` 필드(25행 `purgeRequested` 옆)를 두고 426행 가드에 추가. 방장 교체로 새 `EscapeAuthority`… 는 아니다(같은 객체가 방장 전환 후에도 남는다 — `EscapeManager.authority`는 씬 수명) → `Adopt`(120) 때 `launchRequested = RoomState.TryGetDouble(SpyEscapedAt, out _)`로 맞춘다.
- 두 번째 호출이 막히므로 "첫 쓰기의 타임어택 키가 남는" 경우도 사라진다.

### 10.4 F-09 첫 프레임 경합 — `Monster/GamePhaseStarter.cs` 13-30
- P0과 함께 확인(14장 E-1). 수정은 단순화: 키가 없을 때 바로 쓰지 말고, **씬에 들어온 뒤 `GameSettings.SceneLoadGrace`(이미 있는 값) 동안 기다린 후에도 없을 때만** 쓴다. 정상 흐름(대기실 경유)에서는 절대 쓰지 않게 된다.

### 10.5 F-10 위치 기록 — `Escape/EscapeManager.cs:134`, `Escape/EscapeAuthority.cs` `TrackPositions` 123-130
- `authority.TrackPositions()`를 방장 여부와 무관하게 **모든 클라이언트**가 0.2초마다(매 프레임 → 간격) 실행. 비용은 캐릭터 수(≤8)만큼 사전 쓰기.
- 새 방장의 첫 프레임 "모르면 통과"(580·586행)가 사라진다.

### 10.6 F-18 커밋 순서 — `Escape/EscapeState.cs`, `EscapeAuthority.Commit` 619-624, `EscapeManager.OnRoomPropertiesUpdate` 170-177
- `EscapeState`에 `Revision`(int) 추가, `FormatVersion` 3 → 4(19행), `Commit`마다 `State.Revision++`.
- `ApplyLocalState`(191-198): 방장 화면에서 받은 상태의 `Revision`이 지금 화면 것보다 작으면 무시. 비방장은 항상 적용.
- 시험: `EscapeTests`의 인코딩 왕복 시험(143·391행)에 `Revision` 포함 확인 추가.

### 10.7 F-19 도달 불가 코드 — `Escape/EscapeAuthority.cs`
- `Give`(467-481)를 `void`로, `Steal`(255-257)·`PickUp`(177)·`OpenChest`(168)의 반환 처리 정리. `Steal`은 칸 비우기(255-256)를 `Give` 뒤로 옮겨 실패 가능성이 생겨도 불일치가 없게.

### 10.8 S-15 위치 조작 감지 — 결정 Q3
- `EscapeAuthority.TrackPositions`(10.5에서 모든 클라이언트가 돌지만 판단은 방장)에서 `NetSecurityRules.IsPlausibleMove(prev, now, dt, maxSpeed)` — 최대 속도는 `GameSettings`의 달리기·돌진 속도 × 1.5 + 순간이동 허용 경로(낙하 복귀 `FallGuard`, 탈것 하차, 들기 내려놓기)는 예외 신호로 표시.
- 비정상이면 그 사람의 탈출 요청을 3초간 거부 + `[NetGuard]` 로그. 강퇴·판 무효는 Q8에 따름.
- 추천: **넣는다(로그·일시 거부만)**.

---

## 11. P6 정리·성능 ✅ 2026-10-12
- **결과**: F-11 `RoomState.HostActor/DesiredMasterActor`는 `Players.Keys` 한 번 순회, 새 `RoomState.Players`(할당 없는 순회)로 매 프레임·방장 루프 9곳 교체, `EscapeHud` 탑승 문구는 숫자가 바뀔 때만. F-12 `GameSceneCore`에서 배너 2개·`SettingsButton`·색 패널 `TimeLabel`·`GameRuleManagers`의 PhotonView 삭제(에디터 스크립트, 참조는 부모의 자식 목록뿐이었음), `EscMenuBuilder`에 `withSettings`(대기실만). 변장 시간 째깍 소리가 두 번 나던 것도 함께 사라짐. F-13 getter 22개·문구 5개(`EscapeTexts` 에셋 다시 저장)·소리 ID 3개 삭제 + 카탈로그 다시 만들기 → `AudioTests.AllIds_HaveClips` 보류 → 통과. F-15 `Tint` 재질 공유 캐시, `EscapeExitFx` 렌더러 캐시 + `MaterialPropertyBlock`, `SpyRocket` 칸·회전 부품 캐시. F-16 6개 클래스 `MonoBehaviour`로(`HideOrSeekPlayer`는 P3에서 `OnPlayerPropertiesUpdate`를 쓰게 되어 유지).
- **검증**: 전체 EditMode 283개 중 실패 2개(둘 다 디스크에 없는 `PlayerTestScene.unity` — 이번 작업과 무관, 사용자 확인 필요), 빌드 씬 7개 누락 스크립트 0. 오프라인 Play Mode: 칸 수 표시·변장 시간(위쪽 하나만)·호스트 계산·같은 색 재질 공유 확인, 콘솔 오류·경고 0.

### 11.1 F-11 `PlayerList` 반복 할당
- `Core/RoomState.cs` `HostActor`(99-105)·`DesiredMasterActor`(115-122): `PhotonNetwork.PlayerList`를 반복문 안에서 다시 부르는 것을 없애고 `CurrentRoom.Players.Keys`를 한 번 순회(정렬 불필요 — 최솟값만 구함). 할당 0.
- 매 프레임 루프: `Escape/EscapeManager.cs:152`, `Monster/GameRuleController.cs:50·108`, `Escape/EscapeHud.cs:256` → `CurrentRoom.Players.Values` 순회. 결과 화면·징글처럼 **순서가 필요한 1회성**은 그대로.
- `EscapeHud.RefreshBoarding`(244-262)은 숫자가 바뀔 때만 `string.Format`.

### 11.2 F-12 `GameSceneCore` 미사용 UI
- ① `ResultScreen/MonsterWinBanner`·`CookieWinBanner` 삭제 + `Monster/ResultScreenController.cs`의 `monsterWinBanner`·`cookieWinBanner`(17-18)·`HideWinBanners`(188-192)와 호출(86·153) 삭제.
- ② `EscMenu/.../SettingsButton` 삭제 + `Assets/Editor/UI/EscMenuBuilder.cs` 108-109행이 **`GameSceneCore`용으로는** 설정 버튼을 만들지 않게(`BuildInto`에 `withSettings` 인자 — 대기실만 true).
- ③ `GameRuleManagers`의 PhotonView 삭제(스크립트 7개 모두 미사용 확인). 삭제 뒤 맵 씬 6개를 열어 ViewID 경고 없음 확인.
- ④ 변장 시간 두 번 표시 — 결정 Q4(추천: 색 패널 아래 `TimeLabel` 삭제 + `ColorTag/ColorSelectionPanel`의 `timeLabel` 필드·갱신 코드 정리).
- 프리팹 변경은 에디터 스크립트로(직접 YAML 수정 금지).

### 11.3 F-13 미사용 멤버·데이터
- 진단용 getter 22개, 미사용 메서드 4개(`GameAudio.StopMusic`, `SoundSettingsPanel.PickOutput`, `RoomSettingField.StepBy`, `ItemSO.EditorSetVisuals` — 결정 Q5), 읽지 않는 문구 5개(`EscapeTextsSO` 18·19·28·30·41 — SO 에셋의 값도 함께 사라짐), 소리 ID 3개(`SoundId.CookieCarried`·`SpectateSwitch`·`MonsterWaitTick` — 번호는 비워 두고 항목만 삭제, 카탈로그 다시 만들기 → `AudioTests.AllIds_HaveClips` 통과).
- 삭제 전 컴파일러로 재확인(이름이 같은 다른 멤버 주의), 삭제 후 전체 시험.

### 11.4 F-15 머티리얼·매 프레임 탐색
- `Escape/EscapeVisuals.cs` `Tint`(73-80): 같은 (색, 발광) 조합은 **정적 캐시**에서 재사용(`Dictionary<(Color,float,bool), Material>`), 씬 언로드 시 캐시 비움(`SceneManager.sceneUnloaded`). 도구 쓸 때마다 새 재질이 생기지 않는다.
- `Escape/EscapeExitFx.cs` `Tick`(33-46): `Glow` Transform과 렌더러 배열을 장치별로 한 번만 찾고(`EscapeDevice`가 보관), `r.material` 대신 `MaterialPropertyBlock`으로 색·발광 설정 → 재질 복제 없음.
- `Escape/SpyRocket.cs` `Update`(95-103): `Slot_nn_Filled` Transform을 `Awake`에서 배열로 캐시.

### 11.5 F-16 콜백 없는 PUN 상속 8개
- `BrushCursorController`, `ColorSelectionPanel`, `GameManager`, `GamePhaseStarter`, `MonsterController`, `MonsterJoinController`, `SpectatorController`, `HideOrSeekPlayer` → `MonoBehaviour`. 8개 모두 `photonView` 속성을 쓰지 않음(확인). `override` 붙은 `OnEnable`/`OnDisable`(`BrushCursorController:22`, `GameManager:86`, `MonsterController:116·122`, `HideOrSeekPlayer:267·273`)은 `private void`로 바꾸고 `base` 호출 제거. `IPunObservable`은 상속과 무관하게 유지.
- **주의**: `GameManager`는 P4에서 RPC를 바꾸므로 같은 단계에서 함께.

---

## 12. P7 구조 · P8 Info

### 12.1 F-07 + F-14 "탈출했나" 하나로, 중복 도우미 ✅ 2026-10-12
- **결과**: 기준은 `EscapeState.HasEscaped/HasLeftMap` 하나 — 다른 도메인은 `EscapeManager.HasLeftMap/HasEscaped`만 부른다(쿠키·관전·소리·징글·추격 음악·처형 확정). 본인 쓰기 → 방장 쓰기로 바뀌었던 플레이어 키 `Escaped`는 읽는 곳이 없어져 `NetKeys`·`RoomState.HasEscaped`·`MarkEscaped`와 함께 삭제. 파티클 6곳 → `EscapeVisuals.CreateParticles(ParticleStyle)`, 승객 인형 3곳 → `EscapeVisuals.CreatePassenger`, `FindDeep` → `EscapeSequence.FindDeep`. 값은 그대로 옮겼다(Play Mode에서 로켓 연기 40개/초·최대 120 확인).
- `Escape/EscapeState.cs`에 `HasLeftMap(int actor)`(대기·탈출·탑승), `HasEscaped(int actor)`(탈출·탑승) 추가(P3에서 먼저 만든 것을 여기서 전 사용처로).
- 사용처 교체: `EscapeManager.HasLeftMap/IsWaiting`(53-56), `EscapeAuthority.IsActiveEscaper`(462), `GameRuleController`(53), `ResultScreenController`(132), `EscapeAudioPresenter`(77-78), `MusicDirector`(94).
- 파티클 6곳 → `EscapeVisuals.CreateSmoke(Transform parent, SmokeStyle style)`(스타일 SO 없이 값 구조체), `FindDeep` 2벌 → `EscapeSequence.FindDeep` 하나, 승객 인형 3벌 → `EscapeVisuals.CreatePassenger(Transform parent, float scale)`.
- 다른 도메인이 `EscapeManager` 정적 조회를 쓰는 것은 유지(규모상 이득 적음) — 대신 조회 함수가 위 메서드 하나만 부르게.

### 12.2 F-08 캐릭터 프리팹 구성 — 결정 Q6 ⏸
- 추천: **이번 범위에서 제외**(P2·P3로 `HideOrSeekPlayer`가 크게 바뀐 직후라 회귀 위험이 큼). 별도 계획으로.

### 12.3 Info F-20~F-25 ✅ 2026-10-12
- **결과**: F-20 새 방 키 `WitchStrikeAt`(내리친 시각)로 경과를 재 방장이 바뀌어도 이어진다(도구 쿨다운·훔치기 기록은 그대로 — 문서화). F-21 `EscapeManager.recipeOverride`를 맵 5개에, `OvenSequence.mapDoor`를 베이커리에 연결(`EscapeMapSetup.BindSceneReferences` — 맵 도구가 앞으로도 채운다, 비어 있으면 이름으로 찾고 경고). F-22 `NetworkDefaults`의 `RuntimeInitializeOnLoadMethod`에서 `SerializationRate` 설정(로비 전용 설정 삭제). F-23 2클라이언트 절차는 13.3·16장 체크리스트. F-24 변경 없음. F-25 P3에서 해소.
| ID | 처리 |
|---|---|
| F-20 방장 교체 시 마녀 경과·도구 쿨다운·훔치기 기록 초기화 | 마녀 경과는 방장 로컬 시간 대신 `WitchStrike`를 쓴 시각을 함께 기록(`WitchStrikeAt`, Room Prop)해 새 방장도 같은 기준으로 — `GameRuleController`(18·64). 도구 쿨다운·훔치기 기록은 현재대로(문서화) |
| F-21 씬 이름·오브젝트 이름 의존 | `EscapeManager`의 레시피는 `recipeOverride`(19-20)를 맵 5개에 채워 이름 규칙 의존 제거, `OvenSequence`의 문(78)은 `[SerializeField]`로 — 맵 도구(`EscapeMapSetup`)가 채우게 |
| F-22 `SerializationRate` | `GameSettings`를 읽는 정적 초기화(`AudioRuntime.Boot`와 같은 `RuntimeInitializeOnLoadMethod`)에서 한 번 설정 — 어느 씬에서 시작해도 같은 값 |
| F-23 PlayMode·다중 클라이언트 시험 없음 | 이번 계획의 2클라이언트 절차(P0-3)를 `Plan.md`에 체크리스트로 남김. 자동화는 범위 밖 |
| F-24 큰 클래스 | 변경 없음(문서화) |
| F-25 잡은 수 읽고-더해-쓰기 | P3에서 방장 누적으로 바뀌며 해소 |

---

## 13. P9 시험·Play Mode·빌드·문서, 회귀 위험 ✅ 2026-10-12 (멀티는 16장 — 사용자)
- **EditMode**: 전체 283개 **모두 통과**. 처음 실패 2개는 빌드 목록에 남아 있던, 디스크에 없는 `Assets/Scenes/PlayerTestScene.unity` 때문이었다 → 사용자 결정으로 빌드 목록에서 뺐고, `RuleTests`는 그 씬이 있을 때만 검사하도록 바꿨다. 그러자 가려져 있던 실패 1개가 드러났다: P6에서 지운 `GameRuleManagers` PhotonView의 `sceneViewId` 오버라이드가 맵 씬 5개에 남아 있던 것 → 남은 오버라이드만 지웠다(Unity의 "Remove unused overrides"와 같음). `AuditFixTests` 18개 새로 추가.
- **오프라인 Play Mode**(캔디숲·베이커리): 채팅 서식 차단·도배 제한, 들기 거절, 위치 조작 감지, Revision, 로켓 1회 발사, 처단(내리침 13.6초 → 부서짐 14.6초 → 결과 16.1초, 방장 확정), 쿠키 탈출(8초 결과), 로켓 타임어택(마녀 등장·로켓 상승), 베이커리 오븐 문(직접 연결)·빛 가루, 변장 시간 표시 하나, 콘솔의 게임 코드 오류·경고 0(시험용 감시 스크립트의 NRE 1건은 씬 전환 뒤 그 스크립트 자체의 것).
- **빌드**: Windows 64 `Builds/AuditFixCheck/TagOfChaos.exe` — 성공, 오류 0, 경고 2(기존 `NotoSansKR SDF` 임포트 경고, PUN 재컴파일 안내), 씬 7개, 359 MB, 40초.
- **시험 메뉴**: `Assets/Editor/Net/NetTamperMenu.cs`(에디터 전용) — 16장 멀티 시험에서 조작된 클라이언트 역할.

### 13.1 새 EditMode 시험(파일: `Assets/Editor/Tests/AuditFixTests.cs`)
- `NetSecurityRules.ShouldReclaimMaster`, `IsAllowedWriter`(키 표 전체), `ValidateGrabKill`(단계·거리·높이·쿨다운·탈출·파괴), `CanAcceptCarry`, `IsPlausibleMove`
- `MonsterRoster.Remaining`(동시 2명 퇴장), `ChatText.Sanitize`(서식·길이·`</noparse>` 제거)
- `EscapeState` `Revision` 왕복·무시 규칙, `HasLeftMap/HasEscaped`
- `NetKeys` 범위표: 모든 권위 키에 `Writer`가 지정됐는지
- 기존: `BuildSceneTests` 6건 통과(P1), `AudioTests.AllIds_HaveClips` 통과(P6), 전체 실패 0 목표

### 13.2 Play Mode(오프라인, 이 환경)
- 탈출·로켓·처단 세 경로를 이전과 같은 시나리오(`k7_*`)로 다시 실행해 결과·연출·소리 회귀 없음 확인. 잡기·마녀 사망이 방장 확정으로 바뀐 뒤에도 사망 연출·관전·결과 화면이 같은지.

### 13.3 2클라이언트(빌드 + 에디터) — 사용자 환경 필요
- E-1 첫 프레임, E-2 들기 거절, E-3 가마솥 동시 지원(8인 필요 — 가능한 범위), E-4 괴물 동시 퇴장, 보안: 시험용 개발 메뉴(에디터 전용 `#if UNITY_EDITOR`, 빌드 제외)로 ① 남의 `HitCount` 쓰기 → 되돌아감 ② 방 `GameResult` 쓰기 → 되돌아감·결과 화면 안 뜸 ③ `RegionMove` 쓰기 → 아무도 안 나감 ④ `SetMasterClient(자기)` → 자격자가 되찾음 ⑤ 남의 ViewID로 `PaintStroke` → 무시 ⑥ 가짜 채팅 서식 → 글자 그대로 표시.

### 13.4 회귀 위험·주의
- **네트워크 형식 변경**: RPC 서명(들기·채팅), `EscapeState.FormatVersion` 4 → `GameVersion` "2"로 올려 옛 빌드와 분리.
- **사망 확정 지연**: 방장 왕복만큼 늦다 — 연출이 먼저라 체감 없음 예상, Play Mode로 확인.
- **방장 교체 순간**: 가드의 "직전 방장 1초 유예", 되찾기 반복 제한, 처리 중 잡기 유실(괴물 쿨다운 해제)로 대비.
- **정직한 방장 가정**: 조작된 방장 동안은 막지 못한다(1장). 근본 해결(서버 판정)은 별도 결정 사항.
- **씬 키(`curScn`)**: Q2에서 A를 고르면 위험이 남는다.
- **프리팹 변경**: `GameSceneCore`를 쓰는 씬 6개 — 에디터 스크립트로 변경 후 씬을 열어 누락 참조 확인.

### 13.5 문서
- 단계마다 이 문서 상태 갱신, 끝나면 `research.md`에 7차 판 요약(F-항목 상태표만 갱신, 6차 본문 보존), `Plan.md/README.md`에 이 문서 한 줄.

---

## 14. 결정이 필요한 질문

| # | 질문 | 선택지 | 추천 |
|---|---|---|---|
| **Q1** | 보안 수정 범위 | A 받는 쪽 검증만(P4) / B A + 감지·복구·방장 되찾기(P2) / **C B + 권위 이동(P3)** | **C** — 불사·탈출 위조·즉사 위조를 실제로 막는 건 P3 |
| **Q2** | PUN 씬 키(`curScn`) 위조 | **A 위험 수용, 기록만** / B 자동 씬 동기화를 끄고 방장 이벤트로 직접 구현 | **A**(이번), B는 별도 작업 |
| **Q3** | 위치 조작 감지(S-15) | **넣기(로그 + 3초 요청 거부)** / 넣지 않기 | **넣기** |
| **Q4** | 변장 시간 두 번 표시(F-12 ④) | **색 패널 아래 숫자 삭제** / 위 표시 삭제 / 둘 다 유지 | **아래 숫자 삭제** |
| **Q5** | 미사용 공개 메서드 4개(`StopMusic`, `PickOutput`, `StepBy`, `EditorSetVisuals`) | 삭제 / **유지** | **유지**(공개 API, 해가 없음) — 진단용 getter 22개만 삭제 |
| **Q6** | F-08 캐릭터 프리팹 구성 이전 | 이번에 / **다음 작업** | **다음 작업** |
| **Q7** | F-03 가마솥 동시 지원 | **방장 대기열** / 가마솥 재전송 | **방장 대기열** |
| **Q8** | 위조·방장 다툼을 감지했을 때 | **로그 + 되돌리기만** / + 반복(3회) 시 그 사람 연결 끊기 / + 판 무효 후 대기실 | **로그 + 되돌리기**, 반복 시 연결 끊기는 선택 |
| **Q9** | `GameVersion`을 "2"로 올려 옛 빌드와 분리 | **예** / 아니오 | **예** — RPC·상태 형식이 바뀐다 |

---

## 15. F-ID ↔ 단계 대응표

| F | 내용(요약) | 단계 | 절 |
|---|---|---|---|
| F-01 | 들기 수락·발신자 | P4 | 9.1 |
| F-02 | 출시 씬 개발용 부트스트랩 | P1 | 6 |
| F-03 | 가마솥 동시 지원 | P5 | 10.1 |
| F-04 | 괴물 동시 퇴장 목록 | P5 | 10.2 |
| F-05 | 로켓 발사 가드 | P5 | 10.3 |
| F-06 | 이벤트 발신자 3종 | P4 | 9.2 |
| F-07 | 정적 조회 결합 | P7 | 12.1 |
| F-08 | 런타임 구성 | P7(⏸ Q6) | 12.2 |
| F-09 | 첫 프레임 경합 | P0·P5 | 5, 10.4 |
| F-10 | 방장 교체 시 위치 기록 | P5 | 10.5 |
| F-11 | PlayerList 할당 | P6 | 11.1 |
| F-12 | GameSceneCore 미사용 UI | P6 | 11.2 |
| F-13 | 미사용 멤버·데이터 | P6 | 11.3 |
| F-14 | 중복 로직 | P7 | 12.1 |
| F-15 | 머티리얼·매 프레임 탐색 | P6 | 11.4 |
| F-16 | 콜백 없는 PUN 상속 | P6 | 11.5 |
| F-17 | 강퇴 전역 설정 | P4 | 9.5 |
| F-18 | 커밋 순서 | P5 | 10.6 |
| F-19 | 도달 불가 코드 | P5 | 10.7 |
| F-20~F-25 | Info | P8 | 12.3 |
| (신규) S-01~S-05, S-11~S-15 | 클라이언트 조작 | P2·P3·P4·P5 | 7~10장 |

---

## 16. 멀티 시험 절차(사용자 — PC 4대) ⬜

> 이 환경은 오프라인 1인 Play Mode만 가능해, 쓰는 사람이 둘 이상이어야 드러나는 동작(되돌리기·방장 되찾기·거절 통지·동시 퇴장)은 여기서 확인한다.
> 준비: `GameVersion`이 "2"로 올라 **옛 빌드와는 방이 보이지 않는다** — 4대 모두 이번 빌드를 쓴다. 한 대는 **에디터 Play Mode**(시험 메뉴 `Tools/TagOfChaos/Net Tamper Test/` — 에디터 전용, 빌드에 없음), 나머지 3대는 빌드 .exe. 에디터 쪽 Console에서 `[NetGuard]`·`[GrabKill]`·`[NetTamper]` 로그를 본다. 방장(빌드 PC 중 방을 만든 사람)의 로그는 빌드의 `Player.log`(%USERPROFILE%\AppData\LocalLow\<회사>\<제품>\Player.log)에 남는다.

| # | 장면 | 하는 것 | 기대 결과 |
|---|---|---|---|
| M-1 | 대기실 | 빌드 A가 방을 만들고 B·C·에디터 D가 들어온다. 채팅에 `<size=200>크게</size>`, `</noparse><b>굵게` 입력 | 모든 화면에서 태그가 **글자 그대로** 보임. 이름은 각자 화면이 보낸 사람 닉네임으로 붙임. 1초에 3번 이상 보내면 3번째부터 안 보임 |
| M-2 | 대기실 | D: 메뉴 `3 Write RegionMove` | **아무도 방을 나가지 않음**. A의 로그 `[NetGuard] Room keys changed…restoring: RegionMove` |
| M-3 | 대기실 | D: `4 Take master client` | 1초 안에 A가 방장을 되찾음(A 로그 없음, D Console에 방장 교체가 두 번). 시작 버튼은 계속 A |
| M-4 | 대기실 | 정원을 4명으로 하고 B·C가 **동시에** 가마솥에 들어간다(괴물 2명 인원표일 때) / 1명 인원표면 D가 가마솥에서 멀리 있을 때는 지원이 안 되는지 | 두 사람 모두 괴물이 됨(예전: 한 명만). D가 멀면 A 로그 `Claim ignored: … not at the cauldron` |
| M-5 | 게임(변장 시간) | D: `5 Paint other cookie`, `6 Clear other cookie paint` | 대상(B)의 위장이 **어느 화면에서도** 바뀌지 않음 |
| M-6 | 게임(사냥) | D: `7 Grab other cookie from afar` / B와 C가 같은 쿠키를 거의 동시에 F로 들기 | 멀리서 든 요청은 거절(B가 들리지 않고 D 화면도 곧 되돌아감). 동시 들기는 한 사람만 성공, 다른 사람 손은 비어짐 |
| M-7 | 게임(사냥) | D: `1 Break other player` | 대상이 **부서지지 않음**. A 로그 `[NetGuard] Actor …'s master-only keys were changed — restoring: HitCount, DeathCause` |
| M-8 | 게임(사냥) | D: `2 Write GameResult` | 결과 화면이 **뜨지 않음**, 판 계속 |
| M-9 | 게임(사냥, D가 괴물) | D: `8 Monster - grab-kill other cookie from afar` | 쿠키가 살아 있음. A 로그 `[GrabKill] Rejected … distance …` |
| M-10 | 게임 | 괴물이 실제로 쿠키를 잡음 | 처형 연출 → 잡힌 쿠키 관전 → 결과 화면 잡은 수 +1(예전과 같은 흐름, 방장 확정이라 수십 ms 늦을 수 있음) |
| M-11 | 게임 | D가 잡힌 뒤 `9 Revive myself` | D가 되살아나지 않음(방장이 되돌림) |
| M-12 | 게임 | 방장 A가 판 도중 나간다 | B가 방장이 되고 판이 계속됨(탈출 상태·마녀 시각 이어받음). 이후 D가 `4 Take master client`를 해도 B가 되찾음 |
| M-13 | 게임(괴물 2명 판) | 괴물 두 사람이 **동시에** 게임을 끈다 | 5초 경고 뒤 대기실로(예전: 한 명이 목록에 남아 판이 이어짐) |
| M-14 | 게임 | 스파이가 로켓에 타고 발사 / 쿠키가 탈것으로 탈출 / 마녀 처단 | 로켓 발사 연출 1번, 탈출한 사람만 결과 화면 "탈출", 처단 때 스파이·괴물만 부서짐 |
| M-15 | 대기실 → 게임 | 강퇴 기능으로 한 명을 내보낸 뒤 게임 시작 | 강퇴는 대기실에서만 동작(게임 씬에서는 연결 끊기 요청에 응하지 않음) |

- 결과를 이 표의 # 번호로 알려 주면 실패 항목만 다시 고친다.
- 알려진 한계: 방장 자신이 조작된 클라이언트면 막지 못한다(서버 판정이 없는 Photon Cloud 구조, 1장). 낙하 복귀 순간이동은 위치 조작으로 보여 3초간 탈출 요청이 거부될 수 있다(10장 P5).
