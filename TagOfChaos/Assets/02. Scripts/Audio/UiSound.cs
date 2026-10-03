using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 버튼 하나의 호버·클릭 소리(SoundPlan.md S2). 에디터 도구(Tools/TagOfChaos/Audio/Attach UI Sounds)가 빌드 씬·UI 프리팹의
// 모든 Button에 붙이고 버튼 용도에 맞는 클릭 소리(확인·취소·시작·색 고르기 …)를 넣는다. 붙지 않은 버튼은 AudioTests가 잡는다.
// 클릭 소리는 버튼의 원래 동작과 별개로 onClick에 더한다 — 씬을 넘기는 버튼이어도 소리는 AudioRuntime(DDOL)에서 끝까지 난다.
[RequireComponent(typeof(Button))]
public class UiSound : MonoBehaviour, IPointerEnterHandler
{
    [SerializeField] private SoundId click = SoundId.UiClick;
    [SerializeField] private SoundId hover = SoundId.UiHover;

    private Button button;

    public SoundId Click => click;
    public SoundId Hover => hover;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(PlayClick);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(PlayClick);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hover != SoundId.None && button.IsInteractable()) GameAudio.Play(hover);
    }

    private void PlayClick()
    {
        UiSoundCues.ButtonClick(click); // 같은 프레임에 저장·오류음이 나면 그쪽이 대신한다
    }

#if UNITY_EDITOR
    public void EditorSetup(SoundId clickSound, SoundId hoverSound)
    {
        click = clickSound;
        hover = hoverSound;
    }
#endif
}
