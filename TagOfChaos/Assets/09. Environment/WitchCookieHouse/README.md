# Witch Cookie House (마녀의 과자집)

TagOfChaos용 랜드마크 건물. 참고 이미지 `snackShop.png` 기반의 귀엽지만 불길한 진저브레드 하우스.

## 1. 크기 기준

| 항목 | 값 | 근거 |
|---|---|---|
| 단위 | 1 unit = 1 m (FBX Scale 1, Convert Units) | |
| 쿠키 캐릭터 | CapsuleCollider 높이 2.0 m, 폭 0.92 m | `HideOrSeekPlayer.prefab` |
| 게임 바닥 | 24 x 24 m (Ground 큐브, 윗면 y = 0) | `GameScene.unity` |
| 전체 바운딩 | 약 18.7 (X) x 21.0 (Y, 연기 포함) x 18.7 (Z) m | 요청: 약 20 x 20 x 20 |
| 벽 몸체 | 15 x 15 m, 벽 두께 0.5 m, 처마 높이 7.5 m | |
| 지붕 | 박공지붕, 용마루 윗면 15.85 m, 처마 돌출 1.3 m | 좌/우 문 위 처마 하단 6.2 m |
| 문 개구부 | 폭 1.5 m x 높이 3.0 m (아치형) | 쿠키 대비 약 1.5배 |
| 문짝 | 1.47 x 2.15 x 0.15 m (사각, 경첩 쪽 둥근 모서리) | 벽 두께 가운데, 틈 1 cm |
| 문 위 장식판 | 아치 윗부분 (높이 2.22~2.985 m) | 고정, 충돌체 있음 |
| 내부 | 14 x 14 m, 천장은 지붕 안쪽까지 트여 있음 | |

- 원점(피벗): 건물 바닥 중앙, 바닥 윗면 y = 0.03 (Ground와 z-fighting 방지). 그대로 Ground 위(y = 0)에 놓으면 된다.
- 방향: Unity **+Z = 정면(Front)**, -Z = 후면(Back), +X = 좌측(Left), -X = 우측(Right). 정면에서 집을 바라볼 때 기준 좌/우.

## 2. 파일

| 경로 | 내용 |
|---|---|
| `Witch_Cookie_House.fbx` | 모델 (애니메이션 미포함, 클립은 별도 .anim) |
| `Materials/*.mat` | Built-in Standard 머티리얼 22개 (FBX 슬롯에 이름으로 리맵됨) |
| `Textures/T_Cookie_BaseColor.png`, `T_Cookie_Normal.png` | 512px 쿠키 표면 텍스처 (타일링 가능) |
| `Animations/Door_{Side}_Open/Close/OpenOut/CloseOut.anim` | 문 클립 16개 (30fps, 18프레임 = 0.6초) |
| `Animations/Door_{Side}.controller` | 문별 Animator Controller (Bool `IsOpen`, `OpenOutward`) |
| `Animations/Chimney_Smoke_Loop.anim/.controller` | 굴뚝 연기 루프 (18프레임) |
| `Assets/04. Prefabs/Environment/Witch_Cookie_House.prefab` | 바로 배치 가능한 프리팹 (Animator, Rigidbody, 콜라이더 설정 완료) |
| `Assets/Editor/WitchCookieHouse/*.cs` | 임포트 규칙 + 프리팹/클립 빌더/검증 도구 |
| `리소스/WitchCookieHouse/Witch_Cookie_House.blend` | Blender 원본 (Unity 폴더 밖) |
| `리소스/WitchCookieHouse/Renders/` | 검수 렌더 (정면/후면/좌/우/탑/원근/내부/연기) |
| `리소스/WitchCookieHouse/BlenderScripts/` | 재내보내기 스크립트 (아래 5절) |

## 3. 오브젝트 계층

