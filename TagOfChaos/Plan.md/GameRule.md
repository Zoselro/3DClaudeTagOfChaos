# GameRule.md — 숨바꼭질(쿠키) + 술래잡기(괴물) 본게임 설계 (v3.7, 2026-08-21)

> **v3.7 — 전체 구현 완료 보고(2026-08-21, "전부 구현해라" 지시에 따른 실제 Unity 작업 결과)**:
> 이번 개정에서는 문서를 더 다듬지 않고, v3.6까지 확정된 설계를 실제로 Unity 프로젝트에
> 구현했다. Unity MCP로 스크립트 생성/수정, 프리팹 조립, 씬 배선, Animator Controller 편집,
> Play Mode 실행 검증까지 전부 수행했다 — `read_console`로 컴파일 에러 0건을 반복 확인했고,
> Play Mode에서 실제로 로비 접속→캐릭터 스폰→색칠 슬롯 등록→`GrabKill` 처형→몬스터 스폰까지
> 예외 없이 동작함을 직접 실행해 확인했다(중간에 `PlayerAnimator.controller`에 `Broken`/`Held`
> 트리거 파라미터가 아예 없어 `SetTrigger()`가 에러를 내던 실제 버그 1건을 이 과정에서 발견해
> 즉시 수정함).
>
> **✅ 코드/씬 배선까지 완료된 것**: §1.5(스킨 선택), §2(가마솥/괴물 선정), §3(자유 색칠+슬롯
> 등록+지우개+리셋+강제도포), §4.1(그랩/캐리), §4.4(GrabKill 자동 처형), §4.3(TentacleDash),
> §6.1(안개), §6.2(1인칭 카메라+MonsterController), §6.3(관전), §6.4(GameEndTime), §7.1(괴물
> 이탈 처리), §8(승리 판정+결과 화면), §9(NetKeys/NetEventCodes), `MonsterPlayer.prefab`
> 신규 조립(Animator+Avatar+Rigidbody+콜라이더+전 스크립트), 구 4라운드 색상 시스템
> (`ColorSelectionManager`/`ColorVoteTally`/`TaggerColorAssigner`/`PlayerColorVoteIndicator`/
> `PlayerColorDisplay`/구`RoomLifecycleWatcher`) 완전 삭제.
>
> **⚠️ 의도적으로 단순화한 것(현실적 제약)**:
> - **"쿠키만 GameScene 이동, 괴물은 GameLobbyScene 대기"(§2.3/§2.4)는 구현하지 않았다.**
>   Photon PUN2의 `AutomaticallySyncScene`은 방 전체를 한 씬으로 함께 옮기는 것이 기본 동작이라,
>   같은 방 안에서 일부 클라이언트만 다른 씬에 머무르게 하는 것은 표준 기능이 아니다 — 대신
>   전원이 함께 `GameScene`으로 이동하고, `GamePhaseStarter`가 60초 색칠 페이즈를, 그 뒤
>   `MonsterJoinController`가 괴물 스폰을 순차 트리거하는 방식으로 같은 결과(쿠키 먼저 색칠,
>   그다음 괴물 등장)를 낸다. 진짜 "다른 씬 대기"가 필요하다면 별도 설계 검토가 필요하다.
> - **가마솥/문 3D 모델은 원통형 Primitive 플레이스홀더**다 — `솥단지.glb`가 아직 `Assets/`로
>   임포트되지 않아(§14.3 항목), 실제 모델을 넣는 작업은 이번 범위에서 하지 않았다.
> - **VFX/SFX(파괴 파편, 타격 임팩트, 가마솥 파티클 등)는 전부 미구현**이다 — 코드에는 훅
>   (`breakVfxPrefab` 등 직렬화 필드)만 뚫려 있고, 실제 파티클/오디오 에셋이 없어 필드가 비어
>   있다. 필요한 에셋을 §14.1 기준으로 준비되면 연결만 하면 된다.
> - **결과 화면/색 슬롯 UI는 기능 위주의 최소 배치**다 — `Result.png` 참고 아트(배너 일러스트,
>   왕관 아이콘 등)는 없고 단색 패널+텍스트로만 구성했다.
> - **밸런스 값(`MinStrokesToRegister=15`, 가마솥 6배 스케일 등)은 가정값 그대로**이며 실제
>   플레이테스트 조정이 필요하다.
>
> **재확인 필요(v3.6에서 이미 지적됐던 것)**: `GrabKill` 트리거 콜라이더 크기(3m 가정값)는
> 임시값이다.

> **v3.6 대비 변경 요약**(이번 개정 사유, 2026-08-21 사용자 확인: "`GrabKill`로만 할 거고,
> 타격은 하지 않을 것" — v3.4부터 남아있던 마지막 모순(균열 완전 삭제 여부)이 최종 해소됨):
> 1. **균열(hitCount==1) 상태와 손/촉수 수동 타격을 완전히 폐기하는 것으로 최종 확정.**
>    `GrabKill` 단독으로만 쿠키를 파괴하며, 그 외 어떤 타격 수단도 두지 않는다. §4.2/§4.4/§6.3의
>    "재확인 필요" 표시를 전부 제거했다.
> 2. **`PlayerCrackDisplay.cs`를 파괴(hitCount==2) 전용으로 단순화** — `_CrackAmount` 셰이더
>    분기를 제거했다(클래스 이름은 최소 변경 원칙에 따라 유지).
> 3. **균열 관련 에셋 필요 항목을 전부 "불필요 확정"으로 마감** — `_CrackAmount` 셰이더 확장,
>    타격 임팩트 이펙트/SFX, 균열 전용 UI 아이콘 모두 더 이상 필요 없다(§10.5~§10.8, §14.1).
> 4. 이로써 **§12/§14.2의 열린 질문이 전부 소진됐다** — `GrabKill` 관련 설계는 구현 착수에
>    필요한 모든 결정이 끝난 상태다(남은 것은 §3.7 색칠 판정 방식 등 이번 개정과 무관한 항목뿐).

> **v3.5 대비 변경 요약**(이번 개정 사유, 2026-08-21 사용자가 v3.4의 잔여 재확인 항목 2건에
> 추가로 답변):
> 1. **`GrabKill` 쿨다운 지속시간 확정** — "지속시간은 애니메이션이 지속되는 시간으로 한다."
>    즉 `MonsterGrabKillTrigger.onCooldown`은 `GrabKill` 애니메이션 재생이 끝나는 순간까지만
>    유지되고, 그 시점에 `ResetTrigger()`가 호출된다 — §4.4/§10.4/§12/§14.2 갱신.
> 2. **`MonsterAnimator.controller`의 `GrapKill` 오타를 `GrabKill`로 정정하기로 확정** —
>    코드(`MonsterMoveState` enum, `MonsterGrabKillTrigger.cs` 주석)를 `GrabKill`로 먼저
>    바꿔뒀다. 다만 **Animator Controller의 실제 트리거 파라미터 이름은 아직 Unity 에디터에서
>    바뀌지 않았다** — 코드와 실제 파라미터 이름이 어긋나면 트리거가 걸리지 않으므로, 파라미터
>    리네임이 코드 배포보다 먼저(또는 반드시 함께) 이뤄져야 한다(§11.1/§14.3에 작업 항목으로
>    등록).
> 3. **§4.4의 "균열 이동 제약·회복" 모순(v3.4 §12 참고)은 이번에도 답변되지 않아 여전히
>    열려있다** — 균열 단계를 완전히 삭제할지는 아직 재확인이 필요하다.

> **v3.4 대비 변경 요약**(이번 개정 사유, 2026-08-21 사용자가 §12 열린 질문 다수에 답변 — 그
> 답변을 §4.4/§6.2/§6.3/§7.3/§9/§10.1/§10.4/§10.9/§12/§13/§14.1/§14.2 전체에 전파해 문서
> 내 모순을 제거했다):
> 1. **`GrabKill`이 §4.2의 "타격 2회(균열→파괴)" 설계를 완전히 대체하는 (A)안으로 최종
>    확정.** `MonsterStrikeAttack.cs`(손/촉수 수동 1차 타격 골격)와 "균열(hitCount==1)" 상태,
>    타격 스윙 애니메이션 확보 필요성이 통째로 사라진다 — §4.4/§10.1/§10.4/§10.9/§14.1 전체
>    갱신.
> 2. **괴물 이동 방식을 Rigidbody 물리 기반으로 확정.** §6.2의 `MonsterController`/§4.3의
>    `TentacleDash` 통합 골격을 `HideOrSeekPlayer`와 동일한 Rigidbody 패턴으로 재작성했다.
> 3. **타격 판정·조준·가마솥 타임아웃 등 §12의 나머지 질문 다수가 함께 확정**됐다(전방 3m
>    자동 발동, 촉수만 사용, 가마솥 타임아웃 30초, `TentacleDash` 입력키 좌Shift 임시 유지,
>    `GrabKill` 발동 중 회피 불가) — §12/§14.2 표를 전부 갱신했다.
> 4. **⚠️ 확인 필요 — Q3/Q4(균열 이동 제약·회복 여부) 답변이 (A) 확정과 서로 모순된다.**
>    (A)는 `hitCount`가 0→2로 곧장 뛰어 "균열(hitCount==1)" 상태 자체가 도달 불가능한 죽은
>    상태가 되는데, Q3("균열 이동 제약 없음")·Q4("균열은 영구, 회복 없음") 답변은 그 균열
>    상태가 실제로 존재한다는 전제로 주어졌다 — 두 답변 다 (A) 하에서는 **적용될 일이 없는
>    무효 답변**이 된다. §6.3/§12/§14.2에 이 모순을 명시해뒀다 — 균열 단계를 정말 완전히
>    없앨 것인지 재확인 바란다.
> 5. **`GrabKill` 재사용 대기시간(쿨다운 지속시간) 답변이 원 질문과 어긋나 재확인이 필요하다**
>    — "스킬 쓰고 난 직후"는 쿨다운이 *시작*되는 시점(이미 코드에 반영돼 있던 것)이지, 원래
>    질문("언제 다시 풀어주는지")이 묻던 쿨다운의 *길이*가 아니다. §12/§14.2에 재질문으로
>    남겨뒀다.
> 6. **색칠 판정 방식(§3.7, Ray/정적 프록시 콜라이더 전환)은 "일단 보류"로 확정** — 논의
>    자체는 유효하게 남지만 이번 개정에서 착수하지 않는다.

> **v3.3 대비 변경 요약**(이번 개정 사유, 2026-08-19 사용자 지시 + 실제 코드베이스/에셋 파일
> 직접 확인 결과 반영):
> 1. **쿠키 스킨(색상 A/B/C) 선택을 `GameLobbyScene` 진입 시점으로 확정.**
>    `Assets/05. Materials/Character/Cookie_BaseSkin_{A,B,C}.mat`(+각 `_Color.png`)가 이미
>    3벌 모두 확보돼 있었으나(`research.md` §4.6에서 "B/C 미배선"으로 지적됐던 바로 그 에셋)
>    선택 UI·네트워크 동기화가 없었다 — 신규 §1.5로 설계를 채운다.
> 2. **색칠 판정 방식(§3) 재검토 — "붓이 몸에 닿아야 칠해지는" 현재 방식을 Ray(레이) 발사
>    방식으로 바꾸는 논의를 시작한다.** 사용자 우려: "붓 자체도 어떻게 보면 닿아야 되는거기
>    때문에 나중에 버그가 생길 요지가 있다." 이 우려는 이 프로젝트의 실제 커밋 이력과 정확히
>    맞아떨어진다(`16c662b`의 "상체 색칠 안됨 및 붓 커서가 몸안에 파고드는 현상 수정" 커밋,
>    `PlayerPaintCanvas.cs` 내부의 `Bug-fix-plan.md §17/§20` 주석 다수) — §3.7 신규.
> 3. **괴물 3D 모델·애니메이션 4종(`Monster_T_Pose`/`Idle`/`Walk`/`TentacleDash`/`GrabKill`)이
>    실제로 확보·임포트돼 있음을 파일로 직접 확인.** `Assets/Animation/Monster/Monster_Rigged*.fbx`
>    5개 + `Assets/Animation/MonsterAnimator.controller`(Idle/Walk/TentacleDash/GrapKill 4개
>    트리거 파라미터, `Monster_Rigged.fbx.meta`의 `animationType: 2`로 Generic 리그 확인)를
>    직접 열어봤다 — §10.3/§10.4/§11.1 갱신, §14.1의 "최우선 미확보" 항목 다수 해소.
> 4. **신규 스킬 `TentacleDash` 설계.** 쿨타임 15초, 사거리 20m 돌진기 — §4.3 신규.
> 5. **`GrabKill` 자동 처형 메커니즘을 §4.2("타격 2회: 균열→파괴")의 대체안으로 제안.**
>    "Player가 범위 안에 들어오면 자동으로 발동"이라는 이번 지시는 v3.2가 열어뒀던 §12-1
>    (타격 트리거 방식)을 "자동 근접"으로 확정하는 동시에, 수동 `MonsterStrikeAttack` 타격
>    골격 자체를 대체하는지 새로 묻는다 — §4.4 신규, 관계는 §4.4 말미의 열린 질문 참고.
> 6. **가마솥(`Cauldron`) 3D 모델 확보 확인.** `TagOfChaos/리소스/솥단지.glb`(Unity `Assets/`
>    바깥의 임포트 대기 스테이징 폴더 — `괴물.glb`가 `Monster_Rigged.fbx`로 임포트되기 전
>    같은 폴더에 있었던 것과 동일한 성격)에서 확인됨, 아직 `Assets/`로 임포트되지 않은 상태다.
>    §2.1/§10.3 갱신.
>
> **이번에도 실제 스크립트/에셋 배선 작업은 진행하지 않는다** — 설계 문서 갱신까지만
> 수행한다(v3와 동일한 원칙 유지, 사용자 확인 전 착수 금지).

---

> **v3 대비 변경 요약**(2026-08-18, 이전 개정 사유 — 참고용으로 보존):
> 1. **마녀(Witch) 캐릭터 → 괴물(Monster)로 전면 교체.** 마녀 프리팹은 제거하고, 신규 괴물
>    3D 모델로 대체한다. 참고 이미지("괴물 T-pose.png")는 최초 조사 시점엔 프로젝트에 없었으나,
>    **같은 날 사용자가 다시 반입해 `Assets/Screenshots/괴물 T-pose.png` +
>    `리소스/괴물 T-pose.png` 두 곳에서 확인됨** — 이미지 확인 결과는 §4.2/§11.1/§14.1에
>    반영했다(요약: 원형 머리+고깔모자+주름 칼라+해골 단추 의상+주름 치마+둥근 신발을 갖춘
>    **삐에로 풍 몬스터**이며, **팔다리가 6개**다 — 다리 2개, 몸통 옆에서 뻗은 관절형 팔 2개
>    (발톱 손), 머리 뒤에서 뻗어나오는 구불거리는 촉수 2개(뭉툭한 발바닥형 손)).
> 2. **F키 원거리 마법 공격(v2에서 신설) → 완전 폐기.** `WitchMagicAttack`/`MagicCast`
>    이벤트/1인칭 카메라 전제가 전부 제거된다.
> 3. **(v3.2로 재차 갱신) 괴물의 공격은 "포획"이 아니라 "타격으로 부수는" 메커니즘이다** —
>    1회 피격 시 균열(아직 이동 가능), 2회 피격 시 파괴(탈락). §4.2 전면 재설계.
> 4. **소품 던지기(v1부터 있던 기능) → 완전 폐기.** `ThrowableProp`/`NoiseListener`/
>    `NoisePing` 이벤트가 전부 제거된다.
> 5. **(v3.2) 괴물 카메라는 1인칭으로 확정.** "3인칭으로 하기엔 힘들 것 같다"는 사용자 판단에
>    따라, v3에서 "폐기 검토"로 분류했던 1인칭 카메라를 되살린다. §6.2 전면 재설계.
> 6. 위 변경에 따라 §4.2·§6.2·§6.3·§8·§9(NetKeys/EventCodes)·§10(에셋 목록)·§12(열린 질문)·
>    §14(사용자 제공 필요 항목)를 갱신했다.
>
> **아직 구현하지 않는다** — 이번 개정은 설계 문서 반영까지만이며, 실제 스크립트/에셋 작업은
> 진행하지 않는다(사용자 확인).

---

## 0. 이 설계가 기존 코드에 미치는 영향 (요약)

현재 `ColorTag/` 도메인(15파일)의 "팀 투표로 술래 색 감추기" 컨셉은 완전히 대체된다. 색은
개인 자유 표현(최대 4색, 슬롯 UI, 임계량 등록, Reset/지우개)이고, 괴물은 색이 아니라 **가마솥
행위 + 별도 캐릭터 모델**로 정해지며, 원거리 공격도 근접 포획도 아니라 **손/촉수로 타격해
부수는(균열→파괴)** 방식으로 쿠키를 탈락시킨다.

| 기존 컴포넌트 | 처리 |
|---|---|
| `ColorSelectionManager`(4라운드 루프+`AssignTagger`) | **폐기**. §2(괴물 선정)+§3(페인트 타이머)로 대체 |
| `ColorVoteTally` / `TaggerColorAssigner` | **폐기**. 팀 투표·색 치환 식별자 개념 자체가 없어짐 |
| `ColorSelectionPanel`/`ColorSwatchButton` | **재작성**. 개인 슬롯 UI(§3.4)로 |
| `PlayerColorVoteIndicator` / `PlayerColorDisplay` | **폐기**(단, `PlayerColorVoteIndicator`의 코드 구조는 §4.2의 균열 표시 컴포넌트가 그대로 본뜬다) |
| `PlayerPaintCanvas` | **핵심 로직 재사용**, 슬롯/임계량 등록/지우개로 교체(§3) |
| `RoomLifecycleWatcher`(술래 퇴장 감지) | **재작성**. 단일 액터 → 다중 `MonsterActorNumbers` 배열 대응(§7) |
| `GameLobbyController.OnMasterClientSwitched` | **그대로 활용** — 방장 위임은 Photon 기본 동작으로 이미 해결됨(§7.2) |

**v2에서 계획됐다가 v3/v3.2에 다시 폐기/변경되는 것**:

| v2 계획 컴포넌트 | 최종 처리 |
|---|---|
| `Witch/WitchMagicAttack.cs` | **폐기.** F키 원거리 마법 공격 자체가 없어짐(§6.2 옛 버전) |
| `Witch/WitchFirstPersonCamera.cs` | **부활, `Monster/MonsterFirstPersonCamera.cs`로 확정(v3.2)** — §6.2 |
| `NetEventCodes.MagicCast` | **폐기** |
| `Grab/ThrowableProp.cs`, `Grab/NoiseListener.cs` | **폐기.** 소품 던지기 기능 자체가 없어짐(§5) |
| `NetEventCodes.NoisePing` | **폐기** |
| (v3 계획) 괴물 포획(촉수/손) 컴포넌트 | **v3.2에서 "타격" 컴포넌트로 재설계** — §4.2 |
| `Witch/*` 폴더·클래스 전체 | `Monster/`로 명칭 변경. 로직(선정/이탈 처리/승리 판정 등)은 대부분 유지, 명칭만 교체 |
| `PlayerMoveState.Dead`/`Caught`, `NetKeys.IsDead`/`IsCaught` | **`Broken`/`HitCount`로 재설계(v3.2)** — "즉사"도 "포획"도 아니라 "파괴"가 정확한 표현(§4.2, §9) |

---

## 1. 전체 게임 플로우 (v3.2 갱신)

```
GameLobbyScene (대기실 — 문 4개 + 가마솥, 입장 즉시 스킨 선택 가능(§1.5))
  ├─ 아무 쿠키나 가마솥에 들어감 → 선착순 괴물 확정(§2.1)
  │    └─ 예외: MonsterSelectTimeout(예: 30초) 안에 아무도 안 들어가면 마스터가 랜덤 1인 배정
  ├─ 연출(보글보글→짜잔) + 괴물 프리팹 교체(§2.2)
  ├─ 10초 카운트다운 → 쿠키만 GameScene 이동, 괴물은 GameLobbyScene 대기(§2.3~2.4)
GameScene (쿠키만 입장)
  ├─ 60초 자유 색칠(§3) — 일정량 이상 칠한 색만 슬롯에 등록
  ├─ 60초 경과 → 등록 슬롯 0개인 플레이어는 서로 겹치지 않는 색으로 전신 강제 도포(§3.6)
  └─ 괴물 GameScene 합류(§6.4) — GameEndTime = 합류 시각 + 10분
        ├─ 쿠키: 시야 축소(안개, §6.1)
        ├─ 쿠키: 서로 그랩/캐리(§4.1) — 소품 던지기는 폐기됨(§5)
        ├─ 괴물: 1인칭 시점(§6.2)에서 촉수 또는 손으로 쿠키를 타격(§4.2, 실제 플레이어, AI 아님)
        │    └─ 1회 피격 = 균열(이동 가능), 2회 피격 = 파괴(탈락)
        ├─ 파괴된 쿠키: Space로 생존 쿠키 시점 관전(§6.3)
        ├─ 괴물 전원 퇴장 시 5초 경고 후 GameLobbyScene 복귀(§7)
        └─ 승리 판정(§8) — 전원 파괴→괴물 승, 10분 생존→쿠키 승, 결과 화면(§8.2) 표시
```

---

## 1.5 쿠키 스킨(색상 A/B/C) 선택 — `GameLobbyScene` 진입 시 (신규, v3.3)

사용자 지시: "쿠키(Player)는 처음에 GameLobbyScene으로 입장할 때, 색상 A, 색상 B, 색상 C 중에서
선택해서 고르는 걸 시작으로 하는 방향으로 가는 걸 원해." 이 세 가지 스킨은 이미 프로젝트에
존재한다 — `research.md` §4.6이 지적했던 "에셋만 있고 미배선" 상태의 바로 그 머티리얼이다:

```
Assets/05. Materials/Character/Cookie_BaseSkin_A.mat (+ Cookie_BaseSkin_A_Color.png)
Assets/05. Materials/Character/Cookie_BaseSkin_B.mat (+ Cookie_BaseSkin_B_Color.png)
Assets/05. Materials/Character/Cookie_BaseSkin_C.mat (+ Cookie_BaseSkin_C_Color.png)
```
세 `.mat` 모두 Unity 기본 Standard 셰이더(`m_Shader: {fileID: 46, ...}`)를 쓰고 `_MainTex`
프로퍼티로 컬러 텍스처를 물고 있음을 직접 열어 확인했다 — 이는 정확히
`PlayerPaintCanvas.InitPaintCanvas()`가 이미 읽고 있는 바로 그 프로퍼티다(아래 실제 코드):

```csharp
// PlayerPaintCanvas.cs — InitPaintCanvas() 중 실제 코드, 변경 없이 그대로 재사용 가능
Material original = bodyRenderer.sharedMaterial;
Material painted = new Material(paintedSkinShader);
if (original != null && original.HasProperty("_MainTex"))
    painted.SetTexture("_MainTex", original.mainTexture);
painted.SetTexture("_PaintTex", PaintCanvas);
```
즉 **스폰 시점에 `bodyRenderer.sharedMaterial`을 A/B/C 중 선택된 것으로 미리 바꿔두기만 하면**,
`PlayerPaintCanvas.cs`는 한 줄도 수정할 필요 없이 스킨별 베이스 컬러를 자동으로 페인트 합성
머티리얼에 반영한다.

### 1.5.1 동기화 설계 — 소유권 원칙 그대로 재사용

스킨 선택은 "그 캐릭터 소유자만 자기 값을 쓰고, 다른 모두가 소유권과 무관하게 그 값을 읽어
표시한다"는 이 프로젝트의 기존 패턴(`VoteColorIndex`/`PlayerColorVoteIndicator`,
`RegisteredSlotCount` 등)과 완전히 같은 성격이다.

