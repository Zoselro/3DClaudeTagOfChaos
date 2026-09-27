# 계획: 몬스터 재리깅 2차 — `NewAnimation/Monster_Rigged` 세트로 `MonsterPlayer` 교체

> 상태: **✅ 구현·에디터 검증 완료, 2026-09-27** — 결정 §5.1, 진행 현황 §7, 검증 결과 §8. 남은 것은 빌드 2개로 원격 확인(V7)뿐.
>
> 사용자 요청: 새 괴물 애니메이션으로 교체. 기존 `NewAnimation` 폴더는 `Old2Animation`으로 이름을 바꿨고, 새 파일은 `NewAnimation`에 넣었다.
> 전달받은 작업 지시:
> 1. `Monster_Rigged.fbx` — Rig 탭 Generic, "Create From This Model"로 Avatar 생성
> 2. 애니메이션 4개 — "Copy From Other Avatar"로 1번 Avatar 지정
> 3. Idle·Walk는 Loop Time 켜기, TentacleDash·GrabKill은 끄기
> 4. 본 이름이 현재 프리팹 리그(`Hip_L`, `Forearm_Lower_R` 등)와 다르므로 `MonsterPlayer` 프리팹의 모델과 Avatar도 이 파일로 교체
>
> 이 문서는 위 4단계에 더해, 에디터(Unity MCP)와 FBX 파일을 직접 실측해 **지시에 없지만 반드시 함께 처리해야 하는 문제**(§1)를 정리했다.
> 이전 1차 재리깅(2026-09-25, kihong 리그) 기록은 맨 아래 **부록**에 그대로 보존했다.

---

## 0. 현재 상태 (실측)

### 0.1 폴더 이름 변경 결과

| 항목 | 상태 |
|---|---|
| `Animation/Monster/Old2Animation/` | 옛 세트 5개(`Monster_Rigged_kihong.fbx` + `Monster_Manual_{Idle,Walk,TentacleDash,GrabKill}.fbx`). **`.meta`가 함께 옮겨져 GUID 유지** — 프리팹·컨트롤러 참조가 끊기지 않았다. 폴더 `.meta`는 13:40에 새로 생성됨 |
| `Animation/Monster/NewAnimation/` | 새 세트 5개(`Monster_Rigged.fbx`, `Monster_{Idle,Walk,TentacleDash,GrabKill}.fbx`). Unity가 13:40에 **기본 설정으로 자동 임포트**: Generic, Avatar 없음(`NoAvatar`), 애니메이션 가져오기 켬, 클립 설정 없음 |
| `Animation/Monster/OldAnimation/` | 더 옛 세트 5개. 어디서도 참조하지 않음 |
| git | 탐색기에서 옮긴 것이라 지금은 "`NewAnimation/*` 삭제 + `Old2Animation/` 신규"로 보인다. 스테이징하면 이름 변경으로 인식된다 |

옛 세트를 참조하는 곳(GUID 검색): `Monster_Rigged_kihong.fbx` ← `04. Prefabs/Resources/MonsterPlayer.prefab`(메시·Avatar·머티리얼),
`Monster_Manual_*.fbx` 4개 ← `Animation/MonsterAnimator.controller`. **이 두 에셋만 바꾸면 교체가 끝난다.**

### 0.2 현재 `MonsterPlayer.prefab` (MCP 실측)

| 항목 | 값 |
|---|---|
| 루트 | 레이어 10(Monster), **스케일 3** |
| 자식 | `EyeSocket`(로컬 (0, 1.09, 0.03)), `Mesh_0`·`MonsterArmature`(로컬 위치 (0, 0.84, 0), 회전 X 270°, **스케일 100**) |
| Animator | Avatar `Monster_Rigged_kihongAvatar`, Controller `MonsterAnimator.controller`, Apply Root Motion 끔, Culling AlwaysAnimate |
| SkinnedMeshRenderer | 본 57개, rootBone `Root`, 머티리얼 `Material_0`(FBX 내장, Standard, `_MainTex`=`Image_0_BaseColor`, `_BumpMap`=`Image_2_Normal`) |
| CapsuleCollider | r 0.1 / h 0.35 / center (0, 0.18, 0) → 월드 r 0.3, h 1.05 |
| SphereCollider(트리거, 처형 범위) | r 0.5 / center (0, 0.15, 0.1) → 월드 r 1.5 |
| MonsterController | cameraTargetHeight 2.2, cameraDistance 12, speed 4, obstructionMask 1(Default), pv·eyeSocket·grabKillTrigger·animator 모두 연결 |
| 머리 높이 | `Head` 본 월드 y **3.27**, `EyeSocket` 월드 y 3.27 |

### 0.3 현재 `MonsterAnimator.controller`

| 상태 | 모션(현재) | 파라미터(Trigger) |
|---|---|---|
| Idle | `Old2Animation/Monster_Manual_Idle.fbx` : `Idle`(4.00초, 반복) | Idle |
| Walk | `…_Walk.fbx` : `Walk`(2.00초, 반복) | Walk |
| TentacleDash | `…_TentacleDash.fbx` : `TentacleDash`(2.04초) | TentacleDash |
| GrabKill | `…_GrabKill.fbx` : `GrabKill`(3.71초) | GrabKill |

AnyState → 각 상태(전환 0초, 자기 자신으로 전환 허용), Write Defaults 켬. `MonsterController.ChangeState`가 `MonsterMoveState` enum 이름을 트리거로 쓴다(`MonsterController.cs:265-273`).

### 0.4 새 세트 (`NewAnimation/`, FBX 파일 직접 파싱 + MCP 실측)

| 항목 | 값 |
|---|---|
| 노드 | 5개 파일 모두 **같은 40개**: `MonsterArmature`, `Mesh_0` + 본 38개 |
| 본 | `Root`, `Hips`, `Spine`, `Chest`, `Neck`, `Head`, `Horn`, `Thigh/Shin/Foot_{L,R}`, `UpperArm/Forearm/Hand_{Top,Low}_{L,R}`, `Tentacle_01~06_{L,R}`, **`Grab_Socket`**(Root 자식, 스케일 1에서 (0, 0.35, 0.75) — 몸 앞쪽) |
| 옛 리그와 비교 | 본 57개 → 38개. 이름 체계가 다르다(예: `Hip_L`·`Knee_L`·`Ankle_L` → `Thigh_L`·`Shin_L`·`Foot_L`, `Forearm_Lower_R` → `Forearm_Low_R`, 손가락·IK·Pole 본 없음, 촉수 4쌍 → 6쌍). **옛 클립은 새 리그에 쓸 수 없고 새 클립은 옛 리그에 쓸 수 없다** |
| 코드의 본 이름 의존 | **없음**(`EyeSocket`은 본이 아닌 별도 자식, 처형 범위는 루트의 SphereCollider). 프리팹 안에서만 SkinnedMeshRenderer가 본을 참조 |
| 단위·축 | FBX 7.4, UnitScaleFactor 100(cm)이지만 임포트 결과 자식 스케일 **1**(옛 kihong은 100). 자식 회전 X 270°로 옛 모델과 같음 |
| 크기(스케일 1, Idle 첫 프레임 포즈) | 폭 2.06 × **높이 1.78** × 깊이 1.10 m, **발 y = 0.00**(피벗이 바닥), `Head` 본 y 1.04 |
| 메시 | 정점 7,031 — **옛 kihong 메시와 정점 수·UV가 7,031개 모두 같은 인덱스로 일치**. 같은 메시를 다시 리깅한 것이다 |
| 머티리얼 | `Material.001`(Standard, **흰색, 텍스처 없음**). FBX는 `Image_0.jpg`, `Image_2.jpg`, `texture_0_metallic.png`, `texture_0_roughness.png`를 가리키지만 프로젝트에 없다 |
| 애니메이션 | 파일마다 테이크 1개, **클립 이름이 모두 `Scene`**, 커브 390개, **새 모델에서 해석 안 되는 커브 0개** |
| 클립 길이(30fps) | Idle **3.20초** / Walk **1.60초** / TentacleDash **1.63초** / GrabKill **2.63초** (옛: 4.00 / 2.00 / 2.04 / 3.71) |
| 애니메이션 파일 부수물 | 애니메이션 4개 파일에도 메시·머티리얼이 들어 있어 각각 임포트된다(쓰이지 않음) |

