using System.Collections.Generic;
using UnityEngine;

// 손에 든 도구 쓰기(EscapePlan.md §1.8). 맞힌 대상은 쓰는 사람 화면에서 고르고, 방장이 횟수·쿨다운을 확인해
// 모두에게 명중(ToolHit)을 알린다 — 기절·떨어뜨리기·색칠은 맞은 사람 본인이 처리한다(ToolHitReceiver).
// - 뿅망치: 앞쪽 가까운 대상 / 스턴건: 카메라가 보는 방향 사거리 안 첫 대상 / 물풍선: 던진 지점 주변 범위.
// - 스턴건은 우클릭을 누르고 있는 동안만 조준(조준점 + 약간 확대)되고, 그때 좌클릭해야 쏜다(사거리 ToolSO, 15 m).
// 괴물은 충돌 캡슐(반지름 약 0.31 m)이 몸 겉모습보다 훨씬 작으므로, 몸 크기의 트리거(잡기 범위 구)도 맞힘 판정에 넣는다.
public class ToolUser : MonoBehaviour
{
    private PlayerInventory inventory;
    private EscapeManager manager;
    private float readyAt;
    private readonly Collider[] overlap = new Collider[16];
    private const float AimZoom = 0.72f;          // 조준 중 시야각 배율(약간 확대)
    private const float AimZoomSpeed = 8f;
    private float baseFov = -1f;
    private float zoom = 1f;
    private GameObject crosshair;

    // 로컬 플레이어가 스턴건을 조준하는 중인지.
    public static bool Aiming { get; private set; }

    private AudioHandle aimHum;

    public void Init(PlayerInventory owner, EscapeManager escape)
    {
        inventory = owner;
        manager = escape;
    }

    private void Update()
    {
        if (inventory == null || PlayerInventory.Local != inventory) return;
        ItemSO held = inventory.HeldItem;
        bool canAim = held != null && held.IsTool && held.Tool.Kind == ToolKind.StunGun
                      && !PlayerInput.IsGameplaySuppressed && GamePhaseState.Current != GamePhase.Paint;
        Aiming = canAim && PlayerInput.CameraRotateHeld; // 우클릭(시점 회전 버튼)을 누르고 있는 동안
        Camera cam = Camera.main;
        if (cam != null)
        {
            if (baseFov < 0f) baseFov = cam.fieldOfView;
            zoom = Mathf.MoveTowards(zoom, Aiming ? AimZoom : 1f, AimZoomSpeed * Time.deltaTime);
            cam.fieldOfView = baseFov * zoom;
        }
        UpdateAimHum(Aiming);
        if (Aiming && crosshair == null) crosshair = CreateCrosshair();
        if (crosshair != null && crosshair.activeSelf != Aiming) crosshair.SetActive(Aiming);
    }

    // 스턴건을 조준하는 동안 윙 소리 반복(본인 2D, S5).
    private void UpdateAimHum(bool aiming)
    {
        if (aiming && !aimHum.IsPlaying) aimHum = GameAudio.StartLoop(SoundId.StunAimHum, null);
        else if (!aiming && aimHum.IsPlaying) { aimHum.Stop(); aimHum = default; }
    }

    // 도구를 쓰는 순간의 소리(던지기·발사·휘두르기). ToolHitReceiver도 다른 사람 화면에서 같은 소리를 쓴다.
    public static SoundId UseSound(ToolKind kind)
    {
        switch (kind)
        {
            case ToolKind.Hammer: return SoundId.HammerSwing;
            case ToolKind.StunGun: return SoundId.StunFire;
            default: return SoundId.BalloonThrow;
        }
    }

    private void OnDestroy()
    {
        aimHum.Stop();
        if (crosshair != null) Destroy(crosshair);
        Camera cam = Camera.main;
        if (cam != null && baseFov > 0f) cam.fieldOfView = baseFov;
        Aiming = false;
    }