```csharp
// NetKeys.cs — 신규 추가 (v3.3)
public const string SkinIndex = "SkinIndex"; // Player CustomProperty, int 0=A/1=B/2=C, 기본값 0(A)
```

```csharp
// Assets/02. Scripts/Lobby/PlayerSkinSelector.cs (신규) — GameLobbyScene의 스킨 선택 UI에 부착
using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

public class PlayerSkinSelector : MonoBehaviour
{
    [SerializeField] private Button skinAButton;
    [SerializeField] private Button skinBButton;
    [SerializeField] private Button skinCButton;

    private void Awake()
    {
        skinAButton.onClick.AddListener(() => SelectSkin(0));
        skinBButton.onClick.AddListener(() => SelectSkin(1));
        skinCButton.onClick.AddListener(() => SelectSkin(2));
    }

    // 대기실에서 언제든 다시 눌러 바꿀 수 있다 — 강제 선택이 아니며, 한 번도 안 누르면 기본값(A)로 스폰된다.
    private void SelectSkin(int index)
    {
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { NetKeys.SkinIndex, index } });
    }
}
```

```csharp
// Assets/02. Scripts/Unit/PlayerSkinApplier.cs (신규) — HideOrSeekPlayer.prefab에 부착.
// bodyRenderer는 PlayerPaintCanvas.bodyRenderer와 인스펙터에서 동일한 SkinnedMeshRenderer를 연결한다.
//
// Awake()에서 적용하는 이유: Unity는 같은 프레임 안에서 "씬(또는 Instantiate)에 있는 모든
// 컴포넌트의 Awake가 전부 끝난 뒤에야 비로소 아무 컴포넌트의 Start가 시작된다"를 보장한다 —
// HideOrSeekPlayer.Awake()가 networkSync를 IsMine 여부와 무관하게 최우선 생성하는 것과 정확히
// 같은 근거(research.md §5.7). 여기서 sharedMaterial을 미리 바꿔두면, 나중에 실행되는
// PlayerPaintCanvas.Start()의 InitPaintCanvas()가 "bodyRenderer.sharedMaterial의 _MainTex를
// 읽어 합성 머티리얼을 만드는" 시점에는 이미 올바른 스킨이 반영돼 있다.
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PlayerSkinApplier : MonoBehaviourPunCallbacks
{
    [SerializeField] private PhotonView pv;
    [SerializeField] private Renderer bodyRenderer; // PlayerPaintCanvas.bodyRenderer와 동일한 렌더러
    [SerializeField] private Material[] skins; // 인덱스 0=A/1=B/2=C — Cookie_BaseSkin_A/B/C 연결

    private void Awake()
    {
        ApplySkin();
    }

    // 다른 클라이언트 관점에서 이 캐릭터가 스폰된 시점에 소유자의 SkinIndex가 아직 서버에
    // 반영되기 전이었을 가능성에 대한 방어 — GameManager.md류 기존 문서가 여러 번 지적해온
    // "네트워크 프로퍼티 도착 순서가 스폰 순서를 보장하지 않는다"는 이 프로젝트의 반복되는
    // 전제와 동일 계열이다. 값이 늦게 도착하면 이 콜백이 재적용한다.
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (targetPlayer != pv.Owner) return;
        if (!changedProps.ContainsKey(NetKeys.SkinIndex)) return;
        ApplySkin();
    }

    private void ApplySkin()
    {
        if (bodyRenderer == null || skins == null || skins.Length == 0 || pv.Owner == null) return;

        int index = pv.Owner.CustomProperties.TryGetValue(NetKeys.SkinIndex, out object v) ? (int)v : 0;
        index = Mathf.Clamp(index, 0, skins.Length - 1);
        bodyRenderer.sharedMaterial = skins[index];
    }
}
```

### 1.5.2 UI 배치

`GameLobbyScene`은 이미 `PlayerSpawnPos`/`VoidKillZone`이 배치돼 있어 대기실에서도 캐릭터가
실제로 스폰돼 돌아다닐 수 있는 씬이다(`research.md` §3 씬 배선 표) — 즉 스킨을 고르면 대기실
안에서 바로 자기 캐릭터에 반영된 모습을 눈으로 확인할 수 있다. `PlayerSkinSelector`가 부착된
`SkinSelectPanel.prefab`(§10.2 D 신규)을 `GameLobbyUICanvas`에 상시 노출 형태로 배치하고,
"게임 시작" 버튼과는 독립적으로 언제든 눌러 바꿀 수 있게 한다(강제 진행형 팝업이 아님 — 정원이
찰 때까지 대기하는 동안 자연스럽게 고르는 것을 전제로 함).

---

## 2. 괴물 선정 & 가마솥 연출 (`GameLobbyScene`)

### 2.1 가마솥 트리거 — 선착순 + 타임아웃 랜덤 배정

사용자 확인: "선착순 자진 입장이 맞다. 하지만 아무도 안 들어가면 랜덤으로 1명이 괴물이 된다."
클라이언트가 신청을 마스터에 보내고 마스터가 확정하는 구조는 그대로 두고, **마스터 전용
타임아웃 폴백**만 둔다 — `ColorSelectionManager`/`RoomLifecycleWatcher`가 이미 쓰는 "마스터만
`Update()`에서 만료 시각 폴링" 패턴 그대로다.

```csharp
// Assets/02. Scripts/Monster/Cauldron.cs — OnTriggerEnter에서 ClaimMonster RaiseEvent
```

```csharp
// Assets/02. Scripts/Monster/MonsterAssignmentAuthority.cs (마스터 전용)
public class MonsterAssignmentAuthority : MonoBehaviourPunCallbacks, IOnEventCallback
{
    [SerializeField] private float monsterSelectTimeout = 30f;
    private double sceneEnterTime;

    private void Start()
    {
        if (PhotonNetwork.IsMasterClient) sceneEnterTime = PhotonNetwork.Time;
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != NetEventCodes.ClaimMonster) return;
        if (!PhotonNetwork.IsMasterClient) return;
        if (HasMonsterAssigned()) return; // 이미 확정 — 이후 요청/타임아웃 전부 무시

        int claimantActorNumber = (int)photonEvent.CustomData;
        ConfirmMonster(new[] { claimantActorNumber });
    }

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (HasMonsterAssigned()) return;
        if (PhotonNetwork.Time < sceneEnterTime + monsterSelectTimeout) return;

        var players = PhotonNetwork.PlayerList;
        int randomActorNumber = players[new System.Random().Next(players.Length)].ActorNumber;
        ConfirmMonster(new[] { randomActorNumber });
    }

    private bool HasMonsterAssigned() =>
        PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(NetKeys.MonsterActorNumbers);

    private void ConfirmMonster(int[] monsterActorNumbers)
    {
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { NetKeys.MonsterActorNumbers, monsterActorNumbers },
            { NetKeys.MonsterRevealTime, PhotonNetwork.Time },
        });
    }
}
```

> 📌 **다중 괴물 확장에 대한 메모**: 초기 배정은 "가마솥에 1명이 들어가면 괴물"이므로 여전히
> 1명이다. `MonsterActorNumbers`를 처음부터 배열로 설계해둔 것은 §7의 이탈 처리 시나리오가
> 다중 괴물을 전제하기 때문이며, 나중에 인원수가 늘어나도 배열 길이만 늘리면
> §2.2(리빌)/§4.2(타격)/§7(이탈 처리)/§8(승리 판정) 전부 그대로 동작한다.

> 📌 **가마솥 3D 모델 확보 확인(v3.3)**: `TagOfChaos/리소스/솥단지.glb`에서 실제 파일을 확인했다.
> 이 `리소스/` 폴더는 Unity `Assets/` 바깥에 있는 임포트 대기 스테이징 폴더로, 몬스터 모델도
> 같은 폴더의 `괴물.glb`로 있다가 `Assets/Animation/Monster/Monster_Rigged.fbx` 계열로 이미
> 임포트 완료된 전례가 있다(§10.3, §11.1). `솥단지.glb`는 아직 `Assets/`로 임포트되지 않은
> 상태이므로, `Cauldron.prefab`(§10.2 C)에 실제로 붙이려면 먼저 Unity 프로젝트 안(예:
> `Assets/Animation/Cauldron/` 또는 `Assets/04. Prefabs/` 하위 적절한 위치)으로 임포트하는
> 작업이 선행돼야 한다 — 이 임포트 자체는 §14.3과 같은 성격의 "결정 사항이 아니라 구현 작업"
> 이라 사용자 확인 없이 진행 가능하다.

### 2.2~2.4 (명칭만 교체, 로직은 v2와 동일)

리빌 연출·10초 카운트다운·쿠키만 GameScene 이동·괴물 GameLobbyScene 대기 로직은 이전과
동일하다. `MonsterRevealController`/`PlayerSpawner`의 판별 조건은
`((int[])room.CustomProperties[NetKeys.MonsterActorNumbers]).Contains(PhotonNetwork.LocalPlayer.ActorNumber)`
로 다중 괴물을 대응한다.

---

## 3. 개인 자유 색칠 (GameScene, 60초) — 슬롯 등록 방식 + 강제 도포 중복 방지(v3.1 갱신)

> **v3.1 추가 변경**: §3.6 "60초 만료 시 전신 랜덤 강제 도포"에서, 등록 슬롯 0개인 플레이어가
> **2명 이상이면 서로 같은 색으로 겹쳐 도포되지 않도록** 마스터 권위 배정 방식으로 재설계했다
> (사용자 확인). §7.2가 이름만 언급해뒀던 `PaintPhaseController`(마스터 전용, §3.6)를 이번에
> 실제로 정의한다.

### 3.1 문제 재정의 — 악성 유저의 "색 숨기기" 방지

사용자 확인: v1에서 우려했던 문제(재도색을 막으면 자연스러운 보정이 어려움)의 실제 의도는
**"1픽셀만 칠하고 다른 색으로 넘어가 괴물이 실제 색을 판단 못 하게 하는 악용"**을 막는 것이었다.
해결책: **색을 선택하는 순간이 아니라, 그 색으로 "일정량 이상" 실제로 칠했을 때만 슬롯에
등록**한다. 등록되지 않은 색은 화면엔 보이지만(실시간 공유 요구사항 충족) 슬롯 카운트에는
잡히지 않으므로, 60초가 지나도 슬롯이 0개면 그대로 전신 강제 도포 대상이 된다.