---

## 1. 지시 외에 함께 처리해야 하는 문제

| # | 심각도 | 문제 | 근거 | 처리 |
|---|---|---|---|---|
| P1 | **높음** | **클립 이름이 모두 `Scene`이라 괴물 코드가 클립 길이를 못 찾는다.** `MonsterController.FindClipLength`는 컨트롤러의 클립 중 이름에 `"GrabKill"`·`"TentacleDash"`가 들어간 것을 찾는다(`MonsterController.cs:34, 45, 106-121`). 못 찾으면 대체값을 쓴다: 처형 쿨다운 **2초**(실제 2.63초 — 처형 동작이 끝나기 전에 Idle로 끊기고 다음 처형이 가능해짐), 돌진 유지 **0.25초**(실제 1.63초 — Bug-fix-plan §34에서 고친 "돌진 동작이 끝까지 재생되지 않음"이 **다시 발생**) | 코드 + 클립 이름 실측 | 임포트 설정(Animation 탭)에서 클립 이름을 `Idle`/`Walk`/`TentacleDash`/`GrabKill`로 지정한다. **코드 변경 없음**(옛 세트도 이 방식이었다) |
| P2 | **높음** | **새 모델이 흰색으로 보인다.** FBX가 가리키는 텍스처 파일이 프로젝트에 없다 | 머티리얼 실측 | UV가 옛 메시와 완전히 같으므로 기존 텍스처(`Monster_Rigged_textures/Image_0_BaseColor.png`, `Image_2_Normal.png`)로 **외부 머티리얼 1개**를 만들고 FBX의 `Material.001`을 그 머티리얼로 리맵한다(지금 괴물과 같은 겉모습) |
| P3 | 중간 | **루트 스케일을 다시 정해야 한다.** 자식 스케일이 100 → 1로 바뀌고 모델 비율도 다르다 | §0.2, §0.4 | 결정 D1. 머리 높이(3.27 m)를 유지하려면 3.27 ÷ 1.04 ≈ **3.15**(추천). 지금 값 3을 그대로 두면 약 5% 작아진다 |
| P4 | 중간 | **애니메이션 길이가 짧아져 게임 수치가 바뀐다.** 처형 쿨다운 = GrabKill 클립 길이(3.71 → 2.63초, 처형 간격 1.1초 단축), 돌진 후 경직 = TentacleDash 클립 길이(2.04 → 1.63초) | §0.3, §0.4 | 결정 D3. 그대로 받아들이거나, 옛 길이를 유지하려면 상태 재생 속도를 낮춘다 |
| P5 | 중간 | `EyeSocket`(카메라 시선 높이)을 새 `Head` 본 위치로 다시 맞춰야 한다 | §0.2 | 1차 때와 같이 `Head` 본 월드 좌표 → 루트 로컬로 변환해 배치 |
| P6 | 낮음 | 루트 CapsuleCollider가 몸에 비해 매우 작다(월드 반경 0.3 m, 높이 1.05 m, 몸 폭은 약 6 m). **이번 교체로 생긴 문제가 아니라 원래 그렇다** | §0.2 | 결정 D2. 이번에는 로컬 값을 유지(스케일 3 → 3.15로 월드 크기 5% 증가)하는 것을 추천. 키우면 문·통로 통과, 처형 판정이 달라지므로 별도 작업 |
| P7 | 낮음 | 애니메이션 파일 4개도 메시·머티리얼을 임포트한다 | §0.4 | 4개 파일은 머티리얼 가져오기 끔(`Material Creation Mode: None`) — 흰 머티리얼 4개가 생기지 않게 |
| P8 | 낮음 | 반복 이음새 확인 필요 | — | Bug-fix-plan §34처럼 Idle·Walk의 첫/마지막 프레임 포즈를 비교해 다르면 Loop Pose를 켤지 판단 |
| P9 | 참고 | 새 본 `Grab_Socket`(몸 앞)이 생겼다. 지금 코드는 쓰지 않는다 | §0.4 | 이번 범위 밖. 나중에 처형 때 쿠키를 붙잡는 위치 등에 쓸 수 있다 |

---

## 2. 작업 순서

> 모든 작업은 Unity MCP로 수행하고, 단계마다 콘솔 에러·경고를 확인한다.

### K1. 모델 임포트 설정 — `NewAnimation/Monster_Rigged.fbx`
1. Rig: Animation Type **Generic**, Avatar Definition **Create From This Model**, Root node는 비워 둠(1차와 같음) → Apply.
2. Animation: **Import Animation 끔**(이 파일에는 테이크가 없다).
3. 결과 확인: `Monster_RiggedAvatar` 생성, `isValid = true`, `isHuman = false`.

### K2. 애니메이션 임포트 설정 — `NewAnimation/Monster_{Idle,Walk,TentacleDash,GrabKill}.fbx`
1. Rig: Generic, Avatar Definition **Copy From Other Avatar**, Source = K1의 `Monster_RiggedAvatar`.
2. Animation: 테이크 `Scene`을 클립 하나로 두고 **이름을 `Idle` / `Walk` / `TentacleDash` / `GrabKill`로 지정**(P1), 범위는 테이크 전체.
3. Loop Time: Idle·Walk **켬**, TentacleDash·GrabKill **끔**. Loop Pose는 P8 결과에 따라.
4. Materials: Material Creation Mode **None**(P7).
5. 결과 확인: 클립 이름·길이(3.20 / 1.60 / 1.63 / 2.63초)·반복 여부, 커브 해석 실패 0개, 클립이 비어 있지 않음.

### K3. 머티리얼 — P2
1. `Assets/Animation/Monster/Monster_Rigged_textures/M_Monster.mat` 생성(Standard). `_MainTex` = `Image_0_BaseColor.png`, `_BumpMap` = `Image_2_Normal.png`, 나머지 값은 현재 `Material_0`과 같게.
2. `Monster_Rigged.fbx` Materials 탭에서 `Material.001` → `M_Monster` 리맵(externalObjects) → Apply.
3. 결과 확인: 모델 프리뷰가 지금 괴물과 같은 색으로 보임(흰색 아님).

### K4. `MonsterAnimator.controller` — 모션만 교체
- Idle / Walk / TentacleDash / GrabKill 상태의 Motion을 K2 클립으로 바꾼다. 상태·파라미터·전환·Write Defaults는 **그대로**.
- 결과 확인: 컨트롤러의 `animationClips`에 이름에 `GrabKill`·`TentacleDash`가 들어간 클립이 각각 하나씩 있음(P1이 코드 쪽에서 해결됐는지).

### K5. `MonsterPlayer.prefab` 재구성 (에셋 경로·이름 유지 — 코드의 `"MonsterPlayer"` 그대로)
`PrefabUtility.LoadPrefabContents`로 씬을 거치지 않고 편집한다.
1. 옛 `Mesh_0`·`MonsterArmature` 자식 삭제.
2. `Monster_Rigged.fbx`를 인스턴스화 → **`UnpackPrefabInstance(Completely)`를 먼저**(1차 때 이 단계 없이 부모를 바꾸다 자식이 통째로 사라진 사고가 있었다, 부록 §10.5) → `Mesh_0`·`MonsterArmature`를 루트 직계 자식으로 옮김(로컬 위치 (0, 0, 0), 회전 X 270°, 스케일 1 — 임포트 값 그대로) → 남은 임시 오브젝트 삭제.
3. 새 자식 전체 레이어 0(루트만 10 유지 — 기존 규칙).
4. 루트 스케일 = D1 결정값(추천 3.15).
5. Animator: Avatar = `Monster_RiggedAvatar`, Controller 그대로, Root Motion 끔·Culling AlwaysAnimate 유지.
6. `EyeSocket`: 새 `Head` 본 월드 좌표를 루트 로컬로 변환해 배치(P5). `EyeSocket`의 로컬 스케일(0.17)은 의미 없는 값이라 1로 정리.
7. CapsuleCollider·SphereCollider·PhotonView·MonsterController·MonsterGrabKillTrigger·FallGuard: **로컬 값·연결 그대로**(D2가 ①일 때).
8. 저장 후 누락 스크립트·빈 참조 0개 확인.

