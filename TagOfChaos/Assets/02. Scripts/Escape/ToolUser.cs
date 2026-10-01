using System.Collections.Generic;
using UnityEngine;

// 손에 든 도구 쓰기(EscapePlan.md §1.8). 맞힌 대상은 쓰는 사람 화면에서 고르고, 방장이 횟수·쿨다운을 확인해
// 모두에게 명중(ToolHit)을 알린다 — 기절·떨어뜨리기·색칠은 맞은 사람 본인이 처리한다(ToolHitReceiver).
// - 뿅망치: 앞쪽 가까운 대상 / 스턴건: 카메라가 보는 방향 사거리 안 첫 대상 / 물풍선: 던진 지점 주변 범위.
// 괴물은 충돌 캡슐(반지름 약 0.31 m)이 몸 겉모습보다 훨씬 작으므로, 몸 크기의 트리거(잡기 범위 구)도 맞힘 판정에 넣는다.
public class ToolUser : MonoBehaviour
{
    private PlayerInventory inventory;
    private EscapeManager manager;
    private float readyAt;
    private readonly Collider[] overlap = new Collider[16];

    public void Init(PlayerInventory owner, EscapeManager escape)
    {
        inventory = owner;
        manager = escape;
    }

    public void Use(int slot, ItemSO item)
    {
        if (Time.time < readyAt || item == null || !item.IsTool) return;
        ToolSO tool = item.Tool;
        readyAt = Time.time + tool.CooldownSeconds;

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
