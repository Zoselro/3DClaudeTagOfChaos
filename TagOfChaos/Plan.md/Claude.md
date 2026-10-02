## 폴더 규칙

| 분류 | 경로 |
|---|---|
| Scripts | `Assets/02. Scripts/{도메인}/` |
| SO | `Assets/03. SO/{도메인}/` |
| Prefabs | `Assets/04. Prefabs/` |
| UI 프리팹 | `Resources/UI/{Popup|Scene|Tab}/{클래스명}` |
| 유닛 SO | `Assets/03. SO/Unit/{등급}Star/{UnitId}.asset` |
| 전역 SO | `Assets/Resources/GameSettings` |
| 에디터 | `Assets/Editor/` |

## 반드시 지켜야할 점
- 주석 제외 한글 사용 금지
- OOP 기반 설계
- 계획부터 말하고 승인 받은 후에 작업 진행
- 최적화를 고려한 코드 작성

## 주요 시스템

전체 구조와 현재 문제 목록은 `Plan.md/research.md`가 기준이다.

| 시스템 | 위치 | 문서 |
|---|---|---|
| 공통(설정 SO·입력·방 상태·단계) | `Scripts/Core/` | `Plan.md/research.md` |
| 로비·대기방(방 목록·시작 권한·스킨) | `Scripts/Lobby/` | `Plan.md/RoomItemPlan.md`, `Plan.md/GameLobbyScene.md` |
| 게임 진행(스폰·ESC 메뉴·방 설정·나가기) | `Scripts/GameManager/` | `Plan.md/GameManager.md`, `Plan.md/GameRule.md` |
| 괴물·괴물 배정·가마솥·잡기 | `Scripts/Monster/` | `Plan.md/GameRule.md`, `Plan.md/Monster-Rerig-Plan.md`, `Plan.md/Cauldron.md` |
| 쿠키 이동·잡기·동기화 | `Scripts/Unit/` | `Plan.md/PlayerControllPlan.md` |
| 색칠(팔레트·붓·몸 색칠) | `Scripts/ColorTag/` | `Plan.md/GameScenePlan.md` |
| 탈출 모드(상자·장치·로켓·마녀) | `Scripts/Escape/` | `Plan.md/EscapePlan.md`, `Plan.md/EscapeVisualPlan.md` |
| 상호작용(후보 선택·안내 문구) | `Scripts/Interaction/` | `Plan.md/EscapePlan.md` |
| 카메라(3인칭·괴물 1인칭·흔들림) | `Scripts/Camera/`, `Scripts/Core/CameraShake.cs` | `Plan.md/research.md` |
| 환경(문·가마솥 연출) | `Scripts/Environment/` | `Plan.md/GameScenePlan.md` |
| 맵 도구(축소·통과성 검사·시험 주행) | `Editor/Maps/` | `Plan.md/MapReplacePlan.md` |
| 사운드(예정) | `Scripts/Audio/` | `Plan.md/SoundPlan.md` |
| 개발 도구(빌드 씬에 넣지 않음 — `BuildSceneTests`) | `Scripts/Dev/`, `Editor/` | `Plan.md/research.md` |