### K6. 검증 — §4 표

### K7. 정리 (검증 후, D5)
- `Old2Animation/`(1차 세트), `OldAnimation/`(0차 세트), `Monster_Rigged_textures/`의 사용하지 않는 사본(`Image_0_BaseColor 1.png`, `Image_2_Normal 1.png`) — 즉시 삭제하지 않고 D5 결정에 따른다.

---

## 3. 코드 변경 범위

**예상 0건.** 트리거 이름(enum)·프리팹 이름·클립 이름 키워드가 모두 유지되기 때문이다.
단, K2에서 클립 이름을 바꾸지 않으면(P1) `MonsterController`가 대체값으로 동작하므로, K4 확인 항목에서 반드시 잡는다.

---

## 4. 검증 계획

| # | 항목 | 방법 | 통과 기준 |
|---|---|---|---|
| V1 | 임포트 | K1~K3 후 콘솔·임포터 값 확인 | 에러·경고 0, Avatar valid, 클립 4개 이름·길이·반복 여부 일치, 해석 실패 커브 0 |
| V2 | 코드 연동(P1) | Play 중 `MonsterController`가 찾은 길이 확인 | `Animation clip containing … not found` 경고 없음, 처형 쿨다운 2.63초, 돌진 유지 1.63초 |
| V3 | 스폰 | `PlayerTestScene`에서 `OfflineModeBootstrap` `autoCreateRoom`·`spawnAsMonster`를 **테스트 동안만** 켜고 Play(씬 저장 안 함) | `MonsterPlayer(Clone)` 스폰, 예외 0, 흰색이 아닌 텍스처 |
| V4 | 애니메이션 | Idle·Walk 네 번째 주기 이후에도 포즈가 계속 바뀌는지(Bug-fix-plan §34 Z3 방식), Shift 돌진 → 1.63초 유지 후 Idle, 처형 → 2.63초 후 Idle | 모두 새 클립으로 재생·전환 |
| V5 | 크기·카메라 | 쿠키와 나란히 놓고 크기 비교, 3인칭 카메라 시선 높이 | 발이 바닥에 닿음(뜨거나 파묻히지 않음), 카메라가 머리 높이를 따라감 |
| V6 | 처형·돌진 판정 | 쿠키를 앞에 두고 근접 처형, 돌진 경로 처형(§34 Z6 방식) | 기존과 같이 동작 |
| V7 | 원격 | (가능하면) 빌드 2개로 원격 괴물 보간·애니메이션 | 원격에서도 새 모델·동작 |

---

## 5. 결정이 필요한 사항

| # | 질문 | 선택지 | 추천 |
|---|---|---|---|
| D1 | 루트 스케일 | ① 머리 높이 유지 **≈ 3.15** ② 지금 값 3 유지(약 5% 작아짐) ③ 직접 지정 | ① |
| D2 | 루트 CapsuleCollider | ① 로컬 값 그대로(월드 크기 거의 같음) ② 몸에 맞게 키우기(별도 작업) | ① |
| D3 | 짧아진 애니메이션(처형 쿨다운 3.71 → 2.63초, 돌진 경직 2.04 → 1.63초) | ① 새 클립 길이 그대로 ② 옛 길이에 맞게 재생 속도를 낮춤 | ① (애니메이션을 만든 타이밍 그대로) |
| D4 | 새 머티리얼 위치·이름 | `Animation/Monster/Monster_Rigged_textures/M_Monster.mat` | 이대로 |
| D5 | 옛 세트 정리 | ① 검증·빌드 확인 후 `Old2Animation`·`OldAnimation` 삭제 ② 당분간 보관 | ② 후 ① |

---

### 5.1 결정 (2026-09-27, 사용자)

| # | 결정 |
|---|---|
| D1 | ✅ 머리 높이 유지(≈ 3.15, K5에서 실측값으로 확정) |
| D2 | ✅ 몸 충돌체 로컬 값 그대로 |
| D3 | ✅ 새 클립 길이 그대로 |
| D4 | ✅ `Animation/Monster/Monster_Rigged_textures/M_Monster.mat` |
| D5 | ✅ 검증이 끝날 때까지 옛 폴더 보관 → 검증 후 삭제 |
| 이름 | ✅ 새 파일 이름은 지금 그대로(`Monster_Rigged`, `Monster_{Idle,Walk,TentacleDash,GrabKill}`) — 다른 괴물 에셋과 접두어가 같고, 블렌더에서 다시 내보낼 때도 같은 이름이 나오므로. `OldAnimation/Monster_Rigged.fbx`와 겹치는 이름은 D5 정리로 사라진다 |

---

## 6. 되돌리기

프리팹·컨트롤러·`.meta`는 모두 git으로 관리된다. 옛 세트 폴더는 K7에서 삭제했지만 커밋 `0a52d28`에 들어 있다. 되돌리려면 옛 세트(당시 경로 `Assets/Animation/Monster/NewAnimation/Monster_Manual_*`, `Monster_Rigged_kihong.fbx`와 `.meta`)를 git에서 꺼내고, `MonsterPlayer.prefab`·`MonsterAnimator.controller`를 같은 커밋으로 되돌리면 된다(GUID가 `.meta`에 있어 참조가 그대로 이어진다).

---

## 7. 진행 현황

| 단계 | 내용 | 상태 |
|---|---|---|
| K1 | `Monster_Rigged.fbx`: Generic, Create From This Model, Import Animation 끔 → `Monster_RiggedAvatar`(valid, Generic) | ✅ 완료 |
| K2 | 애니메이션 4개: Copy From Other(`Monster_RiggedAvatar`), 클립 이름 `Idle`/`Walk`/`TentacleDash`/`GrabKill`, Loop Time Idle·Walk 켬, 머티리얼 가져오기 끔 → 길이 3.20/1.60/1.63/2.63초, 커브 390개, 비어 있지 않음 | ✅ 완료 |
| P8 | 반복 이음새: Idle·Walk 모두 첫 프레임과 마지막 프레임 포즈 오차 0(위치 0, 회전 0°) → **Loop Pose 켜지 않음** | ✅ 완료 |
| K3 | `M_Monster.mat` 생성(현재 `Material_0` 값 복사: Standard, 흰색, Smoothness 0.5, Metallic 0, `_NORMALMAP`, `_MainTex`=`Image_0_BaseColor.png`, `_BumpMap`=`Image_2_Normal.png`) → `Monster_Rigged.fbx`의 `Material.001` 리맵 → 모델 머티리얼 = `M_Monster` | ✅ 완료 |
| K4 | `MonsterAnimator.controller` 상태 4개의 모션을 새 클립으로 교체(상태·파라미터·전환 그대로). `animationClips` = GrabKill·Idle·TentacleDash·Walk → 코드의 키워드 검색(`GrabKill`, `TentacleDash`) 모두 일치(P1 해결). 옛 세트 참조 0 | ✅ 완료 |
| K5 | `MonsterPlayer.prefab`: 옛 `Mesh_0`·`MonsterArmature` 삭제 → 새 모델 인스턴스 **언팩 후** 루트 직계 자식으로(로컬 (0,0,0), X 270°, 스케일 1) → 자식 41개 레이어 0 → **루트 스케일 3.14**(옛 머리 높이 3.266 m ÷ 새 모델 머리 1.040 m, D1) → Avatar `Monster_RiggedAvatar` → `EyeSocket` 로컬 (0, 1.040, 0.000)·스케일 1(월드 y 3.266, 옛과 같음). SkinnedMeshRenderer 본 38개·rootBone `Root`·머티리얼 `M_Monster`. 콜라이더·PhotonView·MonsterController 값·연결 그대로(D2). 빈 참조·누락 스크립트 0, 옛 kihong 참조 0 | ✅ 완료 |
| K6 | 검증(§8) | ✅ 완료(V7 원격 확인만 빌드 대기) |
| K7 | 참조 0 재확인 후 `Old2Animation/`(1차 세트), `OldAnimation/`(0차 세트) 삭제(AssetDatabase). 둘 다 git에 커밋돼 있어 복구 가능(`OldAnimation` 10개, 1차 세트는 옛 `NewAnimation` 경로로 10개). 삭제 후 프리팹·컨트롤러 참조 정상 | ✅ 완료 |