    // 화면 가운데 조준점(작은 점 + 네 갈래 선). 조준하는 동안만 보인다.
    private static GameObject CreateCrosshair()
    {
        var root = new GameObject("StunGunCrosshair", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        var color = new Color(0.55f, 1f, 0.95f, 0.95f);
        void Bar(Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Bar", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(root.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<UnityEngine.UI.Image>();
            img.color = color;
            img.raycastTarget = false;
        }
        Bar(Vector2.zero, new Vector2(5f, 5f));
        Bar(new Vector2(0f, 16f), new Vector2(3f, 12f));
        Bar(new Vector2(0f, -16f), new Vector2(3f, 12f));
        Bar(new Vector2(16f, 0f), new Vector2(12f, 3f));
        Bar(new Vector2(-16f, 0f), new Vector2(12f, 3f));
        root.SetActive(false);
        return root;
    }

    public void Use(int slot, ItemSO item)
    {
        if (Time.time < readyAt || item == null || !item.IsTool) return;
        ToolSO tool = item.Tool;
        if (tool.Kind == ToolKind.StunGun && !Aiming) return; // 스턴건은 우클릭으로 조준한 상태에서만 쏜다
        readyAt = Time.time + tool.CooldownSeconds;
        GameAudio.Play(UseSound(tool.Kind)); // 쓴 사람은 누르는 순간 바로(2D). 다른 사람은 승인된 ToolHit에서 3D로 듣는다(S5)

        Vector3 origin = transform.position + Vector3.up * 1.1f;
        Camera cam = Camera.main;
        Vector3 aim = cam != null ? cam.transform.forward : transform.forward;
        var targets = new List<int>();
        Vector3 point;

        switch (tool.Kind)
        {
            case ToolKind.Hammer:
            {
                Vector3 flat = Vector3.ProjectOnPlane(aim, Vector3.up).normalized;
                if (flat.sqrMagnitude < 0.01f) flat = transform.forward;
                point = origin + flat * (tool.Range * 0.6f);
                CollectInSphere(point, tool.Range * 0.6f, targets, 1);
                break;
            }
            case ToolKind.StunGun:
            {
                Ray ray = cam != null ? cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)) : new Ray(origin, aim);
                float camOffset = cam != null ? Vector3.Distance(cam.transform.position, origin) : 0f;
                point = ray.origin + ray.direction * (tool.Range + camOffset);
                foreach (RaycastHit hit in SortedHits(ray, tool.HitRadius, tool.Range + camOffset))
                {
                    IGameCharacter c = hit.collider.GetComponentInParent<IGameCharacter>();
                    if (c != null && c.gameObject == gameObject) continue; // 자기 몸은 지나친다
                    if (c == null && hit.collider.isTrigger) continue;      // 캐릭터가 아닌 트리거는 무시
                    point = hit.point;
                    if (c != null && c.View != null) targets.Add(c.View.ViewID);
                    break;                                                  // 처음 맞은 것(벽 포함)에서 멈춘다
                }
                break;
            }
            default: // 물풍선
            {
                Ray ray = cam != null ? cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)) : new Ray(origin, aim);
                float camOffset = cam != null ? Vector3.Distance(cam.transform.position, origin) : 0f;
                point = Physics.Raycast(ray, out RaycastHit hit, tool.Range + camOffset, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    ? hit.point : ray.origin + ray.direction * (tool.Range + camOffset);
                // 앞쪽 사거리(10 m) 안에서 땅에 떨어진다: 너무 멀면 당기고, 허공이면 그 아래 땅으로
                Vector3 flat = point - origin;
                flat.y = 0f;
                if (flat.magnitude > tool.Range) point = new Vector3(origin.x, point.y, origin.z) + flat.normalized * tool.Range;
                if (Physics.Raycast(point + Vector3.up * 2f, Vector3.down, out RaycastHit ground, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    && ground.point.y < point.y + 0.1f)
                    point = ground.point;
                CollectInSphere(point, tool.HitRadius, targets, 8);
                break;
            }
        }

        int color = Random.Range(0, 10); // 물풍선 색(팔레트 번호, 받는 쪽에서 범위를 확인)
        manager.Request(EscapeOp.ToolUse, slot, color, point, targets.ToArray());
    }

    private static RaycastHit[] SortedHits(Ray ray, float radius, float distance)
    {
        RaycastHit[] hits = Physics.SphereCastAll(ray, radius, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        return hits;
    }

    private void CollectInSphere(Vector3 center, float radius, List<int> targets, int max)
    {
        int n = Physics.OverlapSphereNonAlloc(center, radius, overlap, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n && targets.Count < max; i++)
        {
            IGameCharacter c = overlap[i].GetComponentInParent<IGameCharacter>();
            if (c == null || c.gameObject == gameObject || c.View == null || targets.Contains(c.View.ViewID)) continue;
            targets.Add(c.View.ViewID);
        }
    }
}