```
Witch_Cookie_House                  (루트, 원점 = 바닥 중앙)
├─ Structure
│  ├─ Floor, Foundation
│  ├─ Wall_Front / Wall_Back / Wall_Left / Wall_Right   (문·창문 구멍 포함)
│  ├─ Roof, Roof_Tiles, Roof_Icing
│  ├─ Chimney, Chimney_Icing
│  └─ Chimney_Smoke                  (Animator: Chimney_Smoke_Loop)
│     └─ Chimney_Smoke_Puff_0 ~ 5
├─ Doors
│  ├─ DoorFrame_Front / _Back / _Left / _Right          (정적, 초콜릿 문틀)
│  └─ Door_Front / _Back / _Left / _Right               (피벗 = 경첩, Animator + kinematic Rigidbody)
│     └─ COL_Door_{Side}                                (문짝 충돌체, 문과 함께 회전)
├─ Windows
│  ├─ Window_{Side}_L / _R / _Eye                       (쿠키 창틀, 괴물 눈 창문)
│  └─ WindowGlass_{Side}_L / _R / _Eye                  (유리, 독립 머티리얼)
├─ Decorations
│  ├─ DoorDeco_{Side}   (해골, 사탕 기둥 L/R, 마법석 L/R, 아이싱)
│  ├─ WallDeco_{Side}   (별 쿠키, 롤리팝, 젤리, 사탕 점, 룬, 아이싱 띠)  ← 4면 동일 구성
│  ├─ RoofDeco          (지붕 해골, 보석 3개, 사탕)
│  └─ CornerDeco        (모서리 사탕 기둥 4개)
├─ Interior
│  ├─ Interior_Bookshelf_0..6 (짝수), Interior_CandyShelf_1..7 (홀수)  ← 모서리에만 배치
│  ├─ Interior_FloorRune, Interior_CookieRug             (바닥 데칼, 두께 1 cm)
│  └─ Interior_HangingGem(_Chain)                        (용마루 아래 12 m 높이)
└─ Collision                          (COL_*, 렌더러 없음, convex MeshCollider)
   ├─ COL_Floor
   ├─ COL_Wall_{Side}_L / _R / _Top, COL_Gable_Front / _Back
   ├─ COL_DoorFrame_{Side}_L / _R, COL_DoorPillar_{Side}_L / _R
   ├─ COL_CornerPillar_0..3, COL_Roof_L / _R, COL_Chimney
```

- 문짝·문틀·벽·지붕·굴뚝·바닥은 모두 독립 메시. 문짝은 다른 메시와 합치지 않았다.
- 장식(해골, 보석, 사탕 기둥, 롤리팝, 젤리, 별 쿠키, 창틀)은 개별 오브젝트로 분리돼 있어 복제/재배치가 쉽다.
- 전체 약 10만 삼각형(렌더 메시), 렌더러 149개. 움직이지 않는 부분은 프리팹에서 Static으로 지정됨.

## 4. 문과 애니메이션

- 4개 문 모두 **안팎 양쪽으로 열림**. 경첩은 바깥에서 봤을 때 왼쪽, 벽 두께 가운데·문설주에서 8.5 cm 안쪽.
- Unity 기준 닫힘 `localEulerAngles = (0, 0, 0)`, 안쪽 열림 `y = -90`, 바깥쪽 열림 `y = +75` (4개 문 동일).
- 바깥쪽은 75°까지만 연다: 문짝 바깥 면의 소용돌이 장식(7.9 cm 돌출)이 76°부터 경첩 쪽 문틀에 닿는다.
- Blender에서 안쪽 0~90°, 바깥쪽 0~75° 구간을 1° 간격으로 검사: 문짝이 문틀·벽·장식·내부 소품과 겹치지 않음. 문 충돌체는 양쪽 90°까지 겹치지 않음.
- `InteractableDoor`: 파괴되지 않은 쿠키·괴물이 문 앞 2 m 안에서 상호작용 키(E)를 누르면 여닫는다. 누른 캐릭터의 반대쪽으로 열린다(밖 → 안쪽, 안 → 바깥쪽). 상태는 Room Prop `DoorStates`로 동기화된다(Plan.md/GameLobbyScene.md §14).
- 제어: 문마다 Animator가 따로 있으므로 개별 제어 가능.