## 8. 검증 결과 (2026-09-27, 에디터)

`PlayerTestScene`에서 `OfflineModeBootstrap`의 `autoCreateRoom`·`spawnAsMonster`를 **메모리에서만** 켜고 Play(씬 저장 안 함, 테스트 후 디스크 변경 없음 확인).

| # | 항목 | 결과 |
|---|---|---|
| V1 | 임포트 | ✅ Avatar valid(Generic), 클립 4개 이름·길이·반복 설정 계획대로, 커브 390개 해석 실패 0. 단계마다 콘솔 에러·경고 0(Unity Animator 창 내부 NRE `UnityEditor.Graphs.Edge.WakeUp` 1건만 반복 — Cauldron.md K10과 같은 무관한 오류) |
| V2 | 코드 연동(P1) | ✅ `MonsterController`가 찾은 길이: 처형 쿨다운 **2.633초**, 돌진 유지 **1.633초**. "clip not found" 경고 없음 |
| V3 | 스폰 | ✅ `MonsterPlayer(Clone)` 스폰, IsMine, Avatar valid, 머티리얼 `M_Monster`·텍스처 `Image_0_BaseColor`(흰색 아님), 예외 0 |
| V4 | 애니메이션 | ✅ Idle 5번째 주기(n=4.46→5.01)에도 촉수·머리 회전이 계속 변함. Walk 6번째 주기(n=6.55)에도 다리 회전이 계속 변함. 돌진: TentacleDash 상태로 들어가 남은 시간이 1.56초부터 줄어들고, 약 20 m 이동 후 Walk/Idle 복귀. 처형: 쿠키가 처형 범위에 들어오자 GrabKill → 쿠키 파괴(`[CookieLife] -> Broken`) → 약 2.6초 뒤 Idle, 쿨다운 해제 |
| V5 | 크기·카메라 | ✅ 메시 가장 낮은 점 y **0.000**(발이 바닥에 닿음), 머리·`EyeSocket` y **3.27**(옛 괴물과 같음), 카메라 시선 높이 3.27 |
| V6 | 처형·돌진 판정 | ✅ 근접 처형(위 V4) + 돌진 경로 처형: `Tentacle dash caught cookie (view 1004)` → 쿠키 앞에서 멈추고 GrabKill → Idle |
| V7 | 원격 | ⏳ 빌드 2개로 확인 필요 |
| 테스트 | EditMode | 11/12 통과. 실패 1건 `RoundKeys_MatchDeclaredLifetimes`(기대 10, 실제 11)는 **이번 작업과 무관**: 커밋 `0a52d28`에서 `NetKeys.DoorStates`(판 단위 Room 키)가 추가됐는데 테스트는 옛 개수 10을 고정해 두었다(`RuleTests.cs:106`). 괴물 동기화 테스트(`MonsterSync_RoundTripsState`)는 통과 |

**측정 중 관찰(버그 아님)**
- 첫 Walk 확인 때 Walk가 잠시 뒤 Idle로 돌아갔다. 같은 시간대에 괴물 위치가 스폰 지점에서 크게 옮겨져 있어, 에디터에 WASD 입력이 들어온 것으로 보인다. 다시 시험했을 때는 6주기 동안 Walk가 유지됐다.
- 두 번째 쿠키가 스폰되자마자 "파괴"로 처리됐다. 오프라인 방에서는 테스트로 만든 쿠키가 모두 같은 플레이어 소유라, 첫 처형으로 기록된 `HitCount=2`를 이어받았기 때문이다(실제 게임에서는 쿠키마다 소유자가 다름). HitCount를 지운 뒤 다시 시험해 통과했다.
- 돌진·처형 시간은 에디터 프레임이 느려 `Time.timeScale = 0.2`로 관찰한 뒤 1로 되돌렸다. Shift 입력 대신 Update가 키를 눌렀을 때 하는 동작(TryStartDash → 유지 시간 설정 → 상태 전환)을 그대로 호출했다(Bug-fix-plan §34.8과 같은 방법).

### 8.1 변경 파일
- 수정: `Assets/04. Prefabs/Resources/MonsterPlayer.prefab`, `Assets/Animation/MonsterAnimator.controller`
- 신규: `Assets/Animation/Monster/Monster_Rigged_textures/M_Monster.mat`, `NewAnimation/*.fbx.meta` 5개(임포트 설정)
- 삭제: `Assets/Animation/Monster/Old2Animation/`, `Assets/Animation/Monster/OldAnimation/`
- 코드(`.cs`) 변경 **0건**
- 이번 작업과 무관하게 작업 트리에 있는 변경: `GameLobbyScene.unity`(13:03 저장, 대기실 UI 스크롤바 값 미세 변화), 저장소 루트 `괴물FBX/`(사용자가 원본 파일 정리)

### 8.2 사용자 확인 필요
1. 빌드(또는 멀티)에서 괴물의 겉모습·크기, Idle·Walk 반복, 돌진·처형 동작이 새 애니메이션으로 보이는지(원격 화면 포함).
2. 짧아진 처형 쿨다운(2.63초)과 돌진 경직(1.63초)이 게임 감각상 괜찮은지.

---
---

# 부록: 1차 재리깅 기록 (2026-09-25, `Monster_Rigged_kihong` 세트) — 원문 보존

> 아래 원문의 `NewAnimation/` 경로는 당시 폴더 이름이다. 해당 파일들은 지금 `Old2Animation/`에 있다.

## 계획: 몬스터 재리깅 전환 — `Monster_Rigged_kihong` + `NewAnimation` 세트로 `MonsterPlayer` 교체

> 상태: **✅ 구현 완료 (2026-09-25, A안)**. Unity MCP(`execute_code`/`manage_editor`/
> `manage_components`)로 §1(A안)~§7(검증)까지 전부 수행했다. 실제 적용된 수치·결과는 **§10 구현
> 완료 보고**에 정리했다 — 계획 단계의 추정치(스케일 6, EyeSocket (0,0.2,0.05) 등)와 실제 적용값이
> 다른 항목이 있으니 최종 값은 반드시 §10을 기준으로 볼 것.
>
> 원래 아래는 착수 전 작성한 계획이었다. `Assets/02. Scripts/Monster/`의 실제 소스 코드와
> `Assets/04. Prefabs/Resources/MonsterPlayer.prefab`(현재 실사용 프리팹) YAML을 직접 읽고,
> Unity MCP로 새 리깅 에셋(`Assets/Animation/Monster/NewAnimation/`)의 임포트 설정을 확인해
> 작성한 계획이다. `CLAUDE.md`의 "계획부터 말하고 승인 받은 후 진행" 원칙에 따라 실제 작업 착수
> 전 사용자 확인이 필요한 지점을 §1에 명시했다. 관련 기존 발견: `research.md` §6.15(미완성
> `PlayerMonster.prefab`), §6.4(`obstructionMask` 버그), §6.13(`GrapKill` 오타).

---

### 0. 현재 자산 상태 (직접 확인한 사실)

#### 0.1 기존(현역) 구성 — `MonsterPlayer.prefab`

`Assets/04. Prefabs/Resources/MonsterPlayer.prefab`(코드가 `PhotonNetwork.Instantiate`로 실제
스폰하는 프리팹, `MonsterJoinController.cs:14`/`MonsterTestSpawner.cs:10`의
`MonsterPrefabName = "MonsterPlayer"`)의 루트 GameObject 구조를 YAML에서 직접 확인:

