using UnityEngine;

// 소음폭탄 비상벨(Request1009Plan.md §25.5 K): 괴물 화면에 터진 건물 위치를 5초 동안 하늘로 솟는 빨간 빛기둥으로 보여 준다(벽 너머로도 — 투시).
public class AlarmBeacon : MonoBehaviour
{
    public const float ShowSeconds = 5f;
    private float until;

    public static void Show(Vector3 at)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(go.GetComponent<Collider>());
        go.name = "AlarmBeacon";
        go.transform.position = at + Vector3.up * 15f;
        go.transform.localScale = new Vector3(1.2f, 15f, 1.2f);
        EscapeVisuals.Tint(go, new Color(1f, 0.15f, 0.1f), 3f);
        var xray = go.AddComponent<XRayMarker>();
        xray.Init(new Color(1f, 0.2f, 0.15f));
        xray.SetVisible(true);
        go.AddComponent<AlarmBeacon>().until = Time.time + ShowSeconds;
        GameAudio.PlayAt(SoundId.UiToastAlert, at);
    }

    private void Update()
    {
        float left = until - Time.time;
        if (left <= 0f) { Destroy(gameObject); return; }
        transform.Rotate(0f, 120f * Time.deltaTime, 0f);
    }
}