```csharp
var animator = door.GetComponent<Animator>();
animator.SetBool("OpenOutward", false);  // 방향은 닫혀 있을 때 먼저 정한다
animator.SetBool("IsOpen", true);        // Door_{Side}_Open (OpenOutward=true면 Door_{Side}_OpenOut)
animator.SetBool("IsOpen", false);       // Door_{Side}_Close / Door_{Side}_CloseOut
```

  게임에서는 `InteractableDoor`가 이 값을 방장이 쓴 Room Prop에 맞춰 설정한다 — 직접 바꾸면 다른 클라이언트와 어긋난다.
- 굴뚝 연기: 연기 덩어리 6개가 18프레임마다 한 칸씩 위로 올라간다. 맨 아래 덩어리는 크기 0에서 커지고, 맨 위 덩어리는 크기 0으로 줄어들며 사라진다. 루프 이음새 오차는 0이다. 투명 재질 없이 크기로 사라짐을 표현했다.

## 5. 재질

| 머티리얼 | 용도 | 비고 |
|---|---|---|
| M_Cookie_Wall | 벽 | BaseColor + Normal 텍스처 |
| M_Gingerbread_Dark | 문짝, 창틀, 사탕 선반 | 텍스처 + 색 틴트 |
| M_Cookie_Light | 별 쿠키, 문 소용돌이, 러그 | |
| M_Chocolate_Dark / _Milk, M_Floor_Chocolate | 지붕, 문틀, 굴뚝, 바닥 | |
| M_Icing_Purple / _Pink | 아이싱 | |
| M_Candy_Red / _White / _Pink / _Teal, M_Jelly_Orange | 사탕 | |
| M_Skull_Bone / _Socket | 해골 | |
| M_Magic_Gem, M_Magic_EyeGlow, M_Magic_Rune, M_Magic_Smoke | 마법 효과 | Emission 켜짐, 발광 조정용 별도 재질 |
| M_Window_Glass_Orange / _Purple | 창문 유리 | Emission 켜짐, 불투명 |
| M_Book_Mix | 책 | |

- 모든 재질은 불투명(Opaque)이다. 불필요한 투명 재질은 쓰지 않았다.
- UV: 모든 렌더 메시는 겹치지 않는 UV0(Smart UV)를 갖고, 라이트맵용 UV2는 Unity가 생성한다(Generate Lightmap UVs 켜짐).

## 6. 다시 내보내기 / 재빌드

- Blender: `Witch_Cookie_House.blend`를 열고 `BlenderScripts/wch_lib.py`, `wch_export.py`를 exec한 뒤 `export_fbx()`를 실행한다.
  - 기본 FBX 익스포터를 그대로 쓰면 안 된다. Blender 5.2 FBX 익스포터에는 `bake_space_transform` 사용 시 계층이 깊은 오브젝트(예: `Doors/Door_Front`)에 X 90° 회전이 잘못 붙는 버그가 있다. `wch_export.py`는 내보내는 동안에만 이를 보정한다.
- Unity: `Tools/TagOfChaos/Build Witch Cookie House`는 클립, 컨트롤러, 프리팹을 다시 생성한다. `Tools/TagOfChaos/Verify Witch Cookie House`는 검증 결과를 Console에 출력한다.

## 7. 검증 결과 (Unity 6000.0.58f2, Editor.log)

```
Door_Front/Back/Left/Right  restRot=(0,0,0) openY=270(-90) hinge 고정, 안쪽으로 열림, 콜라이더 동반 회전, 18f  OK
Smoke  puffs=6 frames=18 loop=True seamError=0.0000 topEndScale=0.000  OK
renderers=149 badMaterialSlots=0 emptyMeshes=0 convexColliders=42 collisionWithRenderer=0  OK
VERIFY ALL OK
```
