using UnityEngine;
using UnityEngine.UI;

// 결과 화면의 3D 쿠키 유리병(EscapeVisualPlan.md §3.3). 화면 밖 먼 곳에 유리병 모델(EscapeCatalogSO.JarModel)과
// 잡은 수만큼의 작은 쿠키(JarCookieModel)를 쌓아 두고, 전용 카메라가 RenderTexture로 찍어 이 RawImage에 보여 준다.
// 병은 천천히 돈다. 이 UI가 사라지면 무대·카메라·텍스처도 함께 지운다.
[RequireComponent(typeof(RawImage))]
public class MonsterJarStage : MonoBehaviour
{
    private const int MaxCookies = 12;
    private const int PerLayer = 3;
    private const float LayerHeight = 0.17f;
    private const float CookieScale = 1.6f;     // 병 크기에 맞춰 작은 쿠키를 조금 키운다
    private const float StageSpacing = 30f;
    private static readonly Vector3 StageOrigin = new Vector3(5000f, -5000f, 5000f); // 맵에서 아주 먼 곳
    private const float SpinSpeed = 25f;                                              // 도/초

    private static int stageCount;
    private GameObject stage;
    private Transform jar;
    private RenderTexture texture;

    public static bool Available => EscapeCatalogSO.Current != null && EscapeCatalogSO.Current.JarModel != null;

    public void Init(int catches, Vector2 size)
    {
        EscapeCatalogSO catalog = EscapeCatalogSO.Current;
        stage = new GameObject("MonsterJarStage");
        stage.transform.position = StageOrigin + Vector3.right * StageSpacing * stageCount++;

        jar = Instantiate(catalog.JarModel, stage.transform, false).transform;
        jar.localPosition = Vector3.zero;
        foreach (Collider c in jar.GetComponentsInChildren<Collider>(true)) Destroy(c);
        StackCookies(catalog, Mathf.Min(catches, MaxCookies));

        var lightGo = new GameObject("JarLight");
        lightGo.transform.SetParent(stage.transform, false);
        lightGo.transform.localPosition = new Vector3(-1.2f, 2.2f, -2.2f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 8f;
        light.intensity = 2.2f;
        light.color = new Color(1f, 0.93f, 0.85f);

        texture = new RenderTexture(Mathf.RoundToInt(size.x * 2f), Mathf.RoundToInt(size.y * 2f), 24) { name = "MonsterJar" };
        var camGo = new GameObject("JarCamera");
        camGo.transform.SetParent(stage.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 0.75f, -2.9f);
        camGo.transform.LookAt(stage.transform.position + Vector3.up * 0.55f);
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 28f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 8f;                       // 무대만 찍힌다(맵은 아주 멀다)
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.targetTexture = texture;
        cam.depth = -50f;

        RawImage image = GetComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
    }

    // 작은 쿠키를 병 바닥(CookieStack)부터 한 층에 3개씩 눕혀 쌓는다.
    private void StackCookies(EscapeCatalogSO catalog, int count)
    {
        Transform stack = jar.Find("CookieStack");
        Vector3 basePos = stack != null ? stack.localPosition : Vector3.up * 0.05f;
        var rnd = new System.Random(count * 7 + 3);
        for (int i = 0; i < count; i++)
        {
            int layer = i / PerLayer, k = i % PerLayer;
            float angle = (k * 120f + layer * 50f) * Mathf.Deg2Rad;
            GameObject cookie = catalog.JarCookieModel != null
                ? Instantiate(catalog.JarCookieModel, jar, false)
                : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cookie.transform.SetParent(jar, false);
            cookie.transform.localPosition = basePos + new Vector3(Mathf.Cos(angle) * 0.2f, 0.04f + layer * LayerHeight, Mathf.Sin(angle) * 0.2f);
            cookie.transform.localRotation = Quaternion.Euler(-80f + (float)rnd.NextDouble() * 20f, (float)rnd.NextDouble() * 360f, 0f);
            cookie.transform.localScale = Vector3.one * (catalog.JarCookieModel != null ? CookieScale : 0.15f);
            foreach (Collider c in cookie.GetComponentsInChildren<Collider>(true)) Destroy(c);
        }
    }

    private void Update()
    {
        if (jar != null) jar.Rotate(0f, SpinSpeed * Time.unscaledDeltaTime, 0f, Space.World);
    }

    private void OnDestroy()
    {
        if (stage != null) Destroy(stage);
        if (texture != null) { texture.Release(); Destroy(texture); }
    }
}