### 3.2 `PlayerPaintCanvas` 확장 — 임계량 기반 등록 + 슬롯 수 네트워크 동기화

```csharp
// PlayerPaintCanvas.cs — 신규 필드
private const int MinStrokesToRegister = 15; // 임계값 — 밸런스 값, §12 열린 질문
private readonly Dictionary<int, int> pendingStrokeCounts = new Dictionary<int, int>(); // 미등록 색 → 누적 스탬프 수
private readonly List<int> registeredColorSlots = new List<int>(4);

public event System.Action<IReadOnlyList<int>> OnSlotsChanged;
public event System.Action OnSlotRejected; // "이미 4가지 색상을 모두 사용했습니다"

// Update()의 스탬프 직전 검사
int brushColor = GetCurrentBrushColorIndex();
if (brushColor < 0) return;

if (!registeredColorSlots.Contains(brushColor))
{
    if (registeredColorSlots.Count >= 4)
    {
        OnSlotRejected?.Invoke();
        return;
    }

    pendingStrokeCounts.TryGetValue(brushColor, out int count);
    count++;
    if (count >= MinStrokesToRegister)
    {
        pendingStrokeCounts.Remove(brushColor);
        registeredColorSlots.Add(brushColor);
        OnSlotsChanged?.Invoke(registeredColorSlots);
        ReportSlotCount(); // §3.6 신규 — 마스터가 "이 플레이어는 슬롯이 있다"를 알 수 있도록 즉시 반영
    }
    else
    {
        pendingStrokeCounts[brushColor] = count;
    }
}

StampBrush(hit.textureCoord, brushColor);
```

```csharp
// PlayerPaintCanvas.cs — §3.6 신규: 등록 슬롯 수를 자기 자신의 Player CustomProperties에 계속 보고
private void ReportSlotCount()
{
    if (!pv.IsMine) return;
    PhotonNetwork.LocalPlayer.SetCustomProperties(
        new Hashtable { { NetKeys.RegisteredSlotCount, registeredColorSlots.Count } });
}
```

### 3.3 브러시 색 선택 / 3.4 Reset / 3.5 지우개 (변경 없음)

`ColorSwatchButton.SetBrushColor()`, `ColorReplaceMaterial` 재사용 Reset, `EraseStampMaterial`
지우개 — v1 로직 그대로 유효.

### 3.6 60초 만료 — 등록 슬롯 0개 플레이어 전신 강제 도포 (마스터 배정 방식, v3.1)

**문제**: 등록 슬롯 0개인 플레이어가 2명 이상이면, 각자 독립적으로 무작위 색을 뽑을 경우
우연히 같은 색이 겹칠 수 있다. **해결**: 마스터 클라이언트 1명이 전체 상황을 보고 팔레트
색을 셔플한 뒤 앞에서부터 겹치지 않게 하나씩 배정한다.

```csharp
// Assets/02. Scripts/ColorTag/PaintPhaseController.cs (마스터 전용)
public class PaintPhaseController : MonoBehaviourPunCallbacks
{
    [SerializeField] private ColorPaletteSO palette;
    private System.Random rng = new System.Random();
    private bool resolved;

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (resolved) return;
        if (!RoomState.TryGetDouble(NetKeys.PaintPhaseEndTime, out double endTime)) return;
        if (PhotonNetwork.Time < endTime) return;

        ResolvePaintPhase();
        resolved = true;
    }

    private void ResolvePaintPhase()
    {
        var zeroSlotPlayers = new List<Player>();
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            int count = p.CustomProperties.TryGetValue(NetKeys.RegisteredSlotCount, out object v) ? (int)v : 0;
            if (count == 0) zeroSlotPlayers.Add(p);
        }

        if (zeroSlotPlayers.Count == 0) return;

        int[] shuffledColors = Enumerable.Range(0, palette.Count).OrderBy(_ => rng.Next()).ToArray();

        int[] actorNumbers = new int[zeroSlotPlayers.Count];
        int[] assignedColors = new int[zeroSlotPlayers.Count];
        for (int i = 0; i < zeroSlotPlayers.Count; i++)
        {
            actorNumbers[i] = zeroSlotPlayers[i].ActorNumber;
            assignedColors[i] = shuffledColors[i];
        }

        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { NetKeys.ForcedPaintActorNumbers, actorNumbers },
            { NetKeys.ForcedPaintColors, assignedColors },
        });
    }
}
```

```csharp
// PlayerPaintCanvas.cs — 자신이 배정 대상이면 전신을 그 색으로 강제 도포(로컬 1회만 적용)
public override void OnRoomPropertiesUpdate(Hashtable changedProps)
{
    if (!changedProps.ContainsKey(NetKeys.ForcedPaintActorNumbers)) return;
    if (!pv.IsMine) return;
    ApplyForcedColorIfAssignedToMe();
}

private void ApplyForcedColorIfAssignedToMe()
{
    if (!RoomState.TryGetIntArray(NetKeys.ForcedPaintActorNumbers, out int[] actorNumbers)) return;
    if (!RoomState.TryGetIntArray(NetKeys.ForcedPaintColors, out int[] colors)) return;

    int myIndex = System.Array.IndexOf(actorNumbers, PhotonNetwork.LocalPlayer.ActorNumber);
    if (myIndex < 0) return;

    ApplyStamp(finalizeStampMaterial /* 또는 전용 FillAllMaterial, §10.5 */, Vector2.zero, float.MaxValue, colors[myIndex]);
    SendStrokeEvent(Vector2.zero, float.MaxValue, colors[myIndex], force: true);
}
```

### 3.7 색칠 판정 방식 재검토 — "닿아야 함" → Ray 발사 방식 전환 논의 (신규, v3.3, 열린 논의)

사용자 지시: "색칠하는 것을 생각해보니 Ray를 쏘는 방식으로 하는 건 어떨지에 대한 논의가
필요해 보여. 붓 자체도 어떻게 보면 닿아야 되는 거기 때문에 나중에 버그가 생길 요지가 있어
보여." 아래는 이 우려를 실제 코드와 대조해 근거를 확인하고, 구체적인 대안을 제시하는 절이다
— **아직 확정된 변경이 아니라 §12에 열린 질문으로 등록된 논의**다.

#### 3.7.1 현재 방식이 정확히 무엇에 "닿아야" 하는지 (실제 코드 근거)

`PlayerPaintCanvas.cs`의 붓칠 판정은 이미 레이캐스트를 쓰고 있다 — 문제는 레이 자체가 아니라
**레이가 맞아야 하는 대상**이다. 실제 코드:

```csharp
// PlayerPaintCanvas.cs Update() 중 실제 코드
Ray ray = localCamera.ScreenPointToRay(Input.mousePosition);
if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, paintRaycastMask)) return;
if (hit.collider != paintableCollider) return; // 자신의 오브젝트가 아니면 무시
```

`paintableCollider`는 고정된 콜라이더가 아니라 **매 3프레임마다 그 순간의 애니메이션 포즈를
구워(bake) 새로 만드는 `MeshCollider`**다 — `RefreshColliderMesh()`의 실제 코드와 주석:

```csharp
// PlayerPaintCanvas.cs RefreshColliderMesh() 중 실제 코드 + 주석(원문 그대로)
// BakeMesh(정점 16만개대) + MeshCollider 재계산(cook)이 프레임당 약 6ms 이상 들어(Play Mode
// 실측: 매 프레임 갱신 시 257fps -> 15fps로 급락) 매 프레임 수행하면 안 된다
skinnedBodyRenderer.BakeMesh(bakedColliderMesh, false);
...
paintableMeshCollider.sharedMesh = null;
paintableMeshCollider.sharedMesh = bakedColliderMesh; // 강제 재계산(cook) 트리거
```

