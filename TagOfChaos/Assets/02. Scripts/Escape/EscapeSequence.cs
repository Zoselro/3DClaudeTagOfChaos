using UnityEngine;

// 맵별 탈출 장치 연출의 바탕(EscapeVisualPlan.md §4). 시동·문 열림·케이크 부서짐·출발 같은 모든 연출은
// EscapeState의 CompletedAt(완성 시각)과 DepartedAt(출발 시각), 그리고 공통 시계(PhotonNetwork.Time)의 차이로만 계산한다.
// 그래서 모든 화면이 같은 모습이고, 늦게 들어온 사람도 바로 맞는 장면을 본다. 네트워크 메시지를 따로 보내지 않는다.
public abstract class EscapeSequence : MonoBehaviour
{
    private Transform boardPoint;

    // 완성된 뒤 이 시간이 지나야 탈 수 있다(연출이 탈 수 있는 단계에 도착할 때까지). 방장도 이 값으로 탑승을 승인한다.
    public abstract float BoardReadySeconds { get; }

    // 출발 연출 길이. 이 시간 동안은 게임 끝 판정을 미뤄 연출을 끝까지 보여준다.
    public abstract float DepartureSeconds { get; }

    // 쿠키가 타는 곳(모델의 "Board" 빈 오브젝트). 없으면 장치 중심.
    public Transform BoardPoint
    {
        get
        {
            if (boardPoint == null) boardPoint = FindDeep(transform, "Board") ?? transform;
            return boardPoint;
        }
    }

    // 매 프레임(모든 클라이언트). state는 null일 수 있다(판 시작 전).
    public abstract void Tick(EscapeState state, double now);

    // 쿠키 한 명이 탔을 때(모든 클라이언트). from = 그 쿠키가 서 있던 곳.
    public virtual void OnCookieBoarded(Vector3 from) => BoardingFx.Play(from, BoardPoint.position, BoardingFx.Style.Hop);

    // 기준 시각에서 지난 시간(초). 아직 그 일이 일어나지 않았으면 음수.
    protected static float Since(double at, double now) => at > 0 ? (float)(now - at) : -1f;

    // 이름으로 자식을 깊이 찾는다(움직이는 부품 아래에 있는 칸·좌석 자리도 찾게).
    public static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    protected static float Ease(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
}
