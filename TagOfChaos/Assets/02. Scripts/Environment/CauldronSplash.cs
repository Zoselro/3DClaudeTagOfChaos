using UnityEngine;

// 가마솥 풍덩 연출(Plan.md/Cauldron.md §3 A5). 솥 안 수면 트리거에 움직이는 물체가 들어오면 Animator의 Splash 트리거를 당긴다.
// 연출 전용이다 — 괴물 신청 같은 게임 규칙은 씬의 Cauldron(Monster/Cauldron.cs)이 따로 맡는다.
// 원격 캐릭터도 동기화된 위치로 트리거에 들어오므로 모든 클라이언트에서 같은 풍덩이 재생된다(네트워크 동기화 불필요).
// 가마솥 빌더(CauldronBuilder)가 프리팹의 수면 트리거 오브젝트에 붙인다.
//
// Splash 레이어(덮어쓰기)는 풍덩이 재생되는 동안만 가중치 1이다. 가중치를 1로 둔 채 빈 상태(Empty)로 돌아가면 그 레이어가
// 마지막 값을 붙잡고 Base 레이어의 Idle을 덮어써 불꽃·연기·수면이 영구히 멈췄다(Bug-fix-plan.md §35). 풍덩이 끝나면 가중치를 0으로 되돌린다.
// 되돌릴 때까지만 Update가 돌도록 평소에는 컴포넌트를 꺼 둔다 — OnTriggerEnter는 꺼진 컴포넌트에도 전달된다.
[RequireComponent(typeof(Collider))]
public class CauldronSplash : MonoBehaviour
{
    public const string SplashTriggerParameter = "Splash";
    public const string SplashLayerName = "Splash";
    public const string SplashStateName = "Splash";

    [SerializeField] private Animator animator;
    [Tooltip("한 물체의 여러 콜라이더가 연달아 들어와도 풍덩이 한 번만 재생되도록 하는 최소 간격(초).")]
    [SerializeField, Min(0f)] private float cooldown = 0.5f;

    private int splashHash;
    private int splashStateHash;
    private int splashLayer = -1;
    private bool splashStarted; // Animator가 트리거를 받아 Splash 상태에 들어간 것을 한 번이라도 봤는지
    private float nextAllowedTime;

    private void Awake()
    {
        splashHash = Animator.StringToHash(SplashTriggerParameter);
        splashStateHash = Animator.StringToHash(SplashStateName);
        if (animator == null) animator = GetComponentInParent<Animator>();
        if (animator != null)
        {
            splashLayer = animator.GetLayerIndex(SplashLayerName);
            if (splashLayer < 0) Debug.LogWarning($"[CauldronSplash] Animator layer '{SplashLayerName}' not found on {animator.name}.");
            else animator.SetLayerWeight(splashLayer, 0f);
        }
        GetComponent<Collider>().isTrigger = true;
        enabled = false;
        // 가마솥이 끓는 소리(가까이 가면 들리는 3D 반복음, SoundPlan.md S4). 이 컴포넌트는 꺼 두므로 별도 컴포넌트가 낸다.
        if (GetComponent<AmbientEmitter>() == null) gameObject.AddComponent<AmbientEmitter>().Configure(SoundId.CauldronBubble);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (animator == null || Time.time < nextAllowedTime) return;
        if (other.attachedRigidbody == null) return;                 // 움직이는 물체(캐릭터 등)만
        if (other.transform.IsChildOf(animator.transform)) return;   // 가마솥 자신의 부품 제외

        nextAllowedTime = Time.time + cooldown;
        GameAudio.PlayAt(SoundId.CauldronSplash, transform.position); // 풍덩(SoundPlan.md S4)
        if (splashLayer >= 0) animator.SetLayerWeight(splashLayer, 1f);
        animator.SetTrigger(splashHash);
        splashStarted = false;
        enabled = true;
    }

    // 트리거를 당긴 직후 프레임에는 Animator가 아직 갱신 전이라 Empty로 보이므로, Splash 상태를 한 번 본 뒤
    // 다시 Empty로 돌아왔을 때 가중치를 내린다. 재생 중 다시 발동하면 Splash에 머물러 있으므로 그대로 이어진다.
    private void Update()
    {
        if (animator == null || splashLayer < 0)
        {
            enabled = false;
            return;
        }

        if (animator.IsInTransition(splashLayer) || animator.GetCurrentAnimatorStateInfo(splashLayer).shortNameHash == splashStateHash)
        {
            splashStarted = true;
            return;
        }
        if (!splashStarted) return;

        animator.SetLayerWeight(splashLayer, 0f);
        enabled = false;
    }
}