| 항목 | 현재 값 |
|---|---|
| 루트 이름/레이어 | `MonsterPlayer`, Layer 10(Monster) |
| 루트 `m_LocalScale` | `{6, 6, 6}` |
| 자식 계층 | `MonsterArmature`(스케일 100×, 커스텀 크리처 본 — `Spine_Root`/`Tentacle.0N.{L,R}`/`UpperArm`/`Forearm`/`Hand`/`Finger`/`FootIK` 등, Humanoid 아님) + `Mesh_0`(SkinnedMeshRenderer) + `EyeSocket`(로컬 `{0, 0.2, 0.05}`, 본이 아닌 순수 빈 자식 오브젝트) |
| `Animator` | Avatar = `Monster_Rigged.fbx` 내장 아바타, Controller = `MonsterAnimator.controller` |
| `Rigidbody` | mass 1, useGravity 1, 나머지 기본값 |
| `CapsuleCollider`(루트) | radius 0.1 / height 0.35 / center (0, 0.15, 0) / direction 1 — **루트 스케일 6배가 곱해져 실제 월드 단위로는 반경 0.6, 높이 2.1**(작아 보이지만 스케일 고려하면 정상 범위) |
| `PhotonView` | `ObservedComponents = [MonsterController]` |
| `MonsterController` | `pv`=자기 PhotonView, `eyeSocket`=`EyeSocket` 자식, `obstructionMask`=**Nothing(버그, `research.md` §6.4)**, `grabKillTrigger`=자기 `MonsterGrabKillTrigger`, `animator`=자기 Animator |
| `MonsterGrabKillTrigger` | `monsterPv`=자기 PhotonView, `monsterController`=자기 MonsterController |
| `SphereCollider`(루트) | trigger, GrabKill 판정용 |

`MonsterAnimator.controller` 파라미터 5개(전부 Trigger): `Idle`/`Walk`/`TentacleDash`/`GrapKill`/
`GrabKill`. 상태는 4개뿐(`Idle`/`Walk`/`TentacleDash`/`GrapKill`—오타 그대로), 각 상태의 클립은:

| 상태 | 클립 소스 (guid로 직접 대조) |
|---|---|
| `Idle` | `Assets/Animation/Monster/Monster_Rigged_Idle.fbx` |
| `Walk` | `Assets/Animation/Monster/Monster_Rigged_Walk.fbx` |
| `TentacleDash` | `Assets/Animation/Monster/Monster_Rigged_TentacleDash.fbx` |
| `GrapKill`(상태명 오타) | `Assets/Animation/Monster/Monster_Rigged_GrabKill.fbx` |

`MonsterController.cs`의 `ChangeState()`는 `animator.SetTrigger(newState.ToString())`으로
`MonsterMoveState` enum(`Idle/Walk/TentacleDash/GrabKill`) 값을 그대로 트리거 이름으로 쓴다 —
**파라미터 이름과 이 enum이 정확히 일치해야 하는 암묵 계약**(`research.md` §2.4와 동일 패턴).

#### 0.2 신규 리깅·애니메이션 세트 — `Assets/Animation/Monster/NewAnimation/`

커밋 `b134164`가 추가한 5개 `.fbx`:

| 파일 | 용도(추정, 이름 기준) | Rig 임포트 설정(직접 확인) |
|---|---|---|
| `Monster_Rigged_kihong.fbx` | 새 모델 본체(메시+본) | `animationType: 2`(Generic) — 맞음. **`avatarSetup: 0`(No Avatar) — 미설정**. `materialImportMode: 2`, `materialLocation: 1`(외부/레거시 참조 방식 — 머티리얼이 아직 정식 추출·연결 안 됐을 가능성) |
| `Monster_Manual_Idle.fbx` | Idle 애니메이션 | `animationType: 2`, **`avatarSetup: 0`** |
| `Monster_Manual_Walk.fbx` | Walk 애니메이션 | 〃 |
| `Monster_Manual_TentacleDash.fbx` | TentacleDash 애니메이션 | 〃 |
| `Monster_Manual_GrabKill.fbx` | GrabKill 애니메이션 | 〃 |

**핵심 발견**: 구 세트(`Monster_Rigged.fbx`)는 `avatarSetup: 1`("Create From This Model")로
설정돼 있어 자체 Avatar를 갖고 있었다. 반면 **새 세트 5개는 전부 `avatarSetup: 0`(No Avatar) —
즉 지금 상태로는 이 모델에 애니메이션을 재생시킬 Avatar 자체가 없다.** 실제로 기존
`PlayerMonster.prefab`(`research.md` §6.15, `b134164`가 함께 추가한 미완성 프리팹)의
`Animator.m_Controller`/`m_Avatar`가 모두 `{fileID: 0}`(미할당)인 것과 정확히 맞아떨어진다 —
**이 작업이 중단된 지점이 정확히 여기(Avatar 미생성)로 보인다.**

같은 폴더에 `Monster_Manual.blend`(원본 Blender 파일, 저장소 루트 `괴물FBX/`에 위치)가 함께 있어
5개 `.fbx`가 한 블렌더 파일에서 각 액션별로 분리 내보내기된 것으로 보인다 — 이는 "본체 1개
Avatar 생성 + 애니메이션 파일들은 그 Avatar를 Copy" 하는 표준 멀티-FBX 파이프라인과 정확히
일치하는 구조다.

#### 0.3 이미 존재하는 미완성 시도 — `PlayerMonster.prefab`

`Assets/04. Prefabs/Resources/PlayerMonster.prefab`(`research.md` §3/§6.15에서 이미 지적)이
`Monster_Rigged_kihong.fbx` 위에 `Animator`/`Rigidbody`/`CapsuleCollider`/`PhotonView`/
`MonsterController`/`MonsterGrabKillTrigger`/`SphereCollider`를 얹어놓은 상태다. 즉 **이번
작업과 정확히 같은 목적의 시도가 이미 절반쯤 진행돼 있다.** 다만:
- `Animator.m_Controller`/`m_Avatar` 미할당(§0.2의 원인 그대로).
- `MonsterController.pv`/`eyeSocket` 미배선(`{fileID: 0}`) → 지금 스폰하면 `Awake()`의
  `if (!pv.IsMine) return;`에서 즉시 NRE.
- `CapsuleCollider`(radius 0.1 / height 0.35)가 `MonsterPlayer.prefab`과 완전히 같은 수치로
  복사돼 있는데, **루트 `m_LocalScale`을 6으로 맞추는 수정이 없다**(prefab modification에
  `m_LocalPosition`/`m_LocalRotation`만 0으로 오버라이드하고 `m_LocalScale`은 원본 FBX의 기본값
  그대로) — 새 모델의 실제 크기를 모르는 채로 수치만 복붙된 상태로 보인다.
- 코드 어디에서도 `"PlayerMonster"` 문자열을 참조하지 않아(재확인) 지금은 스폰되지 않는다.

---

### 1. 결정 필요 사항 — 최종 프리팹을 어느 파일로 할 것인가 — ✅ A안으로 확정(사용자 확인)

**추천안(A)을 기본값으로 제안하되, 실제 착수 전 확인 요청.**

- **(A, 추천)** 기존 `MonsterPlayer.prefab`을 새 리깅·애니메이션으로 **내용만 교체**한다.
  프리팹 자산 경로/이름이 그대로 유지되므로 `MonsterJoinController.cs`/`MonsterTestSpawner.cs`의
  프리팹 이름 상수(`"MonsterPlayer"`)를 **전혀 건드릴 필요가 없다** — 씬 배선(`PhotonView`
  ObservedComponents 등)도 프리팹 내부 구조만 다시 짜면 되므로 리스크가 가장 낮다. `PlayerMonster
  .prefab`은 시행착오 산출물로 보고 작업 완료 후 삭제.
- **(B)** `PlayerMonster.prefab`을 완성해서 이것을 새 정본으로 삼는다. 이 경우
  `MonsterJoinController.MonsterPrefabName`/`MonsterTestSpawner.MonsterPrefabName` 두 상수를
  `"PlayerMonster"`로 바꾸는 코드 수정이 추가로 필요하고, 기존 `MonsterPlayer.prefab`을 삭제해야
  "이름이 뒤집힌 프리팹 2개" 상태가 해소된다.

