using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// 내가 조종하는 캐릭터(쿠키·괴물) 주변의 상호작용 사물을 찾아 안내 문구를 띄우고, 상호작용 키를 누르면 사물에
// 전달한다(GameLobbyScene.md §14). 쿠키·괴물 프리팹을 고치지 않도록 게임 시작 때 하나만 자동으로 만들어
// 씬이 바뀌어도 유지한다 — 조종 중인 캐릭터는 CharacterRegistry, 사물은 InteractableRegistry에서 찾는다.
// 쓸 수 있는 캐릭터: IGameCharacter.CanInteract가 true인 캐릭터(파괴·들림 중인 쿠키, 처형·돌진 중인 괴물 제외). 채팅 중에는
// PlayerInput.InteractPressed가 억제되고, 안내 문구는 IsTypingInUi로 숨긴다.
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

        // 괴물이 조준한 쿠키가 있으면 E키는 잡기가 우선이다 — 문 등 다른 상호작용보다 먼저(GameFixPlan.md F1-1).
        if (PlayerInput.InteractPressed && localCharacter is MonsterController monster && monster.TryGrabAimTarget()) return;

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
        // 괴물이 쿠키를 조준 중이면 E키는 잡기로 쓰이므로(F1-1) 사물 아이콘을 띄우지 않는다. 잡기에는 아이콘이 없다(F4).
        bool grabAiming = localCharacter is MonsterController monster && monster.HasGrabAimTarget;
        if (focused != null && !grabAiming)
            prompt.Show(PlayerInput.Bindings.InteractKey, focused.InteractionPoint, (focused as IInteractionLabel)?.GetLabel(localCharacter));
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

    // 채팅 입력 중에는 안내 문구를 띄우지 않는다(키 입력 자체는 PlayerInput.IsGameplaySuppressed가 막는다).
    private static bool IsTypingInUi()
    {
        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        return selected != null && selected.TryGetComponent(out TMP_InputField field) && field.isFocused;
    }
}
