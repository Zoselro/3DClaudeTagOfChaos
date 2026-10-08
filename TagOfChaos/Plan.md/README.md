# Plan.md 안내

계획 문서가 많아져 2026-10-07에 정리했다. **지금 읽어야 할 문서는 아래 "현재 문서"뿐**이고, 끝난 계획은 `archive/`에 그대로(이름 그대로) 옮겼다.
코드 주석이 `EscapeVisualPlan.md §…`, `DistanceFadePlan.md §…`처럼 문서 이름을 가리키는 곳이 있으면 `archive/`에서 같은 이름을 찾으면 된다.

## 현재 문서
| 문서 | 무엇 | 상태 |
|---|---|---|
| `Claude.md` | 작업 규칙(폴더·코드·언어) | 항상 |
| **`TwistedCandyPlan.md`** | 뒤틀린 과자 동화 개편(맵 분위기·망가진 소품·숨을 곳·색칠 위장·배경음/효과음 다시 만들기) — 작업 기록·다시 실행 순서 §10 | ✅ V0~V6 (2026-10-07) |
| `GameRule.md` | 게임 규칙 명세(숨바꼭질·괴물·라운드) | 기준 문서 |
| `EscapePlan.md` | 탈출 모드 명세(장치·재료·스파이·마녀) | 기준 문서 |
| `SoundPlan.md` | 소리 시스템 구조와 작업 기록(S0~S9 ✅) | 기준 문서 |
| `SoundReplacementList.md` | 음원 110개 명세(ID·시점·길이·반복·2D/3D) | 기준 문서 — 개편 때도 그대로 |
| `research.md` | 코드 전수 조사(R 번호 — 코드 주석이 참조) | 참고 |
| `images/` | 계획서에 쓰는 그림(`images/twisted/` — 개편 목업) | |

## 남은 확인(옛 문서에서 옮김)
| 항목 | 출처 |
|---|---|
| 캔디숲·진저브레드·공장·놀이공원·베이커리·대기실 씬에 저장된 시험용 `GameObject`(OfflineModeBootstrap) — 출시 전에 제거(BuildSceneTests 6개 실패 원인) | SoundPlan S7·S9, TwistedCandyPlan V6 |
| 빌드에서 소리·음량 설정 저장을 직접 들어 보고 확인, 멀티에서 다른 사람 소리 확인 | SoundPlan S9 |
| `Builds/S9Check/`·`Builds/V6Check/` — 확인용 빌드, 필요 없으면 삭제 | SoundPlan S9, TwistedCandyPlan V6 |
| 두 사람 멀티에서 숨을 곳·실내 소리·새 소리 확인, 새 소리 크기를 실제 스피커로 조정 | TwistedCandyPlan V6 |
| 대기실 점광원 14개 프레임 확인(프로파일링 미실시) | archive/GameLobbyScene.md |

## archive/ (끝난 계획·옛 판)
| 묶음 | 문서 |
|---|---|
| 소리 | `AudioProductionPlan.md`(S8 음원 방향 — TwistedCandyPlan §5로 대체), `AudioP1ListeningGuide.md`, `AudioS8ListeningGuide.md`, `DistanceFadePlan.md` |
| 탈출·게임 수정 | `EscapeVisualPlan.md`, `GameFixPlan.md`, `Request1003Plan.md`, `Request1003bPlan.md`, `Bug-fix-plan.md` |
| 맵·씬 | `GameScenePlan.md`, `GameLobbyScene.md`, `MapReplacePlan.md`, `MapReplacePlan.v1.md`, `Cauldron.md` |
| 캐릭터·구조 | `PlayerControllPlan.md`, `Monster-Rerig-Plan.md`, `GameManager.md`, `RoomItemPlan.md`, `architecture-review.md`, `architecture-review-plan.md` |
| 처음 기획·옛 조사 | `UserPlan.md`(첫 요청), `Plan.md`(옛 조치 계획), `research.prev.md`(조사 4차 판) |
