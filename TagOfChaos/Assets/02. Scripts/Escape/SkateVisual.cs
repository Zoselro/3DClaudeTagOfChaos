using UnityEngine;

// 인라인스케이트를 쓰는 동안 발밑에 보이는 바퀴(Request1009Plan.md §25.5 I). 모든 화면에서 HideOrSeekPlayer.SpeedBoost를 보고 켠다.
// 모델이 생기면 발 뼈에 붙이는 모델로 바꾼다.
public class SkateVisual : MonoBehaviour
{
    private HideOrSeekPlayer cookie;
    private GameObject skates;

    public void Init(HideOrSeekPlayer owner) => cookie = owner;

    private void Update()
    {
        bool on = cookie != null && cookie.SpeedBoost > 1f;
        if (on && skates == null) skates = Build();
        if (skates != null && skates.activeSelf != on) skates.SetActive(on);
    }

    private GameObject Build()
    {
        var root = new GameObject("Skates");
        root.transform.SetParent(transform, false);
        for (int side = -1; side <= 1; side += 2)
        {
            var boot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(boot.GetComponent<Collider>());
            boot.transform.SetParent(root.transform, false);
            boot.transform.localPosition = new Vector3(side * 0.18f, 0.08f, 0.05f);
            boot.transform.localScale = new Vector3(0.16f, 0.1f, 0.42f);
            EscapeVisuals.Tint(boot, new Color(0.95f, 0.3f, 0.55f), 0.4f);
            for (int w = 0; w < 3; w++)
            {
                var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(wheel.GetComponent<Collider>());
                wheel.transform.SetParent(root.transform, false);
                wheel.transform.localPosition = new Vector3(side * 0.18f, 0.03f, -0.1f + w * 0.15f);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                wheel.transform.localScale = new Vector3(0.07f, 0.02f, 0.07f);
                EscapeVisuals.Tint(wheel, new Color(1f, 0.9f, 0.3f), 0.6f);
            }
        }
        root.SetActive(false);
        return root;
    }
}
