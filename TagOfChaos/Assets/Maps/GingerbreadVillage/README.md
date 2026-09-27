# GingerbreadVillage (과자마을)

350 × 350 m 플레이 공간(지면 두께 3 m 이상, 윗면 높이 ≈ 0)의 환경 맵. 참고 이미지: `C:\Program Files\Blender Foundation\Map`. Blender 원본: `Assets/Maps/Source~/Maps/GingerbreadVillage.blend` (전체 작업 파일 `Source~/TagOfChaos_Maps.blend`, 재생성 스크립트 `Source~/Scripts/`).

**랜드마크**: 광장 과자 시계탑 (`GIN_Landmark_ClockTower`, 약 47 m)

## FBX (Models/)

| 파일 | 내용 | 충돌체 |
|---|---|---|
| `GingerbreadVillage_Background.fbx` | 외곽 장식 지형 `*_Terrain_Decor` + 배경 나무/건물 | 없음 |
| `GingerbreadVillage_Decoration.fbx` | 장식 소품(길가 사탕, 꽃, 난간 등) | 없음 |
| `GingerbreadVillage_Effects.fbx` | 이펙트 배치 기준점(빈 오브젝트 `FX_*`) | 없음 |
| `GingerbreadVillage_GameplayProps.fbx` | 엄폐물·가구 등 플레이 공간 소품 | Generate Colliders (MeshCollider) |
| `GingerbreadVillage_Ground.fbx` | 걷는 지형 `*_Terrain_Walkable` + 외곽 투명 벽 `COL_Boundary_*` | Generate Colliders (MeshCollider) |
| `GingerbreadVillage_Lighting.fbx` | 발광 메시(창문·룬 띠·램프 구체 등) | 없음 |
| `GingerbreadVillage_MainStructures.fbx` | 건축물·대형 구조물·랜드마크 | Generate Colliders (MeshCollider) |
| `GingerbreadVillage_Terrain.fbx` | 언덕 위 정원·작업대·경사로 등 지형성 구조물 | Generate Colliders (MeshCollider) |
| `GingerbreadVillage_Water.fbx` | 물 표면(얕은 물, 시각용) | 없음 |

- 모든 FBX 원점은 맵 원점이다. 씬에 **위치 (0,0,0) / 회전 (0,0,0) / 스케일 1**로 넣으면 서로 맞물린다.
- 축: Blender (x, y, z) → Unity (−x, z, −y). 1 unit = 1 m. 축 변환이 이미 적용돼 오브젝트에 −90° 회전이 없다.
- 재질은 `.meta`에서 이름으로 연결된다. 기존 프로젝트 재질(`09. Environment/*/Materials`)을 재사용하고, 없는 것만 `Assets/Maps/Common/Materials`에 있다(Built-in Standard, 텍스처 없음, 색·광택·발광만).
- `COL_Boundary_*`는 `M_Invisible_Collider`(완전 투명)를 쓴다. 필요하면 MeshRenderer만 꺼도 된다.
- 움직이지 않는 오브젝트는 Static으로 지정하고, 재질의 GPU Instancing을 켜 두면 같은 에셋 인스턴스가 한 번에 그려진다.

## 오브젝트 수

| 카테고리 | 오브젝트 |
|---|---|
| Ground | 5 |
| Terrain | 22 |
| MainStructures | 103 |
| GameplayProps | 275 |
| Decoration | 960 |
| Background | 100 |
| Lighting | 155 |
| Effects | 55 |

## 사용 에셋 (인스턴스 수, 개별 FBX: `Assets/Maps/Common/Models/<이름>.fbx`)

`Lollipop_Red` ×112, `Cupcake_Choco` ×106, `GlowDrop_Pink` ×106, `Cupcake_Pink` ×100, `CandyStreetLamp` ×96, `GlowDrop_Yellow` ×90, `GlowDrop_Cyan` ×84, `CandyFlower_Patch` ×77, `GumdropBush_A` ×75, `CandyCane_Red` ×71, `GingerHouse_Cottage` ×44, `CreamPuffTree_A` ×39, `CreamPuffTree_B` ×25, `GingerHouse_Cursed` ×21, `GingerHouse_Twin` ×17, `GingerHouse_Tall` ×15, `Landmark_ClockTower` ×1

## 조명 (`GingerbreadVillage_Lighting.json`)

- Environment Lighting: Color = (0.03, 0.05, 0.14), 강도 0.6
- Fog: Exponential, 색 (0.06, 0.08, 0.2), 밀도 0.006
- Directional Light: 색 (0.55, 0.62, 1.0), 강도 0.16, 회전 X 55° / Y 30° (근사)
- Point Light 153개: `pos_unity`, 색, `range`, 강도, 그림자 여부. 발광 재질만으로 충분하면 생략해도 된다.

## MonsterPlayer 통과

괴물(루트 스케일 3.14: 팔 폭 6.3 m, 키 5.3 m)이 드나드는 문과 실내 통로는 폭 7 m 이상, 높이 7 m 이상이다. 문 안팎 7 m는 막힘 없이 비워 두었다(`Source~/Scripts/door_sweep.py`로 검사).

## 렌더 (`Assets/Maps/Source~/Renders/Blockout/`)

`GingerbreadVillage_TopCam.png`(탑 뷰), `GingerbreadVillage_GameCam_*.png`(게임 카메라 시점), `GingerbreadVillage_Mood.png`(조명), `GingerbreadVillage_Terrain_HeightMap.png` / `_Terrain_Relief_x3.png`(지형)
