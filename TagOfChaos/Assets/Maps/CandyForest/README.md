# CandyForest (사탕숲)

350 × 350 m 플레이 공간(지면 두께 3 m 이상, 윗면 높이 ≈ 0)의 환경 맵. 참고 이미지: `C:\Program Files\Blender Foundation\Map`. Blender 원본: `Assets/Maps/Source~/Maps/CandyForest.blend` (전체 작업 파일 `Source~/TagOfChaos_Maps.blend`, 재생성 스크립트 `Source~/Scripts/`).

**랜드마크**: 중앙 거대 막대사탕 나무 (`CAN_Landmark_LollipopTree`, 높이 약 77 m)

## FBX (Models/)

| 파일 | 내용 | 충돌체 |
|---|---|---|
| `CandyForest_Background.fbx` | 외곽 장식 지형 `*_Terrain_Decor` + 배경 나무/건물 | 없음 |
| `CandyForest_Decoration.fbx` | 장식 소품(길가 사탕, 꽃, 난간 등) | 없음 |
| `CandyForest_Effects.fbx` | 이펙트 배치 기준점(빈 오브젝트 `FX_*`) | 없음 |
| `CandyForest_GameplayProps.fbx` | 엄폐물·가구 등 플레이 공간 소품 | Generate Colliders (MeshCollider) |
| `CandyForest_Ground.fbx` | 걷는 지형 `*_Terrain_Walkable` + 외곽 투명 벽 `COL_Boundary_*` | Generate Colliders (MeshCollider) |
| `CandyForest_Lighting.fbx` | 발광 메시(창문·룬 띠·램프 구체 등) | 없음 |
| `CandyForest_MainStructures.fbx` | 건축물·대형 구조물·랜드마크 | Generate Colliders (MeshCollider) |
| `CandyForest_Water.fbx` | 물 표면(얕은 물, 시각용) | 없음 |

- 모든 FBX 원점은 맵 원점이다. 씬에 **위치 (0,0,0) / 회전 (0,0,0) / 스케일 1**로 넣으면 서로 맞물린다.
- 축: Blender (x, y, z) → Unity (−x, z, −y). 1 unit = 1 m. 축 변환이 이미 적용돼 오브젝트에 −90° 회전이 없다.
- 재질은 `.meta`에서 이름으로 연결된다. 기존 프로젝트 재질(`09. Environment/*/Materials`)을 재사용하고, 없는 것만 `Assets/Maps/Common/Materials`에 있다(Built-in Standard, 텍스처 없음, 색·광택·발광만).
- `COL_Boundary_*`는 `M_Invisible_Collider`(완전 투명)를 쓴다. 필요하면 MeshRenderer만 꺼도 된다.
- 움직이지 않는 오브젝트는 Static으로 지정하고, 재질의 GPU Instancing을 켜 두면 같은 에셋 인스턴스가 한 번에 그려진다.

## 오브젝트 수

| 카테고리 | 오브젝트 |
|---|---|
| Ground | 5 |
| Terrain | 3 |
| MainStructures | 193 |
| GameplayProps | 179 |
| Decoration | 1459 |
| Background | 185 |
| Lighting | 36 |
| Effects | 11 |

## 사용 에셋 (인스턴스 수, 개별 FBX: `Assets/Maps/Common/Models/<이름>.fbx`)

`Marshmallow_Stack` ×178, `CakeSlice` ×168, `Macaron_Rock` ×167, `CandyRock_Broken` ×167, `GumdropBush_A` ×156, `SugarDrift` ×155, `Lollipop_SmallPink` ×152, `CandyFlower_Patch` ×145, `CandyCane_Tall` ×139, `CandyStick_Bundle` ×139, `Lollipop_GiantPink` ×112, `BareTree_Pink_A` ×86, `BareTree_Pink_B` ×77, `CandyTree_Puff` ×43, `TwistedTree_C` ×20, `MagicCrystal_Cluster` ×20, `SmallMushroom_Cluster` ×18, `TwistedTree_B` ×15, `TwistedTree_A` ×14, `GumdropBush_B` ×13, `GiantMushroom_C` ×9, `GiantMushroom_B` ×7, `GiantMushroom_A` ×6, `CandyRock_Choco` ×3, `CandyCane_Arch` ×3, `CandyRock_Mint` ×2, `Landmark_LollipopTree` ×1

## 조명 (`CandyForest_Lighting.json`)

- Environment Lighting: Color = (1.0, 0.74, 0.85), 강도 1.0
- Fog: Exponential, 색 (0.98, 0.8, 0.88), 밀도 0.008
- Directional Light: 색 (1.0, 0.82, 0.9), 강도 1.0, 회전 X 50° / Y -35° (근사)
- Point Light 33개: `pos_unity`, 색, `range`, 강도, 그림자 여부. 발광 재질만으로 충분하면 생략해도 된다.

## MonsterPlayer 통과

괴물(루트 스케일 3.14: 팔 폭 6.3 m, 키 5.3 m)이 드나드는 문과 실내 통로는 폭 7 m 이상, 높이 7 m 이상이다. 문 안팎 7 m는 막힘 없이 비워 두었다(`Source~/Scripts/door_sweep.py`로 검사).

## 렌더 (`Assets/Maps/Source~/Renders/Blockout/`)

`CandyForest_TopCam.png`(탑 뷰), `CandyForest_GameCam_*.png`(게임 카메라 시점), `CandyForest_Mood.png`(조명), `CandyForest_Terrain_HeightMap.png` / `_Terrain_Relief_x3.png`(지형)
