using UnityEngine;

// 게임 맵 씬 표시(맵의 GameSceneCore/PaintManagers에 배치). 예전에는 색칠 단계(60초)가 끝나면 등록 슬롯이 0개인 쿠키에게 색을 배정해
// 전신 강제 도포했지만(GameRule.md §3.6), 색칠 단계는 Request1009Plan.md §25.5 C에서 없앴다. 이제 "이 씬이 게임 맵인지"만 알린다 —
// 파괴 판정(HideOrSeekPlayer.IsBroken)·색깔 물풍선 강제 도포·스탬프 재생(PlayerPaintCanvas)이 이 값을 본다.
// 씬 이름 비교 대신 이 컴포넌트의 존재로 판단하므로, 테스트 씬도 이 컴포넌트를 두면 같은 규칙을 따른다.
public class PaintPhaseController : MonoBehaviour
{
    public static bool IsPaintScene { get; private set; }

    [Tooltip("예전 강제 도포 팔레트(쓰지 않음 — 씬 참조 보존).")]
    [SerializeField] private ColorPaletteSO palette;

    private void Awake()
    {
        IsPaintScene = true;
    }

    private void OnDestroy()
    {
        IsPaintScene = false;
    }
}
