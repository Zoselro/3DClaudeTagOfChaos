using UnityEngine;

// UI 표시/숨김 헬퍼. GameObject.SetActive(false)로 자기 자신을 끄면 Update()가 멈추고,
// MonoBehaviourPunCallbacks라면 OnDisable에서 Photon 콜백 등록까지 풀려 다시는 스스로 켜질 수 없다
// (Bug-fix-plan.md §23.2.1). 오브젝트는 활성 상태로 두고 CanvasGroup으로 알파·입력만 전환한다.
public static class CanvasGroupVisibility
{
    public static CanvasGroup Ensure(GameObject target)
    {
        if (target == null) return null;
        var group = target.GetComponent<CanvasGroup>();
        return group != null ? group : target.AddComponent<CanvasGroup>();
    }

    public static void Set(CanvasGroup group, bool visible)
    {
        if (group == null) return;
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }
}
