# Maps — TagOfChaos 환경 맵 5종

| 맵 | 폴더 | 분위기 |
|---|---|---|
| CandyForest (사탕숲) | `CandyForest/` | 분홍 설탕눈 숲, 거대 막대사탕 나무 |
| GingerbreadVillage (과자마을) | `GingerbreadVillage/` | 밤의 진저브레드 마을, 과자 시계탑 |
| ChocolateFactory (초콜릿공장) | `ChocolateFactory/` | 유리 지붕 공장, 초콜릿 탱크와 강 |
| CursedCandyCarnival (저주받은 놀이공원) | `CursedCandyCarnival/` | 네온 관람차, 서커스 천막 |
| HauntedBakery (마녀의 유령 빵집) | `HauntedBakery/` | 마법 오븐, 보라 천과 나무 인테리어 |

- `Common/Materials`: 기존 프로젝트에 없던 재질만(Built-in Standard, 텍스처 없음). 기존 `M_*` 재질은 이름으로 재사용된다.
- `Common/Models`: 에셋 하나당 FBX 하나(프리팹 원본). 원점은 바닥 중앙이고, 앞은 Unity +Z다.
- `Source~/`: Blender 원본·스크립트·렌더·문서. `~`로 끝나는 폴더라 Unity가 가져오지 않는다.
- 각 맵 `README.md`에 FBX 목록, 충돌체, 배치, 조명, 괴물 통과 기준을 정리했다.
- Unity 코드와 씬은 변경하지 않았다. 씬 구성은 각 맵 FBX를 원점에 배치해서 만든다.
