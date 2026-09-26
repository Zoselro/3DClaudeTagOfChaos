using UnityEngine;

// 문 자동 개폐(Plan.md/GameLobbyScene.md §12). 문 앞뒤 감지 구역에 캐릭터가 있으면 열고, 일정 시간 비면 닫는다.
// 문 상태는 이미 동기화되는 캐릭터 위치에서 파생되는 표시 상태라 각 클라이언트가 스스로 계산한다(RPC·Room Props 불필요).
// 내 캐릭터는 내 클라이언트의 실제 위치로 판정하므로 네트워크 지연 때문에 닫힌 문에 막히지 않는다.
// 과자집 빌더(WitchCookieHouseBuilder)가 문 오브젝트(Animator가 붙은 경첩 피벗)에 붙인다.
[RequireComponent(typeof(Animator))]
public class AutoDoor : MonoBehaviour
{
    public const string DefaultOpenParameter = "IsOpen";
    private const float FallbackCloseDuration = 0.6f;

    [SerializeField] private string openParameter = DefaultOpenParameter;
    [Tooltip("문짝 충돌체. 비어 있으면 자식 콜라이더를 찾는다.")]
    [SerializeField] private Collider leafCollider;
    [Tooltip("벽을 따라 잰 감지 구역 폭(m). 개구부보다 조금 넓게.")]
    [SerializeField, Min(0.1f)] private float zoneWidth = 2.2f;
    [Tooltip("벽 앞뒤 각각의 감지 깊이(m). 안쪽은 문짝이 도는 범위를 덮어야 한다.")]
    [SerializeField, Min(0.1f)] private float zoneDepth = 2.5f;
    [SerializeField, Min(0.1f)] private float zoneHeight = 3.2f;
    [Tooltip("구역이 이 시간(초) 동안 비어 있으면 닫는다.")]
    [SerializeField, Min(0f)] private float closeDelay = 0.5f;
    [SerializeField, Min(0.02f)] private float checkInterval = 0.1f;

    private Animator animator;
    private int openHash;
    private float closeDuration = FallbackCloseDuration;

    // 닫힌 상태 기준 감지 구역(월드). 과자집은 움직이지 않는 정적 배치라 Awake에서 한 번만 계산한다.
    private Vector3 zoneCenter;
    private Quaternion zoneRotation = Quaternion.identity;
    private Vector3 zoneHalfExtents;

    private bool isOpen;
    private float lastOccupiedTime = float.NegativeInfinity;
    private float nextCheckTime;
    private float colliderRestoreTime = -1f; // 닫힘 애니메이션이 끝나 충돌체를 되살릴 시각(-1 = 예약 없음)

    public bool IsOpen => isOpen;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        openHash = Animator.StringToHash(openParameter);
        if (leafCollider == null) leafCollider = GetComponentInChildren<Collider>(true);
        closeDuration = FindClipLength("Close", FallbackCloseDuration);
        ComputeZone();
    }

    private void Update()
    {
        if (Time.time >= nextCheckTime)
        {
            nextCheckTime = Time.time + checkInterval;
            if (IsAnyCharacterInZone()) lastOccupiedTime = Time.time;
        }

        bool wantOpen = Time.time - lastOccupiedTime <= closeDelay;
        if (wantOpen && !isOpen) Open();
        else if (!wantOpen && isOpen) Close();

        if (colliderRestoreTime >= 0f && Time.time >= colliderRestoreTime)
        {
            colliderRestoreTime = -1f;
            SetLeafColliderEnabled(true);
        }
    }

    // 열기 시작하는 순간 문짝 충돌체를 끈다 — 안쪽으로 도는 문짝이 캐릭터를 밀어내거나 열리는 동안 막지 않도록.
    private void Open()
    {
        isOpen = true;
        colliderRestoreTime = -1f;
        SetLeafColliderEnabled(false);
        animator.SetBool(openHash, true);
    }

    // 충돌체는 닫힘 애니메이션이 끝난 뒤에 되살린다. 닫히는 도중 누가 들어오면 Open()이 예약을 취소한다.
    private void Close()
    {
        isOpen = false;
        animator.SetBool(openHash, false);
        colliderRestoreTime = Time.time + closeDuration;
    }

    private void SetLeafColliderEnabled(bool enabled)
    {
        if (leafCollider != null) leafCollider.enabled = enabled;
    }

    private bool IsAnyCharacterInZone()
    {
        foreach (IGameCharacter character in CharacterRegistry.All)
        {
            if (!CharacterRegistry.IsAlive(character) || !CanOpenDoors(character)) continue;
            if (IsInZone(character.gameObject.transform.position)) return true;
        }
        return false;
    }

    // 파괴된 쿠키(관전 중)는 문을 열지 않는다. 네트워크가 없는 테스트 캐릭터는 항상 연다.
    private static bool CanOpenDoors(IGameCharacter character)
    {
        if (character.Role != CharacterRole.Cookie) return true;
        var view = character.View;
        return view == null || view.Owner == null || !RoomState.IsBroken(view.Owner);
    }

    private bool IsInZone(Vector3 worldPosition)
    {
        Vector3 local = Quaternion.Inverse(zoneRotation) * (worldPosition - zoneCenter);
        return Mathf.Abs(local.x) <= zoneHalfExtents.x
            && Mathf.Abs(local.y) <= zoneHalfExtents.y
            && Mathf.Abs(local.z) <= zoneHalfExtents.z;
    }

    // 닫힌 문짝의 가장 얇은 축을 벽의 법선으로 보고 구역을 만든다 — 문 방향별 하드코딩이 필요 없다.
    private void ComputeZone()
    {
        Vector3 center = transform.position;
        Vector3 normal = transform.forward;

        var meshCollider = leafCollider as MeshCollider;
        if (meshCollider != null && meshCollider.sharedMesh != null)
        {
            Transform t = meshCollider.transform;
            Bounds local = meshCollider.sharedMesh.bounds;
            Vector3 size = Vector3.Scale(local.size, t.lossyScale);
            center = t.TransformPoint(local.center);
            normal = size.x <= size.z ? t.right : t.forward;
        }
        else if (leafCollider != null)
        {
            Bounds world = leafCollider.bounds;
            center = world.center;
            normal = world.size.x <= world.size.z ? Vector3.right : Vector3.forward;
        }

        normal = Vector3.ProjectOnPlane(normal, Vector3.up);
        if (normal.sqrMagnitude < 1e-6f) normal = Vector3.forward;

        zoneRotation = Quaternion.LookRotation(normal.normalized, Vector3.up); // 로컬 x = 벽 방향, z = 벽 법선
        zoneHalfExtents = new Vector3(zoneWidth * 0.5f, zoneHeight * 0.5f, zoneDepth);
        zoneCenter = new Vector3(center.x, transform.position.y + zoneHeight * 0.5f, center.z);
    }

    private float FindClipLength(string keyword, float fallback)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return fallback;
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            if (clip != null && clip.name.Contains(keyword)) return clip.length;
        return fallback;
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
        {
            if (leafCollider == null) leafCollider = GetComponentInChildren<Collider>(true);
            ComputeZone();
        }
        Gizmos.color = isOpen ? new Color(0.3f, 1f, 0.4f, 0.35f) : new Color(1f, 0.6f, 0.2f, 0.35f);
        Gizmos.matrix = Matrix4x4.TRS(zoneCenter, zoneRotation, Vector3.one);
        Gizmos.DrawCube(Vector3.zero, zoneHalfExtents * 2f);
    }
}