두 안 모두 최종적으로 프리팹 내부 구조(§5)는 동일하게 다시 짜야 한다 — 차이는 **어느 파일 이름을
정본으로 남길지, 코드를 건드릴지 여부**뿐이다. 아래 §2~§9는 (A) 기준으로 서술하되, (B)를 선택하면
§7에서 코드 변경 2줄이 추가된다.

---

### 2. Unity 임포트 설정 수정 (Rig 탭) — 선행 작업, 반드시 먼저 — ✅ 완료

Avatar가 없는 한(§0.2) Animator/Controller 작업 자체가 불가능하므로 가장 먼저 처리한다.

1. `Monster_Rigged_kihong.fbx` 선택 → Inspector → **Rig** 탭 → Animation Type: Generic(이미
   맞음) → **Avatar Definition: "Create From This Model"**로 변경 → Apply. 이 시점에 Unity가
   본 계층으로부터 Avatar 서브에셋을 생성한다(구 `Monster_Rigged.fbx`가 `avatarSetup: 1`이었던
   것과 동일한 상태로 맞추는 것).
2. `Monster_Manual_Idle.fbx`/`Monster_Manual_Walk.fbx`/`Monster_Manual_TentacleDash.fbx`/
   `Monster_Manual_GrabKill.fbx` 4개 각각 선택 → Rig 탭 → **Avatar Definition: "Copy From Other
   Avatar"** → Source 필드에 1번에서 생성한 `Monster_Rigged_kihong` Avatar를 지정 → Apply.
   (본 이름·계층이 5개 파일 사이에서 정확히 일치해야 클립이 제대로 재생된다 — 한 블렌더 파일에서
   액션별로 내보낸 것이라면 보통 일치하지만, Apply 후 Animation 탭 프리뷰에서 실제로 모델이
   올바르게 움직이는지 눈으로 확인 필요.)
3. **Animation** 탭에서 각 클립의 Loop Time(Idle/Walk는 루프 필요, TentacleDash/GrabKill은
   1회 재생이 자연스러움 — 구 세트의 `Monster_Rigged_Walk.fbx`는 `loopTime: 0`으로 임포트돼
   있었는데 이것이 의도적이었는지 재확인 후 새 클립에도 동일 정책 적용) 확인·설정.
4. `read_console`로 임포트 에러/경고 0건 확인.

---

### 3. 머티리얼 확인 — ✅ 완료(문제 없음 확인)

`Monster_Rigged_kihong.fbx`의 `materialLocation: 1`(외부/레거시 참조)이 실제로 텍스처가 제대로
붙은 상태인지 Unity 에디터에서 모델을 직접 눈으로 확인 필요 — 핑크색(머티리얼 누락) 표시가
나오면 구 `Monster_Rigged_textures` 폴더처럼 머티리얼을 별도 폴더로 추출(`Extract Materials`)하고
텍스처를 재연결해야 한다. 이 항목은 코드와 무관한 순수 아트 파이프라인 작업.

---

### 4. `MonsterAnimator.controller` 갱신 — ✅ 완료(클립 4개 교체, 오타 정정은 미포함)

`MonsterMoveState` enum(`Idle/Walk/TentacleDash/GrabKill`)과 `MonsterController.ChangeState()`가
**트리거 파라미터 이름 문자열**로만 연결되므로, 파라미터 이름 자체(`Idle`/`Walk`/`TentacleDash`/
`GrabKill`)는 그대로 두고 **각 상태의 재생 클립만 새 파일로 교체**하면 C# 코드는 한 줄도 바꿀
필요가 없다.

1. **기존 컨트롤러를 그대로 재사용**할지, 새로 만들지 결정 — **재사용을 추천**한다(상태 4개·
   파라미터 5개 구조를 그대로 옮기고 클립만 갈아끼우는 것이 리스크가 가장 낮음).
2. `Idle` 상태의 Motion을 `Monster_Manual_Idle.fbx` 안의 클립으로 교체.
3. `Walk` 상태 → `Monster_Manual_Walk.fbx`.
4. `TentacleDash` 상태 → `Monster_Manual_TentacleDash.fbx`.
5. `GrapKill`(오타) 상태 → `Monster_Manual_GrabKill.fbx`.
6. **[선택, 이번 기회에 함께 처리 권장]** `research.md` §6.13이 지적한 상태명 오타
   (`GrapKill`→`GrabKill`)를 이번에 함께 정정할지 결정 필요 — 정정 시
   `MonsterController.cs:124`의 `state.IsName("GrapKill")` 문자열도 **반드시 동시에** `"GrabKill"`로
   수정해야 한다(코드-애니메이터 동기화, 둘 중 하나만 바꾸면 GrabKill 쿨다운 해제가 영구히
   깨짐). 이번 재작업 범위에 포함할지는 사용자 확인 필요 — 포함하지 않으면 상태명은 오타 그대로
   두고 클립만 교체.

---

### 5. 프리팹 재구성 (§1의 결정에 따라 `MonsterPlayer.prefab` 또는 `PlayerMonster.prefab`) — ✅ 완료

`Monster_Rigged_kihong.fbx`를 씬/프리팹에 배치한 뒤, §0.1에 정리한 기존 `MonsterPlayer.prefab`
구성을 그대로 재현한다:

1. **루트 GameObject**: 이름을 `MonsterPlayer`로, **Layer 10(Monster)** 지정.
2. **`m_LocalScale`**: 새 모델의 실제 크기를 Scene 뷰에서 확인해가며 조정 — 구 모델의 "6배"라는
   수치 자체는 구 모델 고유의 단위 스케일에 맞춘 값이라 새 모델에 그대로 쓸 근거가 없다.
   **에디터에서 시각적으로 맞추는 작업이 필요**(사람 크기 쿠키 캐릭터와 비교해 적절한 위협감을
   주는 크기로).
3. **`Animator`**: Avatar = §2-1에서 생성한 `Monster_Rigged_kihong` Avatar, Controller =
   §4에서 갱신한 `MonsterAnimator.controller`. `Apply Root Motion`은 기존과 동일하게 **꺼둔다**
   (`MonsterController`가 `Rigidbody` 이동을 직접 제어하므로 루트 모션과 충돌하면 안 됨).
4. **`Rigidbody`**: 기존과 동일 설정(mass 1, useGravity on) — `MonsterController.Start()`가
   `rb.useGravity`/`collisionDetectionMode`/`interpolation`/`constraints`를 코드로 다시
   설정하므로 인스펙터 기본값은 크게 중요하지 않음.
5. **`CapsuleCollider`(루트)**: radius/height/center를 **새 모델·새 스케일 기준으로 재설정**
   (구 수치 0.1/0.35/(0,0.15,0)를 그대로 복사하지 않는다 — §0.3에서 지적한 `PlayerMonster.prefab`의
   실수를 반복하지 않기 위함). Scene 뷰 Gizmo로 캡슐이 실제 모델 몸통을 덮는지 확인.
6. **`EyeSocket`**: 새 모델의 머리 위치에 맞는 빈 자식 GameObject를 새로 만들어 배치(본에
   종속시키지 않는 것은 구 프리팹과 동일한 패턴 — 머리 본을 못 찾아도 동작하도록). 1인칭 시점이
   자연스러운 높이/전방 오프셋으로 배치.
7. **`PhotonView`**: 추가 후 `ObservedComponents`에 `MonsterController`(아래 8번)를 등록.
8. **`MonsterController`**: `pv`=같은 오브젝트의 `PhotonView`, `eyeSocket`=6번의 `EyeSocket`,
   `grabKillTrigger`=9번의 `MonsterGrabKillTrigger`, `animator`=3번의 `Animator`,
   **`obstructionMask`=지형/벽 레이어로 실제 설정**(`research.md` §6.4의 기존 버그를 이번
   재구성 기회에 함께 수정 — 새로 만드는 김에 방치할 이유가 없음, 단 이것도 이번 범위에 포함할지
   사용자 확인 필요).
9. **`MonsterGrabKillTrigger`**: `monsterPv`=7번, `monsterController`=8번.
10. **`SphereCollider`(루트, trigger)**: GrabKill 판정 범위 — 새 모델의 팔/촉수 리치에 맞춰
    반경·위치 재조정.

