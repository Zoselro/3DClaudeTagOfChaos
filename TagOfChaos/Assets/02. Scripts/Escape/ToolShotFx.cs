using System;
using UnityEngine;

// 도구가 날아가는 모습(모든 화면): 물풍선은 던진 사람 손에서 떨어질 곳까지 포물선으로 날아가고, 떨어진 순간 명중 처리
// (물보라·기절·색칠)를 한다. 스턴건은 쏜 사람에게서 맞은 곳까지 잠깐 빛줄기를 그린다.
public class ToolShotFx : MonoBehaviour
{
    private const float BalloonSpeed = 16f;     // m/s (10 m ≈ 0.6초)
    private const float BalloonArc = 0.35f;     // 거리 대비 포물선 높이
    private const float BeamSeconds = 0.18f;

    private Vector3 from, to;
    private float duration, age;
    private Action onArrive;
    private LineRenderer beam;

    public static void ThrowBalloon(ItemSO item, Vector3 from, Vector3 to, Action onArrive)
    {
        var go = new GameObject("WaterBalloonShot");
        var fx = go.AddComponent<ToolShotFx>();
        fx.from = from;
        fx.to = to;
        fx.duration = Mathf.Clamp(Vector3.Distance(from, to) / BalloonSpeed, 0.15f, 1f);
        fx.onArrive = onArrive;
        GameObject model = EscapeVisuals.CreateItemModel(item, go.transform);
        if (model != null) model.transform.localPosition = Vector3.zero;
        go.transform.position = from;
    }

    public static void Beam(Vector3 from, Vector3 to, Color color)
    {
        var go = new GameObject("StunBeam");
        var fx = go.AddComponent<ToolShotFx>();
        fx.duration = BeamSeconds;
        fx.beam = go.AddComponent<LineRenderer>();
        fx.beam.positionCount = 2;
        fx.beam.SetPosition(0, from);
        fx.beam.SetPosition(1, to);
        fx.beam.widthMultiplier = 0.12f;
        fx.beam.material = EscapeVisuals.SoftParticleMaterial();
        fx.beam.startColor = fx.beam.endColor = color;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / duration);
        if (beam != null)
        {
            Color c = beam.startColor;
            c.a = 1f - t;
            beam.startColor = beam.endColor = c;
        }
        else
        {
            Vector3 p = Vector3.Lerp(from, to, t);
            p.y += Mathf.Sin(t * Mathf.PI) * Vector3.Distance(from, to) * BalloonArc;
            transform.position = p;
            transform.Rotate(0f, 0f, 540f * Time.deltaTime, Space.Self);
        }
        if (t < 1f) return;
        onArrive?.Invoke();
        onArrive = null;
        Destroy(gameObject);
    }
}
