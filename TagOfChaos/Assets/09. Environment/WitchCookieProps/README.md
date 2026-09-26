# Witch Cookie Props (과자집 실내·실외 소품)

과자집(`../WitchCookieHouse`)과 같은 규칙으로 제작한 소품 56종과 전체 배치.

## 기준
- 1 unit = 1 m. 쿠키 캐릭터 기준 2 m 높이, 0.92 m 폭
- 로비 씬 임시 배치에 맞춘 치수: 테이블 상판 0.75 m, 의자 좌판 0.45 m, 파라솔 천 2.5 m, 울타리 기둥 0.9 m·간격 1.2 m
- 피벗: 모든 에셋은 바닥 중앙. 예외는 아래와 같다.
  - `Hanging_Lantern`, `Tree_Branch_Decoration`: 윗고리
  - `Candy_Fence_*`: 첫 기둥 밑동
  - `Wall_Skull`, `Spider_Web`: 벽에 닿는 평평한 뒷면
- 정면(Unity +Z)은 사용하는 쪽이다. 의자는 등받이가 -Z쪽에 있다.

## 파일
| 경로 | 내용 |
|---|---|
| `Interior/ Exterior/ Ground/ Decoration/ Effects/*.fbx` | 에셋별 FBX (루트 = 에셋 이름) |
| `Materials/*.mat` | 새 재질 32종. 나머지 18종은 `WitchCookieHouse/Materials`를 이름으로 재사용 |
| `Textures/` | `T_Soil_BaseColor`(4 m 반복), `T_Mushroom_Gradient`(갓 높이 그라데이션) |
| `Witch_Cookie_Layout.json` | Blender 배치(Unity 좌표) 407개 |
| `Assets/04. Prefabs/Environment/WitchCookieProps/{카테고리}/*.prefab` | 에셋별 프리팹 (Static 지정, `Spell_Book_Closed` 제외) |
| `Assets/04. Prefabs/Environment/Witch_Cookie_Environment.prefab` | 과자집 + 실내 + 실외 전체 배치 |
| `리소스/WitchCookieHouse/Witch_Cookie_House.blend` | 원본. 에셋은 `A_<이름>` 컬렉션, 배치는 `WCH_Layout` |

## 에셋 목록
- **Ground**: Ground_Floor, Ground_Floor_Tile(2.78 m 반복), Ground_Floor_Debris, Ground_Magic_Circle(교체용 별도 메시), Ground_Exterior(8 m 모듈), Ground_Exterior_Far(128 m 배경), Ground_Cookie_Path(2 m 모듈), Ground_Grass_Patch, Ground_Cookie_Debris, Ground_Rock
- **Interior**: Witch_Table, Witch_Chair_A/B, Potion_Shelf, Potion_Bottle_Round/Tall/Square/Triangle, Spell_Book_Closed/Open
- **Exterior**: Candy_Parasol, Outdoor_Table, Outdoor_Chair_A/B, Twisted_Tree_A/B/C, Tree_Root, Tree_Branch_Decoration, Giant_Mushroom_A/B/C, Small_Mushroom_Cluster, Candy_Fence_Straight/Corner/Broken
- **Decoration**: Witch_Candle, Witch_Broom, Magic_Crystal(실내외 공용), Wall_Skull, Spider_Web(실내외 공용), Hanging_Lantern, Candy_Jar, Cookie_Plate, Witch_Tea_Set, Candy_Lantern, Cookie_Bench, Candy_Sign, Candy_Planter, Cookie_Crate, Gravestone, Candy_Flower, Small_Rock, Fallen_Leaf
- **Effects**: FX_Firefly_Cluster, FX_Purple_Mist
- `Broken_Fence`는 `Candy_Fence_Broken`과 같은 에셋이다.

## 조작 / 교체 포인트
- `Spell_Book_Closed_Cover_Front`: 피벗이 책등 경첩선에 있다. 로컬 Z축 기준으로 약 -180° 회전하면 펼쳐진다.
- `Candy_Parasol_Canopy`: 피벗이 기둥 축 위(3.05 m)에 있어 Y축 회전이 가능하다.
- `Twisted_Tree_*_Attach_0..2`: 랜턴이나 거미줄을 걸 수 있는 부착점(빈 오브젝트)이다.
- 발광 재질은 아래와 같다. Emission 값만 조절하면 된다.
  - `M_Magic_*`, `M_Liquid_*`, `M_Mushroom_Glow`, `M_Lantern_Glow`, `M_Flame_Purple`, `M_FX_*`
- 투명 재질은 `M_Glass_Clear`(물약병·사탕 병 유리, Standard Transparent) 하나뿐이다.
- 충돌체: 주요 소품 24종에 `COL_*` 볼록 메시가 있으며, 임포트할 때 convex MeshCollider로 자동 변환된다. 작은 장식에는 충돌체가 없다.

## 재생성
- 소품 프리팹과 환경 프리팹: `Tools/TagOfChaos/Build Witch Cookie Props`
- 검증: `Tools/TagOfChaos/Verify Witch Cookie Props`

## 검증 결과 (Unity 6000.0.58f2)
```
prefabs=56/56 badMaterialSlots=0 emptyMeshes=0 convexColliders=32 collisionWithRenderer=0 OK
Witch_Table 0.95 m / Outdoor_Table 0.83 m / Candy_Parasol 3.39 m / Candy_Fence_Straight 2.51 m / Potion_Shelf 2.54 m / Twisted_Tree_C 9.30 m  OK
environment placed=407/407 bounds 128 x 21.1 x 128 m OK
```
