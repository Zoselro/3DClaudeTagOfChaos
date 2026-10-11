# Plan.md 안내

계획 문서가 많아져 2026-10-07에 정리했다. **지금 읽어야 할 문서는 아래 "현재 문서"뿐**이고, 끝난 계획은 `archive/`에 그대로(이름 그대로) 옮겼다.
코드 주석이 `EscapeVisualPlan.md §…`, `DistanceFadePlan.md §…`처럼 문서 이름을 가리키는 곳이 있으면 `archive/`에서 같은 이름을 찾으면 된다.

## 현재 문서
| 문서 | 무엇 | 상태 |
|---|---|---|
| `Claude.md` | 작업 규칙(폴더·코드·언어) | 항상 |
| **`Request1009Plan.md`** | 2026-10-09 요청 11건(상자 길게 누르기·상자/로비/로켓 소리·대기실·오븐·관전·회전목마·놀이공원 충돌체/잡기) — 결과 기록 §15. **§16 2026-10-11 추가 8건**(상자 변장 시간 숨김·아이템 그림·쿠키 들기·아이템 들기 자세·비네트 제거·출력 장치·베이커리 고장 소리) — 결과 §16.11 | ✅ R0~R6·R8~R10 · R7 결과 화면 곡 **승인 대기** · ✅ N0~N9 (2026-10-11) |
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
| `Builds/S9Check/`·`Builds/V6Check/`·`Builds/R1009Check/` — 확인용 빌드, 필요 없으면 삭제 | SoundPlan S9, TwistedCandyPlan V6, Request1009 R6 |
| 두 사람 멀티에서 숨을 곳·실내 소리·새 소리 확인, 새 소리 크기를 실제 스피커로 조정 | TwistedCandyPlan V6 |
| 대기실 점광원 14개 프레임 확인(프로파일링 미실시) — Request1009 R3에서 마녀 집 스포트 4개 추가 | archive/GameLobbyScene.md, Request1009 R3 |
| 결과 화면 곡 후보(`Source~/twisted/review/Result_candidate.ogg`·징글 묶음 3개) 청취 → 승인되면 R7(`MusicId.Result`)로 넣기 | Request1009 R2·R7 |
| 두 사람 이상 멀티: 관전 순환(탈출 = 일반 쿠키만, 부서짐 = 스파이 포함), 다른 사람을 따라 진저브레드 지하로 내려갈 때 동굴 소리, 길게 누르기 중 다른 사람이 먼저 연 상자 취소 | Request1009 R1·R4 |
| 두 사람 이상 멀티: 다른 쿠키를 F로 들고 걷기·달리기 — **세 번째 사람 화면**에서도 들린 쿠키가 드는 쪽 팔 위에 붙어 있는지(새 동기화 값 `RemoteCarrierViewId`), 두 손·한 손 아이템이 남의 화면에서도 손에 있는지, 변장 시간이 끝날 때 모든 화면에서 상자가 함께 나타나는지 | Request1009 §16 N1·N3·N4 |
| 베이커리 고장 소리 청취(`Source~/r1009/review/reel_N7_oven_broken.wav`) — 이미 넣음, 마음에 안 들면 `SFX/Device/OvenBroken.wav`만 교체. 출력 장치(스피커·헤드폰·헤드셋)를 실제 장치로 들어 보기 | Request1009 §16 N6·N7 |
| 쿠키 손에 손가락 뼈가 없어 한 손 아이템은 주먹 쥐는 동작 없이 오른손 위치만 따라간다 — 모델에 손가락이 생기면 쥐는 레이어 추가 | Request1009 §16 N4 |

## archive/ (끝난 계획·옛 판)
| 묶음 | 문서 |
|---|---|
| 소리 | `AudioProductionPlan.md`(S8 음원 방향 — TwistedCandyPlan §5로 대체), `AudioP1ListeningGuide.md`, `AudioS8ListeningGuide.md`, `DistanceFadePlan.md` |
| 탈출·게임 수정 | `EscapeVisualPlan.md`, `GameFixPlan.md`, `Request1003Plan.md`, `Request1003bPlan.md`, `Bug-fix-plan.md` |
| 맵·씬 | `GameScenePlan.md`, `GameLobbyScene.md`, `MapReplacePlan.md`, `MapReplacePlan.v1.md`, `Cauldron.md` |
| 캐릭터·구조 | `PlayerControllPlan.md`, `Monster-Rerig-Plan.md`, `GameManager.md`, `RoomItemPlan.md`, `architecture-review.md`, `architecture-review-plan.md` |
| 처음 기획·옛 조사 | `UserPlan.md`(첫 요청), `Plan.md`(옛 조치 계획), `research.prev.md`(조사 4차 판) |