즉 사용자가 말한 "붓이 닿아야 함"은 곧 **"매 3프레임 다시 구워진, 그 순간의 실시간 포즈를
반영한 스킨 메시 콜라이더에 정확히 맞아야 함"**이라는 뜻이다. 이 부분은 이미 실제로 여러 번
버그를 일으킨 이력이 있다 — `git log`에 남은 커밋 `16c662b`("상체 색칠 안됨 및 붓 커서가
몸안에 파고드는 현상 수정")가 정확히 이 메커니즘 때문에 생긴 버그의 수정 커밋이고, 코드
안에도 `Bug-fix-plan.md §17/§20.6/§20.7/§20.8` 등 같은 영역을 반복 수정한 주석이 다수 남아
있다. 사용자의 우려는 추측이 아니라 **이 프로젝트에서 이미 실증된 버그 패턴**이다.

#### 3.7.2 대안 A(권장) — 실시간 베이크 콜라이더를 정적 프록시 콜라이더로 교체

핵심 아이디어: 붓칠 판정 대상을 "매 프레임 바뀌는 실제 스킨 메시"가 아니라 **한 번만 만들고
다시는 갱신하지 않는 고정(static) 콜라이더**로 바꾼다. 레이는 지금처럼 화면 좌표에서 그대로
쏘되(`ScreenPointToRay` 자체는 유지), 맞히는 대상만 바뀐다.

```csharp
// PlayerPaintCanvas.cs — 대안 A 적용 시 삭제 대상 필드/메서드
// paintableMeshCollider, skinnedBodyRenderer, bakedColliderMesh, colliderRefreshCounter,
// ColliderRefreshInterval, RefreshColliderMesh() 전체 삭제

// Start()에서 이제 이 초기화 블록 자체가 불필요해짐:
// paintableMeshCollider = paintableCollider as MeshCollider;
// skinnedBodyRenderer = bodyRenderer as SkinnedMeshRenderer;
// if (paintableMeshCollider != null && skinnedBodyRenderer != null) { ... }

// Update()에서 RefreshColliderMesh() 호출 줄만 제거 — 나머지 레이캐스트 로직은 완전히 동일:
private void Update()
{
    DetectRoundChange();
    if (!pv.IsMine) return;
    if (!IsColorRoundActive()) return;

    HandleBrushSizeInput(); // colliderRefreshCounter 증가/RefreshColliderMesh() 호출 삭제

    if (!Input.GetMouseButton(0)) return;
    ...
    if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, paintRaycastMask)) return;
    if (hit.collider != paintableCollider) return; // 이제 "고정된" 프록시라 애니메이션 포즈와 무관하게 항상 유효
    ...
}
```

`paintableCollider` 인스펙터 연결 대상을 실제 스킨 메시가 아니라, 캐릭터 몸통을 대략 감싸는
저폴리 "페인트 전용 쉘" 메시(또는 단순 캡슐)로 바꾼다. `hit.textureCoord`가 그대로 UV를
반환하므로, 이 프록시 메시의 UV를 임포트 시점에 미리 실제 스킨 UV와 맞춰두기만 하면 런타임
코드는 오히려 더 단순해진다. `BrushCursorController`(붓 커서 3D 표시)도 `PlayerPaintCanvas.
PaintableCollider`를 그대로 참조하므로 별도 수정 없이 함께 적용된다.

**트레이드오프**: 팔을 들거나 웅크리는 등 포즈가 크게 바뀐 순간에는 화면상 보이는 몸과 프록시
콜라이더의 위치가 살짝 어긋날 수 있다(고정된 중립 포즈 기준이므로). 다만 이 미니게임은 색을
정밀하게 특정 부위에 맞춰 칠하는 정밀 게임이 아니라 "몸에 색을 채워 넣는" 성격이라, 약간의
오차는 체감상 문제가 되지 않을 가능성이 높다 — 실제 채택 여부는 플레이테스트로 확인 필요.

> ⚠️ 이 변경은 §4.2/§4.1이 이미 갖고 있는 `Physics.IgnoreCollision(rootCollider, bodyMeshCollider,
> true)`(캐릭터 자기 충돌 무시, Bug-fix-plan §14) 물리 버그와는 **무관하다** — 그쪽은
> `Rigidbody`끼리의 충돌 문제이고, 여기서 다루는 것은 순수히 "붓 레이가 맞혀야 하는 대상"의
> 문제다. 이 변경으로 IgnoreCollision 관련 코드를 함께 정리할 필요는 없다.

#### 3.7.3 대안 B(부차적, A와 독립적으로 결정 가능) — "드래그로 문지르기"에서 "조준 후 발사"로 입력 모델 자체를 변경

현재는 `Input.GetMouseButton(0)`(누르고 있는 동안 매 프레임)로 마우스를 드래그하며 문지르는
"붓" 입력이다. "Ray를 쏜다"는 표현을 문자 그대로 받아들이면, 클릭 1회 = 레이 1발 발사(스프레이
건/총 방식)로 입력 모델 자체를 바꾸는 방향도 가능하다:

```csharp
// PlayerPaintCanvas.cs — 대안 B 적용 시 입력 조건만 교체(대안 A와 조합 가능, 독립적 선택)
if (!Input.GetMouseButtonDown(0)) return; // GetMouseButton → GetMouseButtonDown으로 교체
```
이 변경은 "화면에 자기 몸을 문질러야 하는" 3인칭 자기 채색 UX를 유지한 채 판정 빈도만
줄이는 것이라, 3.7.1의 버그 원인(실시간 베이크 콜라이더)과는 독립적인 별개의 UX 결정이다 —
대안 A 없이 B만 적용해도 버그 자체는 해결되지 않는다.

#### 3.7.4 권장 및 다음 단계

**권장**: 대안 A(정적 프록시 콜라이더)를 채택해 사용자가 지적한 버그 요인을 구조적으로
제거하고, 대안 B(클릭 1회 = 1발)는 UX 취향 문제로 별도 결정한다. 실제 착수 전 사용자 확인이
필요한 이유는 "프록시 메시를 누가·어떻게 만드는지"(§14.1 신규 항목)가 정해져야 하기 때문이다
— §12/§14에 열린 질문으로 등록해둔다.

---

## 4. 그랩 / 캐리(쿠키 상호작용) + 괴물 타격(균열→파괴, v3.2 재설계)

### 4.1 쿠키 ↔ 쿠키 그랩/캐리 (변경 없음)

괴물의 공격(§4.2)과 그랩(쿠키끼리 서로 들고 나르는 것)은 원래부터 별개 시스템이고, 이번
개정에서도 **쿠키↔쿠키 그랩/캐리는 그대로 유지**된다. 애니메이션 검증 결과(§11)도 변경 없다:

- `Cookie_Carrying.fbx`는 상반신 전용 Avatar Mask 레이어로 `Cookie_Walking.fbx`와 조합 가능(§11.2).
- `Cookie_Hanging_Idle.fbx`는 별도 검증 보류 상태 유지(§11.3).
- 그랩 시작 시 도입 모션은 `ReplayJump()`와 동일한 `Animator.Play()` 하드컷 패턴 권장(§11.4).

넷코드 설계(소유권 이전 없이 `carrySocket` 로컬 추적), `SetCarryLayerWeight()`,
`OnGrabbedByOwner`/`OnReleased` 전부 변경 없음.

### 4.2 괴물 ↔ 쿠키 타격 — 균열(1회) → 파괴(2회) (v3.2, 포획 개념 완전 폐기)

> ⚠️ **이 섹션은 §4.4에서 (A)로 확정되며 대체됐다** — 아래의 `MonsterStrikeAttack.cs` 골격과
> "균열(1회)→파괴(2회)" 2단계 설계는 구현하지 않는다. `hitCount`는 `GrabKill` 한 번의 트리거로
> 0에서 곧장 2가 된다. **균열 단계를 완전히 폐기하는 것도 v3.6에서 최종 확정됐다**("`GrabKill`로만
> 할 거고, 타격은 하지 않을 것" — 사용자 확인) — `_CrackAmount` 셰이더 확장 아이디어를 포함해
> 아래 문단의 균열 관련 서술은 전부 폐기된 (B)안의 잔재다. 다만 `hitCount`/`PlayerCrackDisplay`의
> 소유권·네트워크 설계 근거(아래 "실제 코드베이스 기준 설계 근거" 문단)는 §4.4가 그대로
> 재사용하므로, 이 섹션 자체를 삭제하지 않고 근거 자료로 남겨둔다. 최신 설계는 §4.4를 참고할 것.

사용자 확인: "괴물이 근접 포획을 하는 것이 아니다. 쿠키를 부수는 형태로 할 것이고, 한 번
공격했을 때 쿠키가 사방에 금이 가는 형태(부숴지기 전 형태)가 되며, 두 번 타격했을 경우
쿠키가 부숴지는 연출을 할 것." — 직전에 설계했던 "촉수/손으로 붙잡아 데려가는 포획"(`IsCaught`,
`RequestCapture`)은 **이 설계로 완전히 대체된다.** 손/촉수가 물리적으로 별개 부위라는 T-pose
확인 결과(§4.2 이전 조사, 원형 머리+고깔모자 삐에로 몬스터, 앞쪽 관절형 손 2개+뒤쪽 촉수 2개)는
그대로 유효하다 — 이번에 바뀐 것은 "잡아서 뭘 하는지"이지 "무엇으로 닿는지"가 아니다.

**실제 코드베이스 기준 설계 근거**: 이 프로젝트에는 이미 "네트워크로 동기화된 한 캐릭터의
상태를, 그 캐릭터의 소유 여부와 무관하게 모든 클라이언트가 각자 로컬로 시각화한다"는 정확한
전례가 있다 — `PlayerColorVoteIndicator.OnPlayerPropertiesUpdate(Player targetPlayer, ...)`가
`targetPlayer != pv.Owner`만 걸러내고, `pv.IsMine` 여부는 전혀 확인하지 않는다(투표색 표시는
소유자 자신을 포함해 모두가 봐야 하므로). 균열/파괴 시각 효과도 정확히 같은 성격이다 — 괴물을
포함해 모두가 "이 쿠키가 몇 대 맞았는지"를 봐야 한다. 또한 `PlayerPaintCanvas.InitPaintCanvas()`는
이미 캐릭터별 런타임 머티리얼 인스턴스(`new Material(paintedSkinShader)`)를 만들어
`bodyRenderer.material`에 `_MainTex`/`_PaintTex`를 설정해두고 있다 — 균열 표시는 같은 셰이더에
`_CrackAmount` 프로퍼티 하나만 얹으면 되는 자연스러운 확장이다(§10.5).

**설계**:
1. `HideOrSeekPlayer`가 `hitCount`(0~2)를 자기 자신만 갱신한다(소유권 원칙, 기존
   `VoteColorIndex`/`RegisteredSlotCount`와 동일하게 `PhotonNetwork.LocalPlayer.SetCustomProperties`).
2. 괴물의 타격은 대상 쿠키의 `PhotonView`에 `RequestHit` RPC를 보낼 뿐이고, 실제 카운트
   증가·상태 전이는 **맞은 쿠키 자신의 클라이언트만** 확정한다(`RequestCapture`가 쓰던 것과
   동일한 "판정은 상대가, 확정은 대상 자신이" 패턴).
3. `hitCount==1`(균열)은 애니메이션 상태를 바꾸지 않는다 — 계속 걷고 뛸 수 있고, 균열은 순수
   시각 효과(신규 `PlayerCrackDisplay`)로만 표현된다. `hitCount>=2`(파괴)에서만
   `IsMovementLocked=true` + `PlayerMoveState.Broken`(§9)으로 전이해 관전(§6.3)으로 넘어간다.

```csharp
// Assets/02. Scripts/Monster/MonsterStrikeAttack.cs — MonsterPlayer 프리팹 전용 (잠정 골격, 세부 미확정)
public class MonsterStrikeAttack : MonoBehaviourPunCallbacks
{
    [SerializeField] private PhotonView pv;
    [SerializeField] private Transform tentacleStrikePoint; // 뒤쪽 촉수 타격 판정 원점
    [SerializeField] private Transform handStrikePoint;     // 앞쪽 손 타격 판정 원점
    [SerializeField] private float strikeRadius = 1.5f;      // 임시값, 밸런스 미정(§12)
    [SerializeField] private LayerMask cookieLayer;

    // 판정을 언제 시도하는지(키 입력/자동 근접/애니메이션 이벤트) 자체가 아직 미정(§12) —
    // 아래는 "닿으면 때린다"는 요구만 반영한 최소 골격이다.
    private void TryStrikeCookie(Transform strikePoint)
    {
        Collider[] hits = Physics.OverlapSphere(strikePoint.position, strikeRadius, cookieLayer);
        if (hits.Length == 0) return;

        var cookie = hits[0].GetComponentInParent<HideOrSeekPlayer>();
        if (cookie == null) return;

        cookie.GetComponent<PhotonView>().RPC("RequestHit", RpcTarget.All);
    }
}
```

```csharp
// HideOrSeekPlayer.cs — v3(포획)의 IsCaught/RequestCapture를 완전히 대체
private int hitCount; // 0=정상, 1=균열, 2=파괴(§9)

[PunRPC]
private void RequestHit()
{
    if (!pv.IsMine || hitCount >= 2) return; // 본인 클라이언트만 자기 상태 확정(소유권 원칙, §4.1/§4.2 공통)
    hitCount++;
    PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { NetKeys.HitCount, hitCount } });

    if (hitCount >= 2)
    {
        IsMovementLocked = true;
        animationDriver.ChangeState(PlayerMoveState.Broken); // §9 — 파괴 연출 시작, 이동 애니메이션 계열과 동일한 트리거 체계
    }
    // hitCount == 1(균열)은 여기서 아무 것도 더 하지 않는다 — 상태 전이가 아니라 시각 효과일 뿐이므로
    // 실제 표시는 PlayerCrackDisplay(아래)가 CustomProperties 변화를 감지해 담당한다.
}
```

```csharp
// Assets/02. Scripts/ColorTag/PlayerCrackDisplay.cs — PlayerColorVoteIndicator와 완전히 동일한 구조:
// 소유권과 무관하게 "이 캐릭터가 파괴됐는지"를 그 캐릭터 자신의 시각 효과로 반영한다.
// (v3.6) 균열(hitCount==1) 개념이 완전히 폐기되어(§4.4, 사용자 확인 — "GrabKill로만 할 거고
// 타격은 하지 않을 것"), _CrackAmount 셰이더 분기를 제거하고 파괴(hitCount==2) 전용으로
// 단순화했다. hitCount는 이제 0 또는 2 두 값만 존재하므로 "몇 대 맞았는지"가 아니라
// "파괴됐는지 여부"만 표시하면 된다 — 클래스 이름은 §4.2에서 이어받은 최소 변경 원칙에 따라
// 그대로 유지한다.
public class PlayerCrackDisplay : MonoBehaviourPunCallbacks
{
    [SerializeField] private PhotonView pv;
    [SerializeField] private Renderer bodyRenderer; // PlayerPaintCanvas.bodyRenderer와 같은 렌더러를 인스펙터에서 동일하게 연결
    [SerializeField] private GameObject breakVfxPrefab; // §10.6, 로컬 Instantiate

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (targetPlayer != pv.Owner) return;
        if (!changedProps.ContainsKey(NetKeys.HitCount)) return;

        int hitCount = (int)targetPlayer.CustomProperties[NetKeys.HitCount];
        if (hitCount >= 2) PlayBreakEffect(); // hitCount==1은 도달 불가능해 분기 자체가 없음(v3.6)
    }

    private void PlayBreakEffect()
    {
        if (bodyRenderer != null)
            bodyRenderer.enabled = false; // 원본 메시를 감추고 파편 연출로 대체
        if (breakVfxPrefab != null)
            Instantiate(breakVfxPrefab, transform.position, transform.rotation); // 전원 로컬 재생, 정밀 동기화 불필요(§5와 동일 철학)
    }
}
```

> 📌 **`PlayerCrackDisplay`를 `PlayerPaintCanvas`에 얹지 않고 별도 컴포넌트로 분리한 이유**:
> `research.md` §5.1/§5.11이 이미 확인했듯 이 프로젝트는 `Unit/`을 이동·접지·애니메이션·
> 네트워크·표시 5개로, `ColorTag/`를 계층별로 잘게 분리하는 컨벤션을 일관되게 지켜왔다.
> "타격 시각화"는 "색칠 캔버스 관리"(`PlayerPaintCanvas`의 책임)와 다른 책임이므로, 같은
> 관례를 따라 독립 컴포넌트로 둔다.
>
> 손과 촉수 둘 다 최종적으로는 동일한 `RequestHit` RPC로 귀결되므로(어느 부위로 맞았는지는
> `hitCount` 증가 자체에 영향을 주지 않는다), §12의 "촉수/손 중 무엇을 쓸지"가 아직 확정 안
> 돼도 넷코드·상태 설계 자체는 이미 완결돼 있다 — 남은 결정은 순수하게 "타격 판정 원점이
> 어디인지"와 "애니메이션이 무엇인지"에만 영향을 준다.

### 4.3 `TentacleDash` — 괴물 신규 이동 스킬 (신규, v3.3)

사용자 지시: "TentacleDash는 신규 스킬인데, 쿨타임 15초짜리 스킬이며, 이 스킬을 쓰면 20m로
이동하는 스킬이야." `Assets/Animation/MonsterAnimator.controller`를 직접 열어 확인한 결과
`TentacleDash`가 이미 트리거 파라미터+상태로 등록돼 있다(§11.1) — 애니메이션 배선 자체는
끝나 있고, 이동 로직만 새로 설계하면 된다.

**설계 근거**: 이 프로젝트에는 이미 "고정 거리를 순간적으로 이동하는 스킬"의 전례가 있다 —
`HideOrSeekPlayer`의 회피(Dodge, `CheckDodgeInput()`/`DodgeOut()`)다. 다만 회피는 "지속시간
동안 가속 이동"이고 `TentacleDash`는 "고정 거리(20m) 이동"이라는 차이가 있어, 벽을 뚫고
지나가지 않도록 사전에 사거리를 검사하는 가드가 추가로 필요하다:

```csharp
// Assets/02. Scripts/Monster/MonsterMoveState.cs (신규) — PlayerMoveState와 동일한 계약(Animator
// 파라미터명과 정확히 일치해야 함, research.md §2.4). MonsterAnimator.controller의 실제 트리거
// 파라미터는 현재 "GrapKill"(오타, b 누락)로 등록돼 있으나, 오타를 GrabKill로 정정하기로
// 확정됐다(v3.5) — 아래 enum은 정정된 이름을 미리 반영해둔 것이다. **주의**: Animator
// Controller의 실제 파라미터 이름을 Unity 에디터에서 먼저(또는 동시에) GrabKill로 바꾸지
// 않으면 이 enum과 어긋나 트리거가 걸리지 않는다 — §11.1/§14.3에 선행 작업으로 등록.
public enum MonsterMoveState
{
    Idle,
    Walk,
    TentacleDash,
    GrabKill,
}
```

```csharp
// Assets/02. Scripts/Monster/MonsterTentacleDash.cs (신규) — 순수 C# 클래스(Unity 생명주기 없음),
// Unit/ 도메인의 PlayerGroundDetector/PlayerAnimationDriver와 동일한 "조정자(MonoBehaviour)가
// 소유하는 협력 클래스" 스타일을 그대로 따른다(research.md §2.4).
using UnityEngine;

public class MonsterTentacleDash
{
    private const float DashDistance = 20f;   // 사용자 지정값
    private const float DashDuration = 0.25f; // 20m를 0.25초에 주파 = 80m/s — 밸런스 값, §12 열린 질문
    private const float CooldownDuration = 15f; // 사용자 지정값
    private const float DashRadius = 0.4f;    // SphereCast 반경(캐릭터 대략 두께), §12 열린 질문

    private float cooldownTimer;
    private bool isDashing;
    private float dashTimer;
    private Vector3 dashDirection;
    private float actualDashDistance;

    public bool IsDashing => isDashing;
    public float CooldownRemaining01 => Mathf.Clamp01(cooldownTimer / CooldownDuration); // 쿨다운 게이지 UI용

    public bool TryStartDash(Vector3 forward, Vector3 origin, LayerMask obstructionMask)
    {
        if (isDashing || cooldownTimer > 0f) return false;

        dashDirection = forward;
        actualDashDistance = DashDistance;

        // 벽 등 장애물을 뚫고 지나가지 않도록 시작 시점에 사거리를 미리 클램프한다 —
        // HideOrSeekPlayer가 ContinuousDynamic 충돌 감지로 "빠른 이동이 얇은 지형을 뚫는 문제"를
        // 막는 것과 같은 목적이지만, 이쪽은 순간 이동에 가까운 거리라 물리 스텝에 맡기지 않고
        // SphereCast로 사전 검사한다.
        if (Physics.SphereCast(origin, DashRadius, forward, out RaycastHit hit, DashDistance, obstructionMask))
            actualDashDistance = Mathf.Max(0f, hit.distance - DashRadius);

        isDashing = true;
        dashTimer = DashDuration;
        cooldownTimer = CooldownDuration;
        return true;
    }

    // 매 FixedUpdate 호출 — 이번 스텝에 이동해야 할 변위(delta)만 반환. 실제 위치 갱신은 호출부 책임
    // (HideOrSeekPlayer.Move()가 rb.linearVelocity만 계산하고 실제 적용은 물리 엔진에 맡기는 것과 같은 역할 분담).
    public Vector3 TickDash(float deltaTime)
    {
        if (!isDashing) return Vector3.zero;

        float step = (actualDashDistance / DashDuration) * deltaTime;
        dashTimer -= deltaTime;
        if (dashTimer <= 0f) isDashing = false;

        return dashDirection * step;
    }

    public void TickCooldown(float deltaTime)
    {
        if (cooldownTimer > 0f) cooldownTimer = Mathf.Max(0f, cooldownTimer - deltaTime);
    }
}
```

```csharp
// Assets/02. Scripts/Monster/MonsterController.cs — §6.2 골격에 TentacleDash 통합(v3.3 추가분)
[SerializeField] private LayerMask obstructionMask;
private readonly MonsterTentacleDash tentacleDash = new MonsterTentacleDash();
private MonsterMoveState currentState = MonsterMoveState.Idle;

private void Update()
{
    if (!pv.IsMine) return;
    // ...§6.2의 기존 마우스 시점 회전 처리...

    tentacleDash.TickCooldown(Time.deltaTime);

    // 입력 키는 좌Shift로 확정(사용자 확인, v3.4) — 나중에 키 설정 UI로 이관 예정. 쿠키의
    // Shift(질주)와는 서로 다른 캐릭터 클래스에서 각자 로컬로 읽는 입력이라 충돌 없음
    if (Input.GetKeyDown(KeyCode.LeftShift) && tentacleDash.TryStartDash(transform.forward, transform.position, obstructionMask))
        ChangeState(MonsterMoveState.TentacleDash);
}

// Rigidbody 조작은 물리 스텝에서만(HideOrSeekPlayer.FixedUpdate()와 동일 관례) — 이동 방식이
// Rigidbody 물리 기반으로 확정됨에 따라(v3.4) transform.position 직접 대입에서 전환했다.
// rb/moveInput 필드는 아래 §6.2의 MonsterController 본체 정의를 그대로 사용한다.
private void FixedUpdate()
{
    if (!pv.IsMine) return;

    if (tentacleDash.IsDashing)
    {
        // 순간이동에 가까운 고정 변위라 속도(velocity)가 아니라 rb.MovePosition으로 직접
        // 이동시킨다 — 아래 §6.2 기본 이동(rb.linearVelocity)과 방식이 다른 것은 의도적.
        rb.MovePosition(rb.position + tentacleDash.TickDash(Time.deltaTime));
    }
    else
    {
        Vector3 horizontal = moveInput * speed;
        rb.linearVelocity = new Vector3(horizontal.x, rb.linearVelocity.y, horizontal.z);
    }
}
```

**네트워크 동기화**: `Unit/`이 `PlayerNetworkSync`(위치/회전/상태를 `IPunObservable`로 송수신)를
쓰는 것과 정확히 같은 구조를 괴물에도 병렬로 둔다 — Player와 Monster 도메인이 서로 참조하지
않는 이 프로젝트의 기존 컨벤션(research.md §5.1)을 그대로 지킨다:

```csharp
// Assets/02. Scripts/Monster/MonsterNetworkSync.cs (신규) — PlayerNetworkSync.cs와 완전히 동일한
// 구조(Write/Read/Interpolate), 타입만 MonsterMoveState로 교체. 코드 중복이지만, 두 도메인을
// 서로 참조하게 만들지 않기 위한 의도적 선택 — Unit/과 ColorTag/가 이미 이 원칙을 지켜왔다.
```

> 📌 대시가 원격 클라이언트에는 `PlayerNetworkSync.Interpolate()`와 동일한 거리 기반 스냅
> 로직(기본 `snapDistance=10`)으로 보이는데, 20m 대시는 스냅 거리보다 커서 **원격에서는 순간
> 이동(스냅)처럼 보인다** — 부드러운 보간이 아니라 즉시 전환이라 오히려 "촉수로 확 당겨진"
> 연출 의도와 자연스럽게 맞아떨어질 가능성이 높지만, 최종 느낌은 실제 확인이 필요하다(§12).

### 4.4 `GrabKill` — 근접 자동 처형, §4.2를 완전히 대체하는 것으로 확정 (v3.4)

사용자 지시: "Monster_GrabKill은 Player가 범위 안에 들어오면 자동으로 발동되는 애니메이션이야.
이거로 인해서 플레이어가 이 애니메이션에 맞춰서 촉수에 잡히면 쿠키가 파괴되는 연출을 할 거야."

이 지시는 §12-1(타격을 언제 시도하는지)을 **"자동 근접"**으로 확정한다. 판정 원점이 이제
"괴물이 손/촉수를 휘두르는 판정"이 아니라 **"괴물의 몸 자체에 붙은 트리거 범위"**이므로, 이
프로젝트에 이미 있는 가장 단순한 트리거 패턴을 그대로 재사용할 수 있다 — `VoidKillZone`
(맵 밖으로 떨어지면 `OnTriggerEnter`로 감지해 리스폰시키는 컴포넌트, research.md §2.3)과
완전히 동일한 골격이다:

```csharp
// Assets/02. Scripts/Monster/MonsterGrabKillTrigger.cs (신규) — Monster 프리팹에 부착.
// VoidKillZone.cs(research.md §2.3)의 "OnTriggerEnter로 HideOrSeekPlayer를 찾아낸다" 패턴을 그대로 재사용.
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MonsterGrabKillTrigger : MonoBehaviour
{
    [SerializeField] private PhotonView monsterPv;
    [SerializeField] private MonsterController monsterController; // GrabKill 애니메이션 트리거용(v3.5, 오타 정정 — Animator Controller 파라미터 리네임 선행 필요, §11.1)

    private bool onCooldown; // 동일 대상 중복 트리거 방지 — 쿨다운은 트리거 즉시 시작해 GrabKill 애니메이션 재생이 끝나는 시점까지 지속된다(사용자 확인, v3.5). 아래 ResetTrigger() 참고

    private void OnTriggerEnter(Collider other)
    {
        if (!monsterPv.IsMine) return; // 판정 시도는 괴물 소유 클라이언트만 — §4.2가 이미 세운 관례와 동일
        if (onCooldown) return;

        var cookie = other.GetComponentInParent<HideOrSeekPlayer>();
        if (cookie == null) return;

        onCooldown = true;
        monsterController.PlayGrabKill(); // MonsterNetworkSync를 통해 전원에게 GrabKill 애니메이션 전파(v3.5, 오타 정정)

        cookie.GetComponent<PhotonView>().RPC("RequestGrabKill", RpcTarget.All);
    }

    // GrabKill 애니메이션 재생이 끝나는 시점에 호출한다(확정, v3.5) — HideOrSeekPlayer의
    // PlayerAnimationDriver.HandleJumpAnimationHold()가 AnimatorStateInfo.normalizedTime로
    // 재생 진행도를 감지하는 것과 동일한 방식으로, MonsterController가 매 프레임 GrabKill
    // 상태의 normalizedTime >= 1을 감지해 이 메서드를 호출하는 구현을 권장한다(애니메이션
    // 이벤트로 직접 호출하는 대안도 가능, 구현 시 선택).
    public void ResetTrigger() => onCooldown = false;
}
```

```csharp
// HideOrSeekPlayer.cs — §4.2의 RequestHit(타격 2회: 균열→파괴)을 완전히 대체한다(v3.4, (A) 확정). RequestHit 자체는 이제 쓰이지 않는다.
[PunRPC]
private void RequestGrabKill()
{
    if (!pv.IsMine || hitCount >= 2) return; // 본인 클라이언트만 자기 상태 확정 — 기존 원칙 그대로 유지

    hitCount = 2; // 균열 단계 없이 곧바로 파괴 상태로 — (A) 확정에 따라 §4.2의 "1회=균열, 2회=파괴" 2단계 설계 자체가 폐기됨(v3.4). hitCount==1은 이제 도달 불가능한 죽은 값
    PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { NetKeys.HitCount, hitCount } });

    IsMovementLocked = true;
    animationDriver.ChangeState(PlayerMoveState.Broken); // 기존 §9 Broken 상태·§6.3 관전 전환·§8 승리 판정 그대로 재사용
}
```

> 📌 **`PlayerCrackDisplay`(§4.2)와의 관계**: `hitCount`를 그대로 재사용한다 — 파괴 시각 효과
> (`PlayerCrackDisplay.PlayBreakEffect()`, 렌더러 끄고 파편 VFX 재생)는 `hitCount`가 어떻게
> 2에 도달하는지와 무관하게 그대로 작동한다. `_CrackAmount` 분기는 균열 폐기 확정(v3.6)에
> 따라 이미 제거해뒀다(§4.2의 최종 코드 참고).

**✅ §4.2와의 관계 — (A)로 확정(v3.4, 사용자 확인)**: `GrabKill`이 §4.2의 "손/촉수로 1회 타격 →
균열(hitCount=1), 2회 타격 → 파괴(hitCount=2)"라는 수동 2단계 공격 설계를 **완전히 대체**한다.
괴물에게는 별도의 수동 공격 입력이 아예 없고, 근접 트리거만으로 즉시 처형된다(위 코드의
`RequestGrabKill`이 `hitCount`를 곧바로 2로 세팅하는 것이 그대로 최종 설계다).

**이 확정에 따라 폐기/불필요해지는 것(v3.4)**:
- `Monster/MonsterStrikeAttack.cs`(손/촉수 수동 타격 골격, §4.2/§10.1) — **구현하지 않는다.**
- "균열(hitCount==1)" 상태 자체 — 도달할 방법이 없는 죽은 값이 된다. §4.2 본문·§6.3의 균열
  서술은 폐기된 (B)안의 잔재이므로 실제 구현 시 참고하지 않는다.
- 손/촉수 타격 스윙 애니메이션(§10.4, §14.1) — `GrabKill` 클립 하나로 연출이 끝나므로 별도
  확보 불필요.
- 촉수·손 병행 여부, 판정 범위·쿨다운 차등 여부(구 §12-2/§12-6) — "손"이라는 선택지 자체가
  없어져 무효화.

> ✅ **모순 해소 — 균열 완전 폐기로 최종 확정(v3.6)**: v3.4에서 발견됐던 모순(사용자가 §12에서
> 답한 "균열 이동 제약은 지금 고려하지 않는다"·"균열은 영구적이다, 회복 없음"이 실제로는
> 존재하지 않는 균열 상태를 전제한 무효 답변이었던 문제)은 사용자가 "`GrabKill`로만 할 거고,
> 타격은 하지 않을 것"이라 재확인하며 해소됐다. 균열(hitCount==1) 단계는 잠깐도 남기지 않고
> 완전히 삭제한다 — 두 답변은 이제 적용 대상이 아예 없으므로 그대로 무효로 둔다.

---

## 5. 던지기 (사물) — **완전 폐기**

v1부터 있던 "소품을 던져 괴물의 주의를 끄는" 기능은 이번 개정으로 완전히 제거된다(사용자
확인). `ThrowableProp`/`NoiseListener`와 `NoisePing` 이벤트는 전부 폐기 대상이며, §9(EventCodes)·
§10(에셋 목록)에서도 관련 항목을 제거했다.

폐기되는 것은 "사물을 던져 소음으로 주의를 끄는" 게임플레이 기능이며, 그랩한 쿠키를 놓아주는
동작(§4.1의 `OnReleased(withThrow, ...)`)과는 무관하다 — 그쪽은 그대로 유지된다.

---

## 6. 술래잡기 본게임

### 6.1 쿠키 시야 축소 — 안개 (변경 없음, 생략)

### 6.2 괴물 시점 — **1인칭으로 확정(v3.2)**

사용자 확인: "괴물에 대한 카메라는 1인칭 시점으로 해야 될 것. 3인칭으로 하기에는 힘들
것같다." v3에서 "마법이 없어졌으니 1인칭 근거가 약하다"며 3인칭 재사용 쪽으로 기울었던 판단을
뒤집는다 — §12의 카메라 질문은 **1인칭으로 최종 확정**됐다.

**실제 코드베이스 기준 설계**: 이 프로젝트는 이미 "씬에 고정 배치된 단일 Main Camera
오브젝트를, 로컬 플레이어가 스폰될 때 자기 자신을 넘겨 초기화시키는" 패턴을 갖고 있다 —
`HideOrSeekPlayer.cs`의 실제 코드:
```csharp
// HideOrSeekPlayer.cs Awake() 중 일부 (실제 코드)
Camera_Ctrl camCtrl = Camera.main != null ? Camera.main.GetComponent<Camera_Ctrl>() : null;
if (camCtrl != null)
    camCtrl.InitCamera(gameObject);
```
그리고 `Camera_Ctrl.InitCamera(GameObject player)`는 `Awake()`/`Start()` 중 어느 쪽이 먼저
호출되든 항상 정확히 초기화되도록 설계돼 있다(`ResetToDefaultView()`를 양쪽에서 공유).

**괴물도 이 "Main Camera에 나를 넘긴다" 흐름 자체는 그대로 재사용**하지만, `Camera_Ctrl` 본체는
개조하지 않는다 — `Camera_Ctrl`은 우클릭 드래그로만 회전하고 고정 거리(`m_DefaultDist`)로
캐릭터를 뒤에서 따라다니는 **3인칭 궤도 카메라**로 설계돼 있어(회전은 카메라만, 캐릭터 몸은
이동 방향으로만 도는 구조), 1인칭에는 구조적으로 맞지 않는다. 대신 같은 Main Camera
오브젝트에 **별도 컴포넌트를 나란히** 붙인다 — 한 클라이언트는 쿠키 아니면 괴물 둘 중 하나만
플레이하므로 두 컴포넌트가 동시에 활성화될 일이 없다:

```csharp
// Assets/02. Scripts/Monster/MonsterFirstPersonCamera.cs (신규, v3.2)
public class MonsterFirstPersonCamera : MonoBehaviour
{
    private Transform eyeSocket; // 괴물 머리 안쪽 시점 원점 — 실제 3D 모델에 배치 필요(§14.1)

    public void InitCamera(Transform monsterEyeSocket)
    {
        eyeSocket = monsterEyeSocket;
    }

    private void LateUpdate()
    {
        if (eyeSocket == null) return;

        // 3인칭(Camera_Ctrl)과 달리 거리/오프셋 계산이 없다 — 그냥 눈 위치·방향에 그대로 고정.
        transform.position = eyeSocket.position;
        transform.rotation = eyeSocket.rotation;
    }
}
```

`HideOrSeekPlayer`를 그대로 재사용할 수 없는 이유도 실제 코드에서 확인된다 —
`CheckMovementInput()`은 `Camera.main.transform.forward/right` 기준으로 이동 방향을 계산한다
(3인칭 궤도 카메라를 전제로 한 "카메라 상대 이동"). 1인칭은 반대로 **카메라가 곧 캐릭터
정면**이어야 하므로, 마우스 좌우 입력이 캐릭터 자신의 `transform` 회전(yaw)을 직접 돌려야
하고, 이동은 카메라가 아니라 캐릭터 자신의 `forward`/`right` 기준이어야 한다. 이동 입력 처리
자체가 근본적으로 다르므로, `HideOrSeekPlayer`를 상속/재사용하지 않고 **괴물 전용
컨트롤러**를 새로 둔다(§10.1/§10.2에서도 이미 `MonsterPlayer.prefab`을 별도 프리팹으로
계획했던 것과 일관됨):

```csharp
// Assets/02. Scripts/Monster/MonsterController.cs (신규, v3.2, 이동 로직 v3.4 재작성)
// 이동 방식은 Rigidbody 물리 기반으로 확정됨(사용자 확인, v3.4) — HideOrSeekPlayer.cs와 동일한
// 패턴(로컬만 실제 물리 시뮬레이션, 원격은 isKinematic)을 그대로 따른다(research.md §2.4).
public class MonsterController : MonoBehaviourPunCallbacks
{
    [SerializeField] private PhotonView pv;
    [SerializeField] private Transform eyeSocket;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float speed = 4f;

    private float yaw, pitch;
    private Rigidbody rb;
    private Vector3 moveInput; // Update()에서 입력만 기록, 실제 적용은 FixedUpdate에서(HideOrSeekPlayer와 동일 원칙)

    private void Awake()
    {
        if (!pv.IsMine) return;

        // HideOrSeekPlayer.Awake()가 Camera_Ctrl에 하던 것과 완전히 동일한 "Main Camera에 나를 넘긴다" 패턴.
        var fpsCam = Camera.main != null ? Camera.main.GetComponent<MonsterFirstPersonCamera>() : null;
        fpsCam?.InitCamera(eyeSocket);
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = !pv.IsMine; // 원격은 물리 시뮬레이션 끔(HideOrSeekPlayer.Start()와 동일 이유)
        if (pv.IsMine)
        {
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // TentacleDash 고속 이동 대비
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezeRotation; // 회전은 아래 Update()에서 transform.rotation으로 직접 제어
        }
    }

    private void Update()
    {
        if (!pv.IsMine) return;

        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * mouseSensitivity, -80f, 80f);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);         // 몸체 좌우 회전 = 시점 정면(3인칭과 가장 큰 차이)
        eyeSocket.localRotation = Quaternion.Euler(pitch, 0f, 0f);  // 상하는 눈(카메라)만 따로 숙임/젖힘

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        moveInput = (transform.forward * v + transform.right * h).normalized;
    }

    // Rigidbody 조작은 물리 스텝에서만(HideOrSeekPlayer.FixedUpdate()와 동일 관례). §4.3의
    // TentacleDash 통합 스니펫이 이 FixedUpdate를 확장한다 — 그쪽에 실제 최종 형태가 있다.
    private void FixedUpdate()
    {
        if (!pv.IsMine) return;

        Vector3 horizontal = moveInput * speed;
        rb.linearVelocity = new Vector3(horizontal.x, rb.linearVelocity.y, horizontal.z); // y는 중력이 채운 값 보존
    }
}
```

> 📌 **1인칭 확정으로 드러났던 문제 — 해소(v3.4)**: 손(앞쪽)은 1인칭 시야 안에서 스윙을 직접
> 볼 수 있지만 **촉수(뒤쪽)는 플레이어가 화면으로 볼 수 없는 등 뒤에서 동작한다**는 우려가
> 있었으나, §4.4가 (A)로 확정되며 "손"이라는 선택지 자체가 사라졌고, 판정도 "전방 3m 이내
> 자동 발동"으로 확정됐다(사용자 확인) — 별도의 조준 UI나 반자동 판정을 따로 설계할 필요가
> 없어졌다.

### 6.3 파괴된 쿠키 — Space로 관전 시점 순환 (`IsCaught`→`hitCount>=2`로 재설계)

v3(포획)의 `IsCaught` 불리언은 폐기되고, §4.4(구 §4.2)의 `hitCount`(0 또는 2)가 유일한 진실
소스가 된다. `SpectatorController` 구조 자체는 트리거 조건만 `hitCount >= 2`로 바꾸면 그대로
유효하다. 결과 화면(§8.2) UI 문구도 "잡힘"에서 "부숴짐"으로 바뀐다.

**"구출"/균열 회복 개념 — v3.6에서 최종 폐기 확정**: §4.4가 (A)로 확정되며 `hitCount`가
0에서 곧장 2로 뛰기 때문에, "균열(`hitCount==1`) 상태가 회복되는지"를 묻던 질문 자체가 적용
대상을 잃었었다. 사용자가 "`GrabKill`로만 할 거고, 타격은 하지 않을 것"이라 재확인하며 균열
단계 자체를 완전히 없애는 것으로 최종 확정됐다(§4.4 참고) — "구출"이라는 개념은 이제 이
게임에 존재하지 않는다. 파괴(`hitCount==2`)는 이 프로젝트의 다른 최종 상태 전이(예: 4라운드
완료 후 술래 지정)처럼 되돌릴 수 없는 것으로 유지한다.

### 6.4 괴물 GameScene 합류 → `GameEndTime` 세팅 (변경 없음, 생략)

---

## 7. 괴물(술래) 이탈 & 방장 위임 처리

### 7.1 괴물 이탈 감지 — 다중 괴물 대응

`RoomLifecycleWatcher`의 `IsMonster(player)`(다중 배열 비교)는 "남은 괴물이 0명일 때만" 5초
경고 후 종료한다:

```csharp
// Assets/02. Scripts/Monster/RoomLifecycleWatcher.cs (재작성)
public class RoomLifecycleWatcher : MonoBehaviourPunCallbacks
{
    private double? monstersGoneAt;

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (monstersGoneAt.HasValue) return;
        if (!PhotonNetwork.IsMasterClient) return;
        if (!IsMonster(otherPlayer)) return; // 쿠키가 나간 건 이 경로와 무관(쿠키는 파괴되면 관전만 함, 나가도 게임 계속)

        RemoveFromMonsterList(otherPlayer.ActorNumber);

        if (RemainingMonsterCount() > 0) return;

        monstersGoneAt = PhotonNetwork.Time;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { NetKeys.MonsterDepartedAt, monstersGoneAt.Value },
        });
    }

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!monstersGoneAt.HasValue) return;
        if (PhotonNetwork.Time < monstersGoneAt.Value + 5.0) return;

        monstersGoneAt = null;
        ReturnToGameLobby();
    }

    private bool IsMonster(Player p) =>
        RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters) && monsters.Contains(p.ActorNumber);

    private int RemainingMonsterCount() =>
        RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters) ? monsters.Length : 0;

    private void RemoveFromMonsterList(int actorNumber)
    {
        if (!RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters)) return;
        int[] updated = monsters.Where(a => a != actorNumber).ToArray();
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { NetKeys.MonsterActorNumbers, updated } });
    }
}
```

```csharp
// Assets/02. Scripts/Monster/MonsterDepartureBanner.cs — GameScene에 1개, 전원 대상
public class MonsterDepartureBanner : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject bannerRoot;
    [SerializeField] private TMP_Text bannerText;

    public override void OnRoomPropertiesUpdate(Hashtable changedProps)
    {
        if (!changedProps.ContainsKey(NetKeys.MonsterDepartedAt)) return;
        bannerText.text = "괴물이 나갔습니다. 5초 뒤 게임이 종료됩니다.";
        bannerRoot.SetActive(true);
    }
}
```

### 7.2 방장(MasterClient) 위임 — 변경 없음

Photon Room의 `MasterClientId` 자동 재할당 로직은 캐릭터 명칭과 무관하다. 모든 마스터 권위
로직이 "매 프레임 `IsMasterClient` 확인" 폴링 패턴이므로 별도 코드 없이 자동으로 이어받는다.

### 7.3 다중 괴물 확장 설계 요약

| 항목 | 단일 괴물(현재 규칙) | 다중 괴물(확장 시) |
|---|---|---|
| `NetKeys.MonsterActorNumbers` | `int[1]` | `int[N]` — 코드 변경 없이 길이만 늘어남 |
| §2.2 리빌 연출 | 1명만 프리팹 교체 | `Contains()` 검사라 여러 명이 동시에 교체돼도 동작 동일 |
| §4.4 `GrabKill` 처형 | 각자 독립적으로 `MonsterGrabKillTrigger` 보유(v3.4, §4.2 `MonsterStrikeAttack`은 폐기) | 괴물 수만큼 인스턴스가 각자 독립 동작(공유 상태 없음) |
| §7.1 이탈 처리 | 1명 나가면 즉시 0명 → 5초 경고 | N명 중 일부만 나가면 진행, **0명이 될 때만** 5초 경고(이미 구현됨) |
| §8.3 승리 판정 | "쿠키 전원 파괴" 단순 검사 | 변경 없음 — 괴물 수와 무관하게 쿠키 쪽만 검사하면 됨 |

---

## 8. 승리 조건 + 결과 화면

### 8.1 판정 로직 (`hitCount>=2`로 재설계, 다중 괴물 대응)

```csharp
// Assets/02. Scripts/Monster/GameRuleController.cs
public class GameRuleController : MonoBehaviourPunCallbacks
{
    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters)) return;
        if (!RoomState.TryGetInt(NetKeys.MonsterJoined, out _)) return;
        if (RoomState.TryGetInt(NetKeys.GameResult, out _)) return;

        if (AllCookiesBroken(monsters))
        {
            Finish(GameResult.MonsterWins);
            return;
        }

        if (RoomState.TryGetDouble(NetKeys.GameEndTime, out double endTime) && PhotonNetwork.Time >= endTime)
        {
            Finish(GameResult.CookiesWin);
        }
    }

    private bool AllCookiesBroken(int[] monsters)
    {
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            if (monsters.Contains(p.ActorNumber)) continue;
            int hitCount = p.CustomProperties.TryGetValue(NetKeys.HitCount, out object v) ? (int)v : 0;
            if (hitCount < 2) return false;
        }
        return true;
    }

    private void Finish(GameResult result)
    {
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { NetKeys.GameResult, (int)result } });
    }
}

public enum GameResult { CookiesWin, MonsterWins }
```

### 8.2 결과 화면 UI (`Assets/Screenshots/Result.png` 반영, "잡힘"→"부숴짐")

레퍼런스 이미지 구성 분석:
- **상단 배너**: "괴물 승!"(보라) / "쿠키 승!"(금색) 텍스트 + 승리 사유 부제
- **좌측 일러스트**: 승리 진영에 맞춰 괴물 단독 일러스트 또는 쿠키 4인 축하 일러스트
- **중앙 패널 "남은 쿠키 수"**: `생존수/4` 분수 + 쿠키 아이콘 4개, 파괴된 쿠키는 회색 실루엣
- **우측 패널**: 플레이어 목록 — 괴물 행은 왕관 아이콘+"(괴물)" 라벨, 쿠키 행은 "✕ 부숴짐"
  (빨강) 또는 "○ 생존"(파랑/흰색)
- **하단**: "로비로 이동 (12)" 버튼

```csharp
// Assets/02. Scripts/Monster/ResultScreenController.cs — GameScene에 1개, 전원 대상
public class ResultScreenController : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject root;
    [SerializeField] private GameObject monsterWinBanner;
    [SerializeField] private GameObject cookieWinBanner;
    [SerializeField] private TMP_Text remainingCountText;
    [SerializeField] private Image[] cookieIcons;          // 4개, 생존=컬러/파괴=그레이스케일
    [SerializeField] private Transform playerListContent;
    [SerializeField] private PlayerResultRow playerRowPrefab; // 이름 + 상태(괴물/부숴짐/생존)
    [SerializeField] private TMP_Text lobbyButtonCountdownText;
    [SerializeField] private float autoReturnDelay = 12f;

    public override void OnRoomPropertiesUpdate(Hashtable changedProps)
    {
        if (!changedProps.ContainsKey(NetKeys.GameResult)) return;
        if (!RoomState.TryGetInt(NetKeys.GameResult, out int result)) return;

        ShowResult((GameResult)result);
    }

    private void ShowResult(GameResult result)
    {
        root.SetActive(true);
        monsterWinBanner.SetActive(result == GameResult.MonsterWins);
        cookieWinBanner.SetActive(result == GameResult.CookiesWin);

        RoomState.TryGetIntArray(NetKeys.MonsterActorNumbers, out int[] monsters);
        int aliveCount = 0;
        int cookieIndex = 0;

        foreach (Player p in PhotonNetwork.PlayerList.OrderBy(pl => pl.ActorNumber))
        {
            var row = Instantiate(playerRowPrefab, playerListContent);
            bool isMonster = monsters != null && monsters.Contains(p.ActorNumber);
            int hitCount = p.CustomProperties.TryGetValue(NetKeys.HitCount, out object v) ? (int)v : 0;
            bool isBroken = hitCount >= 2;

            if (isMonster) { row.SetMonster(p.NickName); continue; }

            row.SetCookie(p.NickName, alive: !isBroken);
            if (cookieIndex < cookieIcons.Length) cookieIcons[cookieIndex].color = isBroken ? Color.gray : Color.white;
            cookieIndex++;
            if (!isBroken) aliveCount++;
        }

        remainingCountText.text = $"{aliveCount} / 4";
        StartCoroutine(AutoReturnCountdown());
    }

    private IEnumerator AutoReturnCountdown()
    {
        float remaining = autoReturnDelay;
        while (remaining > 0f)
        {
            lobbyButtonCountdownText.text = $"로비로 이동 ({Mathf.CeilToInt(remaining)})";
            remaining -= Time.deltaTime;
            yield return null;
        }
        OnLobbyButtonClicked();
    }

    public void OnLobbyButtonClicked()
    {
        StopAllCoroutines();
        if (PhotonNetwork.IsMasterClient)
            PhotonNetwork.LoadLevel(SceneNames.GameLobby);
    }
}
```

> 📌 정상 승리 종료(§8)는 §7(괴물 전원 이탈로 인한 비정상 종료)과 달리 **괴물과 쿠키 모두
> 함께 `GameLobbyScene`으로 복귀**해도 무방하다 — `PhotonNetwork.LoadLevel()`을 그대로 써도
> 안전하다.

---

## 9. `PlayerMoveState` / NetKeys / NetEventCodes 최신 목록 (v3.2 전면 갱신)

```csharp
public enum PlayerMoveState
{
    Idle, Walk, Run, Jump, Dodge,
    Held,   // 그랩당한 상태(§4.1, 유지)
    Broken, // GrabKill로 즉시 파괴된 상태(§4.4/§6.3) — v3의 Caught를 대체. §4.2의 균열(1회 피격)
            // 단계는 (A) 확정으로 폐기되어(v3.4), 이 상태로 전이하는 유일한 경로가 GrabKill이 됨.
}
```

```csharp
public static class NetKeys
{
    public const string GameEndTime = "GameEndTime"; // 기존 값 실사용(§6.4)

    // 괴물 선정(§2)
    public const string MonsterActorNumbers = "MonsterActorNumbers"; // int[]
    public const string MonsterRevealTime = "MonsterRevealTime";
    public const string CookiesDeparted = "CookiesDeparted";

    // 페인트 페이즈(§3)
    public const string PaintPhaseEndTime = "PaintPhaseEndTime";
    public const string MonsterJoined = "MonsterJoined";
    public const string ForcedPaintActorNumbers = "ForcedPaintActorNumbers"; // int[] — §3.6
    public const string ForcedPaintColors = "ForcedPaintColors"; // int[] — ForcedPaintActorNumbers와 인덱스로 1:1 대응

    // 이탈/방장 위임(§7)
    public const string MonsterDepartedAt = "MonsterDepartedAt";

    // 승패(§8)
    public const string GameResult = "GameResult";

    // Player CustomProperties
    public const string HitCount = "HitCount"; // int(0 또는 2) — §4.2 신규(v3.2), v3의 IsCaught를 대체. §4.4가 (A)로 확정되며(v3.4) 1(균열)은 도달 불가능한 죽은 값이 됨 — 0=정상, 2=파괴만 실제로 쓰임
    public const string RegisteredSlotCount = "RegisteredSlotCount"; // int — §3.2
    public const string SkinIndex = "SkinIndex"; // int(0~2, A/B/C) — §1.5 신규(v3.3)
}

// Monster/MonsterMoveState.cs — §4.3에서 이미 정의(재게재). MonsterAnimator.controller의 실제
// 트리거 파라미터 4개와 정확히 일치해야 하는 계약(research.md §2.4와 동일한 성격). GrabKill로
// 오타 정정 확정(v3.5) — Animator Controller 파라미터도 함께 리네임해야 함(§11.1/§14.3).
public enum MonsterMoveState { Idle, Walk, TentacleDash, GrabKill }

public static class NetEventCodes
{
    public const byte PaintStroke = 1;
    public const byte ClaimMonster = 2;
    public const byte ClearColor = 3;
    public const byte FillAll = 4;
    // NoisePing = 5 — 폐기(§5)
    // MagicCast = 6 — 폐기(구 §6.2)
}
```

> 📌 괴물의 처형(§4.4)은 `RequestGrabKill` RPC로 처리되므로(v3.4, §4.2의 `RequestHit`을
> 완전히 대체 — `RequestHit`은 더 이상 쓰이지 않는다) 현재는 별도 `RaiseEvent` 코드가 필요
> 없다. 타격 판정 트리거 방식("전방 3m 자동 발동")과 파괴 연출 타이밍("GrabKill 애니메이션
> 재생 종료 시점")이 모두 확정됐으므로(v3.5, §4.4), 신규 `NetEventCodes` 항목 없이 현재
> 구조로 구현 가능하다.

---

## 10. 필요한 프리팹 · 에셋 전체 목록 (v3.2 갱신)

표시 규칙: **기존재 / 확보 / 신규**로 상태를 표시한다.

### 10.1 신규/변경 스크립트·컴포넌트 전체 목록

| 파일 경로 | 역할 | 근거 절 |
|---|---|---|
| `Monster/Cauldron.cs` | 가마솥 트리거, `ClaimMonster` 요청 발신 | §2.1 |
| `Monster/MonsterAssignmentAuthority.cs` | 마스터 전용 괴물 확정(선착순+타임아웃) | §2.1 |
| `Monster/MonsterRevealController.cs` | 리빌 연출 + 쿠키→괴물 프리팹 교체 | §2.2 |
| `Monster/CookieDepartureController.cs` | 쿠키 전용, 10초 후 GameScene 이동 | §2.3 |
| `Monster/MonsterJoinController.cs` | 60초 후 괴물 GameScene 합류 트리거, `GameEndTime` 세팅 | §6.4 |
| `ColorTag/PlayerPaintCanvas.cs`(수정) | 슬롯/임계량 등록+슬롯 수 네트워크 보고, 지우개, 배정된 강제 도포 색 적용 | §3.2, §3.6 |
| `ColorTag/ColorSwatchButton.cs`(수정) | `SetBrushColor()` 호출로 교체 | §3.3 |
| `ColorTag/PaintPhaseController.cs` | 마스터 전용, 등록 슬롯 0개 플레이어에게 서로 겹치지 않는 색을 배정 | §3.6 |
| `ColorTag/RoomState.cs`(수정) | `TryGetIntArray()` 추가 | §7.1, §3.6 |
| `Grab/PlayerGrabController.cs` | 쿠키↔쿠키 그랩 시작/해제(그랩버 측) | §4.1 |
| `Unit/PlayerAnimationDriver.cs`(수정) | `SetCarryLayerWeight()` 추가 | §4.1, §11.2 |
| `Unit/HideOrSeekPlayer.cs`(수정) | `OnGrabbedByOwner`/`OnReleased`/`hitCount` (v3.4 — `RequestHit`은 `RequestGrabKill`로 완전히 대체되어 별도 구현 안 함, §4.4 참고) | §4.1, §4.4 |
| ~~`Monster/MonsterStrikeAttack.cs`~~ | **폐기(v3.4, (A) 확정)** — `GrabKill` 자동 처형이 완전히 대체, 구현하지 않음 | §4.2, §4.4 |
| `ColorTag/PlayerCrackDisplay.cs` | 균열/파괴 시각 효과, 소유권 무관 표시(신규, v3.2) | §4.2 |
| `Monster/MonsterFirstPersonCamera.cs` | 괴물 1인칭 카메라(신규, v3.2 확정) | §6.2 |
| `Monster/MonsterController.cs` | 괴물 전용 이동+시점 회전 컨트롤러(신규, v3.2 — `HideOrSeekPlayer` 재사용 불가 이유는 §6.2), v3.3에서 `TentacleDash` 통합 | §6.2, §4.3 |
| `Monster/MonsterMoveState.cs` | 괴물 애니메이션 상태 enum(신규, v3.3) | §4.3, §9 |
| `Monster/MonsterTentacleDash.cs` | 쿨타임 15초·사거리 20m 돌진 스킬 순수 로직(신규, v3.3) | §4.3 |
| `Monster/MonsterNetworkSync.cs` | 괴물 위치/회전/상태 네트워크 동기화, `PlayerNetworkSync`와 병렬 구조(신규, v3.3) | §4.3 |
| `Monster/MonsterGrabKillTrigger.cs` | 근접 자동 처형 트리거, `VoidKillZone`과 동일 골격(신규, v3.3) | §4.4 |
| `Unit/HideOrSeekPlayer.cs`(수정) | `RequestGrabKill` RPC 추가(v3.3) | §4.4 |
| `Lobby/PlayerSkinSelector.cs` | `GameLobbyScene` 스킨(A/B/C) 선택 UI(신규, v3.3) | §1.5 |
| `Unit/PlayerSkinApplier.cs` | 소유자의 `SkinIndex`를 읽어 `bodyRenderer.sharedMaterial` 적용(신규, v3.3) | §1.5 |
| ~~`ColorTag/PlayerPaintCanvas.cs`(수정, 보류)~~ | 붓칠 판정 대상을 실시간 베이크 콜라이더→정적 프록시로 교체 — **"일단 보류"로 확정(v3.4), 착수하지 않음** | §3.7 |
| `Monster/SpectatorController.cs`(트리거를 `hitCount>=2`로 재설계) | 파괴된 쿠키 시점 순환 | §6.3 |
| `Monster/RoomLifecycleWatcher.cs`(재작성) | 다중 괴물 이탈 감지+5초 경고 | §7.1 |
| `Monster/MonsterDepartureBanner.cs` | 이탈 경고 배너 로컬 표시 | §7.1 |
| `Monster/GameRuleController.cs` | 승리 판정(마스터 전용) | §8.1 |
| `Monster/ResultScreenController.cs` | 결과 화면 표시/자동 복귀 | §8.2 |
| `ColorTag/ColorSelectionManager.cs`, `ColorVoteTally.cs`, `TaggerColorAssigner.cs`, `PlayerColorVoteIndicator.cs`, `PlayerColorDisplay.cs` | **삭제 대상** | §0 |
| ~~`Witch/WitchMagicAttack.cs`~~ | **폐기** — 마법 공격 자체가 없어짐 | 구 §6.2 |
| ~~`Grab/ThrowableProp.cs`, `Grab/NoiseListener.cs`~~ | **폐기** — 소품 던지기 기능 자체가 없어짐 | §5 |

### 10.2 프리팹 목록

**A. 네트워크 프리팹**(`PhotonNetwork.Instantiate` 대상 — 반드시 `Assets/04. Prefabs/Resources/`)

| 프리팹 | 상태 | 부착 컴포넌트 | 근거 |
|---|---|---|---|
| `HideOrSeekPlayer.prefab` | 기존재(수정 필요) | `PlayerGrabController`, `PlayerCrackDisplay`, Animator에 `Carry` 레이어+`Broken` 트리거 추가 | §4.1, §4.2 |
| `MonsterPlayer.prefab` | **신규** | `MonsterController`, `MonsterGrabKillTrigger`, `Rigidbody`, 눈 위치의 `eyeSocket` 자식 Transform(`MonsterFirstPersonCamera`는 Main Camera 쪽에 별도 부착), `PhotonView`+`PhotonTransformView`(v3.4, `MonsterStrikeAttack` 제외) | §2.2, §4.4, §6.2 |
| `BrushCursor.prefab` | 기존재, 변경 없음 | — | (ColorTag 기존) |
| ~~던질 수 있는 소품~~ | **폐기** | — | §5 |

**B. 로컬 전용 프리팹**(각 클라이언트가 로컬 `Instantiate()` — 네트워크 오브젝트 아님)

| 프리팹 | 상태 | 용도 | 근거 |
|---|---|---|---|
| 가마솥 보글보글 파티클(`bubbleFx`) | **신규** | 괴물 미확정 대기 중 연출 | §2.2 |
| 가마솥 짜잔 리빌 파티클(`revealFx`) | **신규** | 괴물 확정 순간 연출 | §2.2 |
| ~~타격 임팩트 이펙트(1회 피격용)~~ | **불필요 확정(v3.6)** — 균열(1회 피격) 완전 폐기, `GrabKill` 단독으로만 파괴 | §4.4 |
| 파괴(shatter) 파편 VFX | **신규**(사실상 필수) | `GrabKill`로 즉시 파괴될 때 `PlayerCrackDisplay.PlayBreakEffect()`가 재생 | §4.4 |

**C. 씬 배치 오브젝트**(프리팹화 권장)

| 프리팹 | 상태 | 용도 | 근거 |
|---|---|---|---|
| `Cauldron.prefab` | **신규** | 가마솥 3D 모델+트리거 콜라이더+`Cauldron`+`MonsterAssignmentAuthority`, `GameLobbyScene`에 1개 | §2.1 |
| 문(door) 프리팹 | **신규**(선택) | 동일 모양 4개 배치, 순수 장식 | §1 |

**D. UI 프리팹**(`Resources/UI/{Popup|Scene|Tab}/{클래스명}` 컨벤션)

| 프리팹 | 상태 | 용도 | 근거 |
|---|---|---|---|
| `Resources/UI/Scene/ColorSlotPanel/ColorSlotPanel.prefab` | **신규**(기존 `ColorSelectionPanel.prefab` 대체) | 4슬롯+Reset+지우개 UI | §3.4 |
| `Resources/UI/Scene/ResultScreen/ResultScreen.prefab` | **신규** | 승패 배너+남은 쿠키 패널+플레이어 목록 루트 | §8.2 |
| `Resources/UI/Scene/ResultScreen/PlayerResultRow.prefab` | **신규** | 결과 화면 플레이어 목록 행(이름+상태) | §8.2 |
| `Resources/UI/Popup/MonsterDepartureBanner/MonsterDepartureBanner.prefab` | **신규** | "괴물이 나갔습니다" 경고 배너 | §7.1 |
| `Resources/UI/Scene/SkinSelectPanel/SkinSelectPanel.prefab` | **신규** | `GameLobbyScene` 상시 노출, 스킨 A/B/C 버튼 3개 | §1.5 |
| `ConfirmDialog`/`GameLobbyPanel`/`LobbyPanel`/`PlayerListItem`/`RoomListItem` | 기존재, 변경 없음 | — | (기존) |
| `ColorSelectionPanel.prefab`(기존) | **폐기 대상** | `ColorSlotPanel`로 대체됨 | §0, §3 |

### 10.3 3D 모델 / 메시

| 항목 | 상태 | 근거 |
|---|---|---|
| 괴물 캐릭터 메시(촉수/손 포함) | **확보(v3.3)** — `Assets/Animation/Monster/Monster_Rigged.fbx`(T-pose 기본 리그)를 직접 열어 확인. `animationType: 2`(Generic)로 이미 임포트돼 있어 §11.1의 "Humanoid 리타겟 불가 → Generic 전환" 방향이 이미 실제로 채택된 상태임을 확인했다. `eyeSocket` 위치는 **임시 배치로 확정(v3.4, 사용자 확인)** — 리그 안의 정확한 위치는 추후 3D 모델 제작 단계에서 결정하고, 그 전까지는 머리 중앙 부근에 임시 Transform을 붙여 진행 | §2.2, §4.4, §6.2, §11.1 |
| 가마솥 3D 모델 | **확보(v3.3), 임포트 대기** — `TagOfChaos/리소스/솥단지.glb`(Unity `Assets/` 바깥, 임포트 전) | §2.1 |
| 문(4개) 모델 | **신규**(선택) | §1 |
| ~~마녀 지팡이 + 1인칭 손 모델~~ | **폐기** | 구 §6.2 |
| ~~던질 수 있는 소품 모델~~ | **폐기** | §5 |

### 10.4 애니메이션 (FBX/컨트롤러) — 확보 현황 종합

| 항목 | 상태 | 비고 |
|---|---|---|
| `Cookie_Carrying.fbx` | **확보** — Humanoid+공용 아바타 전환 필요 | §11.2 |
| `Cookie_Hanging_Idle.fbx` | **확보** — 통합 보류(사용자 지정) | §11.3 |
| `PlayerAnimator.controller`(기존) 수정 — `Carry` 레이어(Avatar Mask) + `Held`/`Broken` 트리거 추가 | **수정 필요** | §4.1, §9 |
| 괴물 이동(Idle/Walk) 애니메이션 세트 | **확보** — `Monster_Rigged_Idle.fbx`/`Monster_Rigged_Walk.fbx`, `MonsterAnimator.controller`에 `Idle`/`Walk` 트리거로 이미 배선 확인. 사용자가 v3.4에서 재차 확인 | §12(해소) |
| `TentacleDash` 돌진 애니메이션 | **확보(v3.3)** — `Monster_Rigged_TentacleDash.fbx`, 컨트롤러에 `TentacleDash` 트리거로 배선 확인 | §4.3 |
| `GrabKill` 처형 애니메이션 | **확보(v3.3)** — `Monster_Rigged_GrabKill.fbx`, 컨트롤러에는 아직 `GrapKill`(오타, b 누락) 트리거로 배선돼 있으나 `GrabKill`로 정정하기로 확정(v3.5, §11.1) — 촉수 사용으로 확정(v3.4, §4.4) | §4.4 |
| ~~괴물 촉수 또는 손 타격(스윙) 모션(§4.2 수동 타격용, `GrabKill`과 별개)~~ | **불필요 확정(v3.4)** — §4.4가 (A)로 확정되어 수동 1차 타격 자체가 없어짐 | §4.2, §4.4 |
| 괴물 파괴(shatter) 연출 — 애니메이션 필요 여부 | **해소(v3.5)** — `GrabKill` 애니메이션이 "촉수에 잡혀 파괴되는" 연출 자체를 담당하므로, `PlayerCrackDisplay.PlayBreakEffect()`(렌더러 끄고 VFX 재생)는 `GrabKill` 애니메이션 재생이 끝나는 시점에 맞춰 호출되는 보조 연출로 확정됐다 — 쿨다운 지속시간이 "애니메이션 지속 시간"으로 확정된 것과 동일한 시점(§4.4) | §4.2, §4.4 |
| `Cookie_StandUp.fbx` — 괴물 리빌용 재사용 여부 | **재사용 불투명** — 괴물이 촉수 2개를 포함한 6지 구조로 확인돼, 표준 Humanoid 리타겟 전제가 성립하지 않는다. Generic 리그 전환 또는 다리만 신규 제작 중 택일 필요(§11.1) | §11.1 |

### 10.5 셰이더 / 머티리얼

| 항목 | 상태 | 근거 |
|---|---|---|
| `EraseStampMaterial.mat` | **신규** — 기존 `brushStampMaterial` 구조 복제 후 출력 알파 고정 0 | §3.5 |
| `FillAllMaterial.mat` | **신규** — UV 전체를 단색으로 덮는 전용 블릿 | §3.6 |
| `ColorReplaceMaterial.mat`(기존) | 기존재, 그대로 재사용(Reset 용도) | §3.4 |
| ~~`ColorTag/PlayerPaintedSkin` 셰이더(기존) 수정 — `_CrackAmount`(또는 `_CrackTex`) 프로퍼티 추가~~ | **불필요 확정(v3.6)** — 균열 완전 폐기로 "균열 오버레이"용 프로퍼티 자체가 쓰일 일이 없다. `PlayerPaintedSkin` 셰이더는 수정 없이 기존 그대로 사용 | §4.2, §4.4 |
| ~~마법 VFX용 머티리얼~~ | **폐기** | 구 §6.2 |

### 10.6 파티클 / VFX

| 항목 | 상태 | 근거 |
|---|---|---|
| 가마솥 보글보글(대기 연출) | **신규** | §2.2 |
| 가마솥 짜잔(리빌 연출) | **신규** | §2.2 |
| ~~타격 임팩트(1회 피격용)~~ | **불필요 확정(v3.6)** — 균열(1회 피격) 완전 폐기 | §4.2, §4.4 |
| **파괴(shatter) 파편 VFX** | **신규(사실상 필수)** — 사용자가 명시적으로 요구한 핵심 연출("쿠키가 부숴지는 연출") | §4.2 |
| 그랩/캐리 관련 VFX | 불필요(문서상 요구 없음) | — |

### 10.7 오디오

| 항목 | 상태 | 근거 |
|---|---|---|
| ~~타격 SFX(1회 피격)~~ | **불필요 확정(v3.6)** — 균열(1회 피격) 완전 폐기 | §4.2, §4.4 |
| 파괴 SFX(2회 피격) | **신규**(사실상 필수 — 파괴 VFX와 짝) | §4.2 |
| 가마솥 보글보글 SFX(선택) | **신규**(선택) | §2.2 |
| ~~던지기 착지 소음 SFX~~ | **폐기** | §5 |
| ~~마법 캐스팅 SFX~~ | **폐기** | 구 §6.2 |

### 10.8 UI 아트(스프라이트/아이콘/일러스트)

| 항목 | 상태 | 근거 |
|---|---|---|
| 결과 화면 — 괴물 승/쿠키 승 배너 아트 2종 | **신규**, `Result.png` 참고 | §8.2 |
| 결과 화면 — 괴물 단독/쿠키 4인 축하 일러스트 2종 | **신규**, `Result.png` 참고(괴물 모델 확보 후 제작) | §8.2 |
| 결과 화면 — 생존/부숴짐 쿠키 아이콘, 왕관 아이콘 | **신규**, `Result.png` 참고. 균열(1회 피격) 전용 아이콘은 **불필요 확정(v3.6)** — 생존/파괴 2종 상태만 실제로 존재 | §8.2, §4.4 |
| 색 슬롯 UI — 4칸 배경, Reset/지우개 버튼 아이콘 | **신규** | §3.4 |
| 괴물 이탈 경고 배너 배경/아이콘 | **신규** | §7.1 |

### 10.9 인프라 설정(레이어/태그)

`ProjectSettings/TagManager.asset` 기준, 현재 커스텀 레이어는 `PlayerCapsule`(8번) 하나뿐이고
`Cookie`/`Monster`처럼 그랩 판정에 쓸 전용 레이어가 없다. `PlayerGrabController.playerLayer`
(§4.1)가 참조하는 `LayerMask`는 이 레이어가 실제로 만들어져야 동작한다(v3.4 —
`MonsterStrikeAttack.cookieLayer`는 §4.4가 (A)로 확정되며 해당 클래스 자체가 폐기돼 더 이상
참조 대상이 아니다).

| 레이어 이름(안) | 용도 | 근거 |
|---|---|---|
| `Cookie` | 쿠키 캐릭터 판별(그랩 대상 필터 등) | §4.1 |
| `Monster` | 괴물 캐릭터 판별(그랩 대상에서 제외 등) | §4.1 |
| (기존 `PlayerCapsule`은 그대로 유지 — 붓칠 레이캐스트 제외용, 용도 다름) | — | (기존) |

---

## 11. 애니메이션 자산 실물 검증

### 11.1 `Cookie_StandUp.fbx` — **괴물 리빌용 재사용은 불투명, T-pose 확인 결과 반영**

v2 조사 결과(변경 없음): 클립 이름 `Cookie_StandUp`, 94프레임 1회성(`loop:0`), 아직 Humanoid로
전환되지 않은 `animationType: 2`(Generic) 상태로 확보돼 있다.

**T-pose 이미지 확인 결과 반영**: 괴물은 다리 2개는 쿠키와 비슷한 이족보행 비율이지만, **팔이
4개(관절형 손 2 + 구불거리는 촉수 2)** 인 6지 구조다. Unity의 표준 Humanoid 리타겟은 양팔·
양다리 하나씩만 매핑하므로, 촉수 2개는 애초에 Humanoid 본 슬롯에 들어갈 자리가 없다 — 다음
두 방향이 남는다:
- **Generic 리그로 전환**하고 촉수는 Extra Bones(추가 트랜스폼)로 별도 애니메이션.
- 다리만 쿠키 Humanoid 클립을 참고해 **새로 제작**하고, 팔/촉수는 처음부터 전용 애니메이터로
  분리 — `Cookie_StandUp.fbx` 자체는 리빌 연출용으로 재사용하지 않음.

어느 쪽이든 실제 3D 모델 파일(메시+리그, §14.1 최우선 미확보)이 나와야 최종 결정이 가능하다.

> 📌 **(v3.3 추가) 실제 확보 결과 — 위 두 방향 중 Generic 리그 전환이 이미 채택돼 있음을 확인**:
> `Assets/Animation/Monster/` 아래 `Monster_Rigged.fbx`(T-pose 기본 리그)와
> `Monster_Rigged_Idle.fbx`/`_Walk.fbx`/`_TentacleDash.fbx`/`_GrabKill.fbx` 4개 애니메이션
> 클립을 직접 확인했다. `Monster_Rigged.fbx.meta`에 `animationType: 2`(Generic)로 명시돼
> 있어, 위에서 논의한 "Generic 리그 전환" 방향이 이미 실제로 적용된 상태다 — `Cookie_StandUp.fbx`
> 재사용 여부 논의 자체는 무효화된다(괴물은 처음부터 독립된 Generic 리그로 제작됨).
>
> `Assets/Animation/MonsterAnimator.controller`(신규, `PlayerAnimator.controller`와 완전히
> 분리된 별도 파일)를 직접 열어 트리거 파라미터를 확인한 결과 `Idle`/`Walk`/`TentacleDash`
> 3개는 의도한 이름 그대로지만, 네 번째는 **`GrapKill`(오타, "b" 누락)**로 등록돼 있다.
> `PlayerMoveState`가 `Animator.SetTrigger(newState.ToString())`으로 enum 이름과 Animator
> 파라미터 이름을 직접 매칭시키는 이 프로젝트의 계약(research.md §2.4)을 그대로 따라야 하는데,
> **오타를 `GrabKill`로 정정하기로 확정됐다(v3.5, 사용자 확인)** — §4.3/§9의 `MonsterMoveState`
> enum은 이미 정정된 이름(`GrabKill`)으로 갱신해뒀다.
>
> ⚠️ **선행 작업 필요**: 문서상 enum은 고쳤지만, `MonsterAnimator.controller`의 실제 트리거
> 파라미터 이름은 여전히 `GrapKill`이다(Unity 에디터에서 직접 리네임해야 하는 에셋 작업,
> 코드 수정만으로는 안 됨). 구현 착수 시 Animator Controller 파라미터를 `GrabKill`로 먼저(또는
> 코드 배포와 동시에) 바꾸지 않으면 `SetTrigger("GrabKill")`이 조용히 무시되어 트리거가
> 걸리지 않는다 — §14.3에 구현 작업 항목으로 등록.

### 11.2 `Cookie_Carrying.fbx` + `Cookie_Walking.fbx` 조합 — 변경 없음(§4.1 전용, 괴물과 무관)

`Cookie_Carrying.fbx`를 Humanoid+`Cookie_Idle` 공용 아바타로 임포트하고, Animator에 상반신
전용 Avatar Mask `Carry` 레이어를 추가해 Base Layer(다리)와 독립적으로 on/off한다.

### 11.3 `Cookie_Hanging_Idle.fbx` — 보류 (변경 없음)

사용자가 "보류"로 명시했으므로 §4.1의 "통합 후보"로만 언급, 실제 Animator 배선은 하지 않는다.

### 11.4 "흐느적거리며 자연스럽게 잡는" 도입 모션 — 변경 없음(§4.1 전용)

하드컷(`Animator.Play()`) 방식 권장은 쿠키↔쿠키 그랩에 대한 결론이며 괴물의 `GrabKill` 처형
모션과는 별개다 — §4.4가 (A)로 확정되며 별도의 "타격 스윙 모션" 자체가 없어졌으므로(v3.4), 이
검토는 `GrabKill` 애니메이션 하나의 하드컷/크로스페이드 여부로 범위가 좁혀진다.

---

## 12. 열린 질문 (v3.5 전면 갱신)

**해결됨**:
- 괴물 카메라 시점 → **1인칭으로 확정**(사용자 확인, §6.2).
- §6.2 마법 자원/쿨다운, 던지기 관련 질문 전체 → 마법·던지기 둘 다 폐기되어 무효화.
- 괴물이 쿠키와 같은 Humanoid 아바타를 쓸 수 있는지 → **완전히 해소**. Generic 리그로 이미
  확보·임포트돼 있음을 파일로 직접 확인(§11.1).
- 근접 "포획" 판정 방식(구 질문) → **포획 개념 자체가 폐기**되어 무효화.
- **`GrabKill`이 §4.2를 완전히 대체하는지((A)/(B))** → **(A)로 확정**(사용자 확인, v3.4,
  §4.4). `MonsterStrikeAttack.cs`·균열(hitCount==1) 상태·손/촉수 타격 스윙 애니메이션이
  전부 불필요해짐.
- **타격 판정을 언제 시도하는지** → **"전방 3m 이내로 Player가 들어오면 자동 발동"으로
  확정**(사용자 확인, v3.4).
- **촉수/손 중 무엇으로 때리는지** → **촉수만 사용으로 확정**(사용자 확인, v3.4) — (A) 확정과
  함께 "손"이라는 선택지 자체가 없어짐.
- **1인칭 시점에서 뒤쪽 촉수 타격을 어떻게 조준/인지하는지** → **별도 조준 UI 불필요로
  확정**(사용자 확인, v3.4) — 3m 이내 자동 발동이라 판정 자체에 조준이 필요 없다.
- 손과 촉수의 판정 범위·쿨다운 차등 여부 → **무효화**(촉수만 사용하기로 확정돼 "차등"을 논할
  대상 자체가 없음, v3.4).
- **`GrabKill` 발동 중 회피/이동으로 벗어날 수 있는지** → **벗어날 수 없다(확정 처형)로
  확정**(사용자 확인, v3.4).
- **`TentacleDash` 입력 키** → **좌Shift로 확정**(사용자 확인, v3.4). 임시값이며, 추후 키 설정
  UI가 구현되면 그쪽으로 이관될 예정.
- **괴물 이동 방식(물리 기반 vs 단순 Transform)** → **Rigidbody 물리 기반으로 확정**(사용자
  확인, v3.4) — §6.2/§4.3의 코드 골격을 `HideOrSeekPlayer`와 동일한 패턴으로 재작성했다.
- **가마솥 무입장 타임아웃(`monsterSelectTimeout`)** → **30초로 확정**(사용자 확인, v3.4 —
  기존 가정값이 그대로 최종값이 됨).
- **괴물 이동(Idle/Walk) 애니메이션 세트 공백** → **확보 확인**(§10.4, §11.1 — 사용자가 v3.4에서
  재차 확인).
- **`GrabKill` 재사용 대기시간(쿨다운 지속시간)** → **"애니메이션이 지속되는 시간"으로
  확정**(사용자 확인, v3.5, §4.4) — `MonsterGrabKillTrigger.ResetTrigger()`는 `GrabKill`
  애니메이션 재생이 끝나는 시점에 호출한다.
- **`MonsterAnimator.controller`의 `GrapKill` 오타를 `GrabKill`로 정정할지** → **정정하기로
  확정**(사용자 확인, v3.5, §11.1). 코드(`MonsterMoveState` enum)는 이미 `GrabKill`로
  갱신했으나, Animator Controller의 실제 파라미터 이름은 아직 `GrapKill`인 채로 남아있어
  Unity 에디터에서 리네임하는 작업이 구현 착수 시 선행돼야 한다(§14.3).
- **균열(1회 피격) 상태를 완전히 삭제할지** → **완전히 삭제하는 것으로 최종 확정**(사용자
  확인, v3.6 — "`GrabKill`로만 할 거고, 타격은 하지 않을 것"). `hitCount`는 0(정상)과
  2(파괴) 두 값만 존재하며, "구출"/균열 회복 개념 자체가 이 게임에 없다(§4.2, §4.4, §6.3).
  v3.4에서 지적됐던 균열 이동 제약·회복 여부 답변의 모순은 이 확정으로 해소됐다 — 그 두
  답변은 이제 적용 대상이 없는 무효 답변으로 남는다.

**남아있는 것**:

1. 색칠 판정 방식을 §3.7의 대안 A(정적 프록시 콜라이더)로 실제로 바꿀지 — **"일단 보류"로
   확정**(사용자 확인, v3.4). 바꾸기로 결정될 경우 프록시 메시를 누가 준비하는지는 여전히
   정해야 한다(§14.1).
2. 괴물 눈(카메라 부착 지점, `eyeSocket`) 위치 — 사용자 확인: "나중에 눈을 부착시킬 것이다,
   이건 일단 임시로 설정 부탁한다" → **임시 배치로 확정**. 정확한 위치는 3D 모델 제작 단계에서
   추후 결정하고, 그 전까지는 머리 중앙 부근에 임시 `eyeSocket` Transform을 붙여 진행한다
   (§6.2, §10.3).
3. §3.2 `MinStrokesToRegister`(임계 스탬프 수) 밸런스 값 — 실제 플레이테스트로 조정 필요.
4. §7.1 5초 경고 타이밍 관련 원문 모호성 — 구현 시 재확인 권장.
5. §10.9 신규 레이어(`Cookie`/`Monster`) 이름·번호 확정 및 프리팹에 실제로 배정하는 작업.
6. §3.6 강제 도포 색 배정에 쓰는 `finalizeStampMaterial`(기존 재사용)과 `FillAllMaterial`
   (§10.5, 신규 예정) 중 어느 쪽을 실제로 쓸지.

---

## 13. 구현 순서 제안 (v3.4 갱신)

1. **§2(괴물 선정+타임아웃+연출) + §9 NetKeys/EventCodes 골격 + §7.2(방장 위임 확인)** — §7.2는
   Photon 기본 동작이라 "확인만" 하면 되므로 별도 구현 비용이 거의 없다.
2. **§3(자유 색칠, 임계 등록) 재작성** — 이번 개정과 무관, 우선순위 2위 유지.
3. **§6.1(안개) + §6.4(GameEndTime) + §4.4(GrabKill 자동 처형) + §6.2(1인칭 카메라·
   `MonsterController`, Rigidbody) + §8(승리 판정+결과 화면)** — 판정 트리거·촉수 사용·이동
   방식(Rigidbody)·쿨다운 지속시간·균열 완전 폐기 등 §12의 모든 질문이 확정됐으므로(v3.6)
   더 이상 막힌 변수 없이 바로 착수 가능한 상태다.
4. **§7.1(괴물 이탈 처리)** — 3번이 끝난 뒤 안정성 보강 차원에서 진행.
5. **§4.1(쿠키↔쿠키 그랩)** — 애니메이션 리소스는 이미 절반 이상 확보돼 있어 리스크가 낮다.
6. 애니메이션/파티클/모델/UI 아트 자산은 병렬 진행 — **단, 괴물 3D 모델(§14.1 최우선)이
   나오지 않으면 §2.2 리빌 연출·§4.2 타격 모션·§6.2 눈 위치·§11.1 재검토 전부 착수가 막힌다**는
   점이 계속되는 병목이다.

7. **(v3.3 추가) §1.5(스킨 A/B/C 선택)는 위 순서와 독립적으로 가장 먼저 착수 가능** — 필요한
   에셋(`Cookie_BaseSkin_A/B/C.mat`)이 이미 전부 확보돼 있고, `PlayerPaintCanvas.cs`를 전혀
   건드리지 않는 순수 추가 기능이라 다른 항목의 진행 상태와 무관하게 병렬로 진행할 수 있다.
8. **(v3.3 추가, v3.4 갱신) 괴물 3D 모델·애니메이션 4종 확보 + §4.4의 (A) 확정으로 3번
   항목(`GrabKill` 처형+1인칭 카메라+승리 판정) 착수 우선순위가 최상위로 올라간다** —
   `MonsterStrikeAttack.cs`는 만들지 않는 것으로 정리됐다(v3.4). §3.7(색칠 판정 방식)은
   "일단 보류"로 확정돼(v3.4) 2번(자유 색칠) 착수 시 기존 실시간 베이크 콜라이더 방식을
   그대로 유지하면 된다.

---

## 14. 사용자 제공 필요 항목 (핸드오프 체크리스트, v3.4 갱신)

§10(에셋 전체 목록)과 §12(열린 질문)에 흩어진 항목 중 **"실제로 사용자가 만들거나/구해서
프로젝트에 넣어야 하는 것"**과 **"예/아니오 답변 한 줄이면 되는 것"**만 골라 별도로 정리한
것이다. 나머지는 §14.3에 명시했듯 사용자가 따로 준비할 필요 없이 진행 가능하다.

### 14.1 에셋 제공 필요 (우선순위순)

| 우선순위 | 항목 | 현재 상태 / 필요 이유 | 근거 |
|---|---|---|---|
| ✅ 완료 | ~~괴물 T-pose 참고 이미지~~ | **확보 완료.** `Assets/Screenshots/괴물 T-pose.png` + `리소스/괴물 T-pose.png` — 삐에로 풍 몬스터, 손 2+촉수 2+다리 2의 6지 구조(§4.2) | §0, §4.2 |
| ✅ 완료(v3.3) | ~~괴물 3D 모델(메시/텍스처/리그)~~ | **확보·임포트 완료.** `Assets/Animation/Monster/Monster_Rigged.fbx`, Generic 리그(§11.1). 눈/카메라 부착 위치(`eyeSocket`)는 임시 배치로 확정(v3.4) — 정확한 위치는 추후 모델 제작 단계에서 결정 | §10.3, §11.1, §6.2 |
| ✅ 완료(v3.3) | ~~괴물 이동(Idle/Walk) 애니메이션 세트~~ | **확보 완료.** `Monster_Rigged_Idle.fbx`/`_Walk.fbx`, 컨트롤러 배선 확인 | §10.4 |
| ✅ 완료(v3.3) | ~~괴물 처형 애니메이션(`GrabKill`)~~ | **확보 완료.** `Monster_Rigged_GrabKill.fbx` — 촉수로 잡아 파괴하는 연출(§4.4) | §10.4, §4.4 |
| ✅ 완료(v3.3) | ~~신규 스킬 애니메이션(`TentacleDash`)~~ | **확보 완료.** `Monster_Rigged_TentacleDash.fbx` — 쿨타임 15초·사거리 20m 돌진(§4.3) | §10.4, §4.3 |
| ✅ 완료(v3.3) | ~~가마솥 3D 모델~~ | **확보(임포트 대기).** `TagOfChaos/리소스/솥단지.glb`, `Assets/`로 임포트하는 작업만 남음(§2.1, §14.3) | §10.3, §2.1 |
| 🔴 최우선(v3.3 신규) | §3.7 대안 A용 "페인트 전용 정적 프록시 메시"(캐릭터를 대략 감싸는 저폴리 쉘 또는 캡슐) | **미확보 — 색칠 방식을 Ray 발사형으로 바꾸기로 결정될 경우에만 필요**(§0-1) | §3.7, §10.5 |
| — (v3.4, 불필요 확정) | ~~괴물 촉수 또는 손 타격(스윙) 애니메이션(`GrabKill`과 별개, §4.2 수동 1차 타격용)~~ | §4.4가 (A)로 확정되며 완전히 불필요해짐 | §10.4, §4.2, §4.4 |
| 🟠 (신규, 사실상 필수) | 파괴(shatter) 파편 VFX + 파괴 SFX | **미확보.** 사용자가 명시적으로 요구한 핵심 연출("촉수에 잡혀 부숴지는 연출") — `GrabKill` 애니메이션 재생 종료 시점에 맞춰 재생(확정, v3.5) | §10.6, §10.7, §4.4 |
| 🟡 (선택) | 문 4개 모델 | 미확보, 동일 프리팹 4회 배치로 대체 가능 | §10.3, §1 |
| 🟢 | 가마솥 보글보글/짜잔 파티클 | 미확보 | §10.6, §2.2 |
| — (v3.6, 불필요 확정) | ~~타격 임팩트 이펙트 + 타격 SFX(1회 피격용)~~ | 균열(1회 피격) 완전 폐기로 불필요 | §10.6, §10.7, §4.4 |
| 🟢 | 결과 화면 아트(배너 2종, 일러스트 2종, 생존/부숴짐 아이콘, 왕관 아이콘) | 미확보 — 괴물 일러스트는 위 3D 모델/컨셉이 먼저 필요 | §10.8, §8.2 |
| 🟢 | 색 슬롯 UI 아이콘(Reset/지우개 버튼) | 미확보(이번 개정과 무관) | §10.8, §3.4 |
| — | ~~마녀 지팡이/손 모델, 던질 소품 모델, 마법 VFX, 마법·던지기 SFX~~ | **더 이상 필요 없음** | 구 §6.2, §5 |

### 14.2 답변만 필요한 것 (결정 사항) — v3.6: 이번 개정 대상 전항목 확정 완료

| 항목 | 확정값 / 상태 | 근거 |
|---|---|---|
| **`GrabKill`이 §4.2의 타격 2회(균열→파괴) 설계를 완전히 대체하는지** | **(A) 완전 대체로 확정** | §4.4, §12 |
| **타격 판정을 언제 시도하는지**(자동 근접/입력 키/애니메이션 이벤트) | **전방 3m 이내 자동 발동으로 확정** | §12 |
| **촉수냐 손이냐** (또는 둘 다 상황별로) | **촉수만 사용으로 확정** | §12 |
| ~~균열(1회 피격) 상태의 이동 제약 여부~~ | **무효 확정(v3.6)** — 균열 단계 자체가 완전히 폐기되어 질문 대상이 사라짐 | §4.4, §6.3, §12 |
| ~~균열이 회복되는지~~(시간/다른 쿠키 도움) | **무효 확정(v3.6)** — "구출" 개념 자체가 이 게임에 존재하지 않음 | §4.4, §6.3, §12 |
| **1인칭에서 뒤쪽 촉수 타격을 어떻게 조준/인지하는지** | **자동 발동이라 조준 자체가 불필요로 확정** | §12 |
| 손/촉수 판정 범위·쿨다운 차등 여부 | **무효화**(촉수만 사용) | §12 |
| 괴물 이동 방식(물리 기반 vs 단순 Transform) | **Rigidbody 물리 기반으로 확정** | §6.2, §12 |
| 가마솥 무입장 타임아웃 | **30초로 확정** | §2.1, §12 |
| 색 슬롯 등록 임계값(`MinStrokesToRegister`) | 미정(15스탬프 가정값 유지, 이번 개정과 무관) | §3.2, §12 |
| **색칠 판정을 §3.7 대안 A(정적 프록시 콜라이더)로 바꿀지** | **"일단 보류"로 확정** — 기존 실시간 베이크 콜라이더 방식 유지 | §3.7, §12 |
| `GrabKill` 트리거 콜라이더 크기 | 미정 | §4.4, §12 |
| **`GrabKill` 재사용 대기시간(쿨다운 지속시간)** | **"GrabKill 애니메이션 재생 시간"으로 확정**(v3.5) — `ResetTrigger()`를 애니메이션 종료 시점에 호출 | §4.4, §12 |
| **`GrabKill` 발동 중 회피/이탈 가능 여부** | **불가(확정 처형)로 확정** | §4.4, §12 |
| **`TentacleDash` 입력 키** | **좌Shift로 확정**(임시, 추후 키 설정 UI로 이관 예정) | §4.3, §12 |
| **`MonsterAnimator.controller`의 `GrapKill` 오타 정정 여부** | **정정하기로 확정**(v3.5) — 코드는 이미 `GrabKill`로 갱신, Animator Controller 파라미터 리네임은 §14.3의 구현 작업으로 남음 | §11.1, §12 |
| 괴물 눈(`eyeSocket`) 위치 | **임시 배치로 확정** — 정확한 위치는 3D 모델 제작 단계에서 추후 결정, 그 전까지는 머리 중앙 부근 임시값 사용 | §6.2, §10.3, §12 |

### 14.3 사용자가 안 줘도 되는 것

아래는 에셋이나 결정이 아니라 **구현 작업 자체**라 개발 쪽(Unity 에디터 조작 포함)에서 그대로
진행 가능하다:
- `Cookie_Carrying`/`Cookie_Hanging_Idle.fbx`의 Humanoid+공용 아바타 Import 설정 전환(§11.2,
  §4.1 전용, 괴물 교체와 무관)
- `Witch/*` → `Monster/*` 폴더·클래스 명칭 일괄 변경, `HitCount`/`Broken` 등 신규 네이밍
  적용 작업 자체(§9, §10.1)
- 신규 레이어(`Cookie`/`Monster`) 신설 및 프리팹 배정(§10.9, §12)
- §10.1에 정리된 스크립트/컴포넌트 구현 전체(단, 착수 시점은 §14.1의 최우선 에셋 확보 이후)
- (v3.3 신규) `TagOfChaos/리소스/솥단지.glb`를 `Assets/`로 임포트하는 작업 자체(§2.1) — 파일은
  이미 확보돼 있으므로 가져오는 작업만 필요
- (v3.3 신규) `Cookie_BaseSkin_A/B/C.mat`을 `PlayerSkinApplier.skins` 배열에 연결하는 작업(§1.5)
  — 세 머티리얼 모두 이미 존재하며 결정 사항이 아님
- (v3.5 신규) `MonsterAnimator.controller`의 트리거 파라미터 이름을 `GrapKill`→`GrabKill`로
  Unity 에디터에서 리네임하는 작업(§11.1, §12) — 정정 여부는 이미 확정됐으므로 결정 사항이
  아니라 순수 에셋 작업. **코드(`MonsterMoveState` enum)는 이미 `GrabKill`로 갱신해뒀으므로,
  이 리네임을 빠뜨리면 트리거가 걸리지 않는다는 점에 주의** — `Monster/` 스크립트 구현과 함께
  또는 그 전에 처리해야 함
