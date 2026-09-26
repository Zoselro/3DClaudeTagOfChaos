using UnityEngine;

// 가마솥 풍덩 연출(Plan.md/Cauldron.md §3 A5). 솥 안 수면 트리거에 움직이는 물체가 들어오면 Animator의 Splash 트리거를 당긴다.
// 연출 전용이다 — 괴물 신청 같은 게임 규칙은 씬의 Cauldron(Monster/Cauldron.cs)이 따로 맡는다.
// 원격 캐릭터도 동기화된 위치로 트리거에 들어오므로 모든 클라이언트에서 같은 풍덩이 재생된다(네트워크 동기화 불필요).
// 가마솥 빌더(CauldronBuilder)가 프리팹의 수면 트리거 오브젝트에 붙인다.
[RequireComponent(typeof(Collider))]
public class CauldronSplash : MonoBehaviour
{
    public const string SplashTriggerParameter = "Splash";

    [SerializeField] private Animator animator;
    [Tooltip("한 물체의 여러 콜라이더가 연달아 들어와도 풍덩이 한 번만 재생되도록 하는 최소 간격(초).")]
    [SerializeField, Min(0f)] private float cooldown = 0.5f;

    private int splashHash;
    private float nextAllowedTime;

    private void Awake()
    {
        splashHash = Animator.StringToHash(SplashTriggerParameter);
        if (animator == null) animator = GetComponentInParent<Animator>();
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (animator == null || Time.time < nextAllowedTime) return;
        if (other.attachedRigidbody == null) return;                 // 움직이는 물체(캐릭터 등)만
        if (other.transform.IsChildOf(animator.transform)) return;   // 가마솥 자신의 부품 제외

        nextAllowedTime = Time.time + cooldown;
        animator.SetTrigger(splashHash);
    }
}