---

### 6. 코드 변경 범위 — ✅ 확인됨(계획대로 `.cs` 변경 0건)

- **(A) 안 선택 시**: `Assets/02. Scripts/Monster/` 어떤 `.cs`도 수정 불필요(프리팹 이름이
  그대로 `"MonsterPlayer"`이므로 `MonsterJoinController.cs:14`/`MonsterTestSpawner.cs:10` 그대로
  유효). 단, §4-6에서 `GrapKill`→`GrabKill` 상태명 정정을 함께 하기로 하면
  `MonsterController.cs:124`의 문자열 리터럴 1곳만 수정.
- **(B) 안 선택 시**: 위 내용 + `MonsterJoinController.cs:14`, `MonsterTestSpawner.cs:10`의
  `MonsterPrefabName` 상수값을 `"PlayerMonster"`로 변경(2개 파일, 각 1줄).

---

### 7. 검증 계획 — ✅ 완료(결과는 §10)

1. `read_console`로 임포트/컴파일 에러 0건 확인(§2, §5 각 단계 직후).
2. **`PlayerTestScene`을 활용한 단독 검증** — 이미 이 목적을 위한 개발 도구가 갖춰져 있다
   (`Dev/OfflineModeBootstrap.cs` + `Dev/MonsterTestSpawner.cs`, `research.md` §2.8). `Offline
   ModeBootstrap.autoCreateRoom` + `SpawnAsMonster`를 켜고 Play Mode 진입 → `MonsterTestSpawner`가
   `MonsterSpawnPos`에서 새 프리팹을 스폰하는지 확인.
3. Play Mode에서 직접 확인할 목록:
   - Idle/Walk/TentacleDash/GrabKill 4개 애니메이션이 실제로 새 모델·새 클립으로 재생되는지
     (`MonsterController.ChangeState()`가 트리거를 걸 때마다 눈으로 전환 확인).
   - `MonsterTentacleDash`(좌Shift)가 실제로 동작하고, 새 캡슐 콜라이더 크기 기준으로 이동 거리가
     부자연스럽지 않은지.
   - `MonsterGrabKillTrigger`의 SphereCollider 범위가 실제 팔/촉수 리치와 맞는지(쿠키를 근접시켜
     확인).
   - `EyeSocket` 위치가 자연스러운 1인칭 시점을 주는지 — 단, **이 검증에는 `research.md` §6.1의
     별도 배선 공백(`MonsterFirstPersonCamera`가 어떤 Main Camera에도 부착돼 있지 않음)이 여전히
     막고 있다.** 이번 재리깅 작업과는 별개 이슈이므로, 카메라가 안 움직이는 것은 이번 작업의
     실패가 아니라 §6.1이 아직 해결되지 않았기 때문임을 구분해서 판단할 것 — 필요하면 이번
     작업에 카메라 배선도 함께 포함할지 사용자에게 재확인.
   - `PhotonView`/`MonsterController`의 `pv.IsMine` 분기가 정상 동작하는지(원격 클라이언트
     시점에서 캐릭터가 보간되며 따라오는지 — 2개 이상의 에디터/빌드 인스턴스 필요, 여의치 않으면
     최소한 로컬 스폰만이라도 NRE 없이 되는지 확인).

---

### 8. 정리 대상 (완료 후)

- (A) 선택 시: `Assets/04. Prefabs/Resources/PlayerMonster.prefab`(§0.3의 미완성 시도) 삭제.
  → **✅ 완료**(`git rm`으로 `.prefab`/`.meta` 제거, 아직 커밋은 안 함).
- 구 `Assets/Animation/Monster/Monster_Rigged*.fbx`(본체+4개 애니메이션) 및
  `Monster_Rigged_textures/` — 더는 어떤 프리팹도 참조하지 않게 되면 삭제 여부 결정(즉시 삭제
  대신 한동안 보관 후 정리도 가능, 사용자 판단 필요). → **보류(미삭제)** — 되돌릴 필요가 생길
  경우를 대비해 이번 작업 범위에서는 남겨뒀다. 삭제는 별도 확인 후 진행.
- `Assets/02. Scripts/QuarterViewActionGame.zip`(관련 없는 다른 프로젝트 잔재, git status에도
  삭제 대기 중으로 표시돼 있음) — 이번 작업과 무관하지만 발견된 김에 언급.

---

### 9. 범위 밖 (이번 재리깅 작업과 분리해서 다룰 것)

- `research.md` §6.1(괴물 1인칭 카메라 미배선) — §7에서 언급했듯 이번 작업의 `EyeSocket`은
  준비하지만, 카메라 스위칭 자체는 별도 작업(포함 여부 재확인 가능).
- `research.md` §6.2(`Cursor.lockState` 미설정), §6.5(재게임 시 프로퍼티 미정리) — 이번 재리깅과
  무관한 별도 버그.
- §4-6에서 언급한 `GrapKill`→`GrabKill` 오타 정정과 §5-8의 `obstructionMask` 수정은 "이번 김에
  포함할 수 있는 선택 항목"으로만 표시했다 — 기본 범위(모델·애니메이션 교체)에는 필수가 아니므로
  포함 여부를 명시적으로 확인 후 진행. **→ 이번 구현에서는 포함하지 않았다**(§10 참고, 여전히
  `research.md` §6.4/§6.13 미해결 상태로 남아 있음).

---

### 10. 구현 완료 보고 (2026-09-25)

Unity MCP `execute_code`로 Unity 6000.0.58f2 에디터(인스턴스 `TagOfChaos@ca592fd6`)에 직접 접속해
아래를 순서대로 수행했다. 모든 단계 사이사이 `read_console`로 에러/경고 0건을 확인했다.

#### 10.1 §2 — Rig 임포트 설정

- `Monster_Rigged_kihong.fbx`: `ModelImporter.animationType = Generic`,
  `avatarSetup = CreateFromThisModel` → 재임포트 → **`Monster_Rigged_kihongAvatar` 생성 확인**
  (`avatar.isValid = true`, `isHuman = false`, 예상대로 Generic).
  - 임포트된 계층에서 본 이름을 실제로 열거해본 결과, 구 리그와 달리 **`Head`/`Chest` 본이
    명시적으로 존재**했다(구 리그는 `Spine_Root`부터 시작, 별도 `Head` 본 없음) — 이 덕분에
    EyeSocket 위치를 "임의 추정" 대신 **실제 Head 본 좌표로 배치**할 수 있었다(§10.3).
  - 나머지 본 이름은 구 리그와 명명 규칙만 다름(`.` 대신 `_`, 예: `Hip.R`→`Hip_R`), 촉수·손가락
    체계는 동일 계열.
- `Monster_Manual_Idle/Walk/TentacleDash/GrabKill.fbx` 4개: `avatarSetup = CopyFromOther` +
  `sourceAvatar = Monster_Rigged_kihongAvatar` → 재임포트 → **4개 전부 비어있지 않은(`empty=false`)
  `Scene` 클립 추출 확인**. 클립 길이: Idle 4.00s, Walk 2.00s, TentacleDash 2.04s, GrabKill 3.71s.
  (참고: Unity `ModelImporterAvatarSetup` enum의 실제 멤버명은 `NoAvatar`/`CreateFromThisModel`/
  `CopyFromOther`이다 — 계획 초안의 "CopyFromOtherAvatar" 표기는 실제 API명과 다름, 실행 시
  발견해 정정.)

#### 10.2 §3 — 머티리얼

`Monster_Rigged_kihong`의 `SkinnedMeshRenderer.sharedMaterial`(`Material_0`, Standard 셰이더)이
`Image_0_BaseColor` 텍스처를 정상 참조 — 핑크(누락) 아님, 추가 조치 불필요로 확인.

#### 10.3 §5 — 프리팹 재구성 실제 값

`Assets/04. Prefabs/Resources/MonsterPlayer.prefab`을 `PrefabUtility.LoadPrefabContents`로 직접
편집(씬을 거치지 않는 헤드리스 방식):

