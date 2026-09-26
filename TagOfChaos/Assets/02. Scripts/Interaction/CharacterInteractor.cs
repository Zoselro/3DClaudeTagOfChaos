using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// 내가 조종하는 캐릭터(쿠키·괴물) 주변의 상호작용 사물을 찾아 안내 문구를 띄우고, 상호작용 키를 누르면 사물에
// 전달한다(GameLobbyScene.md §14). 쿠키·괴물 프리팹을 고치지 않도록 게임 시작 때 하나만 자동으로 만들어
// 씬이 바뀌어도 유지한다 — 조종 중인 캐릭터는 CharacterRegistry, 사물은 InteractableRegistry에서 찾는다.
// 쓸 수 있는 캐릭터: IGameCharacter.CanInteract가 true인 캐릭터(파괴·들림·채팅 중인 쿠키, 처형·돌진 중인 괴물 제외).
public class CharacterInteractor : MonoBehaviour
{
    private const float CheckInterval = 0.1f;
    private const float MaxHeightDifference = 2.5f; // 층이 다른 사물(예: 지붕 위)은 무시

    private static CharacterInteractor instance;

    private InteractionPromptUI prompt;
    private IGameCharacter localCharacter;
    private IInteractable focused;
    private float nextCheckTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject(nameof(CharacterInteractor));
        DontDestroyOnLoad(go);
        instance = go.AddComponent<CharacterInteractor>();
    }

    private void Awake()
    {
        prompt = InteractionPromptUI.Create(transform);
    }

    private void Update()
    {
        if (Time.time >= nextCheckTime)
        {
            nextCheckTime = Time.time + CheckInterval;
            Refresh();
        }

        if (focused == null || !PlayerInput.InteractPressed) return;

        // 검사 주기(0.1초) 사이에 상태가 바뀌었을 수 있으므로 누른 순간 다시 확인한다.
        Refresh();
        if (focused == null) return;
        focused.Interact(localCharacter);
    }

    private void Refresh()
    {
        localCharacter = FindLocalCharacter();
        focused = localCharacter != null && localCharacter.CanInteract && !IsTypingInUi()
            ? FindNearest(localCharacter)
            : null;

        if (prompt == null) return;
        if (focused != null) prompt.Show(PlayerInput.Bindings.InteractKey);
        else prompt.Hide();
    }

    private static IGameCharacter FindLocalCharacter()
    {
        foreach (IGameCharacter c in CharacterRegistry.All)
            if (CharacterRegistry.IsAlive(c) && c.View != null && c.View.IsMine) return c;
        return null;
    }

    // 수평 거리가 가장 가까운, 범위 안의 사용 가능한 사물.
    private static IInteractable FindNearest(IGameCharacter character)
    {
        Vector3 position = character.gameObject.transform.position;
        IInteractable best = null;
        float bestSqr = float.MaxValue;

        foreach (IInteractable candidate in InteractableRegistry.All)
        {
            if (!InteractableRegistry.IsAlive(candidate)) continue;

            Vector3 delta = candidate.InteractionPoint - position;
            if (Mathf.Abs(delta.y) > MaxHeightDifference) continue;
            delta.y = 0f;
            float sqr = delta.sqrMagnitude;
            float range = candidate.InteractionRange;
            if (sqr > range * range || sqr >= bestSqr) continue;
            if (!candidate.CanInteract(character)) continue;

            best = candidate;
            bestSqr = sqr;
        }
        return best;
    }

    // 채팅 입력 중에 친 키가 상호작용으로 처리되지 않게 한다(괴물은 채팅 이동 잠금 대상이 아니므로 여기서 거른다).
    private static bool IsTypingInUi()
    {
        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        return selected != null && selected.TryGetComponent(out TMP_InputField field) && field.isFocused;
    }
}