1. 기존 `Mesh_0`/`MonsterArmature`(구 리그) 자식 삭제.
2. `Monster_Rigged_kihong.fbx`를 `PrefabUtility.InstantiatePrefab`으로 인스턴스화 →
   **`PrefabUtility.UnpackPrefabInstance(..., Completely, ...)`로 언팩**(이 단계 없이 바로
   `SetParent`를 시도했다가 "Setting the parent of a transform which resides in a Prefab instance
   is not possible" 경고와 함께 **자식이 통째로 유실되는 실패를 1회 겪었다** — 아래 §10.5 참고,
   즉시 감지하고 재작업으로 복구함) → `Mesh_0`/`MonsterArmature`를 프리팹 루트의 직계 자식으로
   재배치, 남은 `CINEMA_4D_Editor`(익스포트 잔재 카메라 오브젝트) 삭제.
3. **루트 스케일**: 계획 초안은 구 리그의 "6배"를 그대로 언급했으나, 실행 시 **양쪽 모델을 각각
   임시 인스턴스화해 `SkinnedMeshRenderer.bounds`를 직접 측정**해 비교했다:
   - 구 `MonsterPlayer.prefab`(스케일 6) 실측 월드 바운즈: `(13.04, 12.55, 9.14)`
   - 신 모델(스케일 1) 실측 월드 바운즈: `(2.39, 2.32, 1.92)`
   - 높이(Y) 기준 배율 `13.04/2.32 ≈ 5.42` 산출 → **루트 `localScale = (5.42, 5.42, 5.42)`로 확정**
     (6이 아님). 최종 재구성 후 실측 바운즈 `(12.93, 12.56, 10.40)` — 구 모델과 거의 동일한
     크기로 재현됨.
4. **자식 레이어**: `Mesh_0`/`MonsterArmature` 하위 59개 오브젝트 전부 레이어 0으로 정규화(루트만
   레이어 10 유지, 구 프리팹과 동일 컨벤션).
5. **`Animator`**: `avatar = Monster_Rigged_kihongAvatar`로 교체, `controller`는 **같은
   `MonsterAnimator.controller` 애셋을 그대로 재사용**(§4에서 모션만 갈아끼웠으므로 참조 변경
   불필요).
6. **`EyeSocket`**: 계획 초안의 "임의 배치, 추후 튜닝"을 실행 단계에서 개선 — 실제 `Head` 본의
   월드 좌표를 `root.transform.InverseTransformPoint()`로 루트 로컬 좌표로 변환하고 전방으로
   0.03 미세 오프셋을 더해 배치. 결과: **로컬 `(0, 0.25, 0.03)`**(계획 초안이 가정했던 구 프리팹
   값 `(0, 0.2, 0.05)`와 근접하되, 실측 기반이라 더 신뢰도 높음).
7. `CapsuleCollider`(반경 0.1/높이 0.35/중심 (0,0.15,0))·`SphereCollider`(반경 0.5/중심
   (0,0.15,0.1))·`PhotonView`·`MonsterController`·`MonsterGrabKillTrigger`는 **로컬 수치를 그대로
   유지**했다 — 스케일이 6→5.42로 10%만 달라져 월드 단위 차이가 미미하고(캡슐 월드 반경
   0.6→0.542, 높이 2.1→1.897), §9에서 범위 밖으로 명시한 `obstructionMask`(여전히 Nothing)도
   이번 작업에서 건드리지 않았다.

#### 10.4 §7 — 검증 결과 (`PlayerTestScene`, Play Mode)

`OfflineModeBootstrap.autoCreateRoom`/`spawnAsMonster`를 테스트 동안만 `true`로 설정(검증 후
`false`로 원복, 씬 파일은 저장하지 않아 디스크상 변경 없음) → Play 진입:

- `MonsterTestSpawner`가 `MonsterSpawnPos`에서 `MonsterPlayer(Clone)`을 정상 스폰. **콘솔 에러/
  예외 0건**(계획에서 우려했던 "`pv`/`eyeSocket` 미배선 시 `Awake()` NRE" 시나리오는 재구성된
  프리팹에서는 재현되지 않음 — 필드가 전부 정상 배선돼 있었기 때문).
- `PhotonView.IsMine = true`, `ViewID` 정상 발급.
- `Animator.avatar.isValid = true`, 기본 상태 `Idle`이 루프 재생 중(`normalizedTime`이 계속
  누적).
- `Animator.SetTrigger()`로 `Walk`/`TentacleDash`/`GrabKill`/`Idle` 4개 상태를 순서대로 강제
  진입시켜 전부 새 클립(`Scene`)으로 정상 전환되는 것을 `GetCurrentAnimatorStateInfo().IsName()`로
  확인. (`GrabKill` 트리거는 기존과 동일하게 `GrapKill`이라는 오타 상태로 들어간다 — §9에서
  범위 밖으로 명시한 그대로, 이번 작업이 새로 만든 문제가 아님.)
- `Rigidbody`(useGravity, non-kinematic), `SkinnedMeshRenderer`(bounds 정상) 모두 예상대로 동작.
- 테스트 종료 후 Play Mode 정지, `OfflineModeBootstrap` 플래그 원복.

#### 10.5 계획에 없었던 실행 중 발견 사항

- **`PrefabUtility.InstantiatePrefab`으로 모델(.fbx) 애셋을 인스턴스화하면 "모델 프리팹 인스턴스"로
  취급되어, 언팩(`UnpackPrefabInstance`) 없이는 그 자식(`Mesh_0`/`MonsterArmature`)을 다른
  부모로 옮길 수 없다** — 시도 시 Unity가 경고를 내며 조용히 무시하고, 그 상태에서 임시
  래퍼(`temp`) 오브젝트를 `DestroyImmediate`하면 **자식까지 함께 삭제되어 프리팹이 시각적으로
  텅 비어버리는 사고가 1회 발생**했다. `read_console`로 즉시 감지(`Setting the parent of a
  transform which resides in a Prefab instance is not possible` 경고 3건) → `UnpackPrefabInstance`
  단계를 추가해 재작업, 최종 결과물에는 영향 없음. 향후 유사 작업(모델 애셋을 프리팹 내부로
  합성) 시 이 언팩 단계를 먼저 넣을 것.
- `ModelImporterAvatarSetup` enum의 실제 값은 `CopyFromOther`이지 `CopyFromOtherAvatar`가 아니다
  (계획 문서 §2의 표기를 실제 API 확인 후 정정).
- `git status`상 `Assets/Fonts/NotoSansKR SDF.asset`이 일시적으로 수정된 것으로 표시됐으나
  `git diff`상 실제 내용 차이는 없었다(개행 방식 등 무해한 차이로 추정, 이번 작업이 만든 실질적
  변경 아님).

#### 10.6 최종 변경 파일 목록 (`git status`)

- `Assets/04. Prefabs/Resources/MonsterPlayer.prefab` — 수정(신규 리그로 내부 재구성).
- `Assets/04. Prefabs/Resources/PlayerMonster.prefab`(+`.meta`) — 삭제(§8, `git rm`).
- `Assets/Animation/Monster/NewAnimation/Monster_Rigged_kihong.fbx.meta`,
  `Monster_Manual_{Idle,Walk,TentacleDash,GrabKill}.fbx.meta` — 수정(Rig 임포트 설정).
- `Assets/Animation/MonsterAnimator.controller` — 수정(4개 상태 모션 교체).
- `Assets/02. Scripts/**/*.cs` — **변경 없음**(계획대로 0건).

#### 10.7 남은 후속 항목 (이번 범위 밖, `research.md`에 이미 기록됨)

- §6.1 괴물 1인칭 카메라 미배선, §6.2 `Cursor.lockState`, §6.4 `obstructionMask`, §6.5 재게임
  프로퍼티 정리, §6.13 `GrapKill` 오타 — 전부 이번 재리깅과 무관하게 그대로 남아 있음.
- 스케일(5.42)·EyeSocket 위치·콜라이더 크기는 "구 모델과 비슷한 크기로 재현"을 기준으로 정한
  값이라 실제 게임 내(카메라 시점, 그랩킬 판정 리치)에서 위화감이 있으면 인스펙터에서 미세 조정
  권장.
