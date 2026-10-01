using UnityEngine;

// 베이커리 탈출 장치: 마법 오븐 = 작동하는 기계(EscapeVisualPlan.md §5.3).
//   완성 0~3초 : 톱니가 돌기 시작하고 피스톤이 오르내리며 굴뚝에서 연기, 오븐 불빛이 밝아진다
//   3~6초      : 맵의 둥근 오븐 문(HAU_MagicOven_Door)이 옆으로 굴러가며 서서히 열리고, 안이 하얗게 빛난다 → 탈 수 있음
//   탑승        : 쿠키가 경사 발판을 올라 문 앞에서 타면 빛 속으로 빨려 들어간다(BoardingFx.Suck)
//   출발        : 문이 굴러 돌아와 닫히고, 오븐 안에서 큰 빛이 번쩍인다
// 문은 아무나 열 수 없는 일반 물체다(MapSceneBuilder). 모델 이름 규칙은 escape_devices.py(device_bakery)와 같다.
public class OvenSequence : EscapeSequence
{
    private const string DoorName = "HAU_MagicOven_Door";
    private const float WarmupSeconds = 3f;
    private const float DoorSeconds = 3f;
    private const float CloseSeconds = 1.6f;
    private const float FlashAt = 1.8f;
    private const float FlashSeconds = 1f;
    private const float RollDistance = 16f;      // 문이 옆으로 굴러가는 거리(m)
    private const float GearSpeed = 70f;         // 큰 톱니 각속도(도/초)
    private const float GearRatio = 12f / 8f;    // 큰 톱니 12개 : 작은 톱니 8개

    private Transform gearA, gearB, piston, needle, ovenLight, mouth, smokePoint;
    private Quaternion gearAHome, gearBHome, needleHome;
    private Vector3 pistonHome;
    private Transform doorPivot;
    private Vector3 doorPivotHome;
    private Quaternion doorPivotRotation;
    private float doorRadius = 7.6f;
    private Light glow;
    private ParticleSystem smoke, flash;
    private float gearAngle;
    private bool flashed;

    public override float BoardReadySeconds => WarmupSeconds + DoorSeconds + 0.2f;
    public override float DepartureSeconds => FlashAt + FlashSeconds + 0.6f;

    private void Awake()
    {
        gearA = FindDeep(transform, "Gear_A");
        gearB = FindDeep(transform, "Gear_B");
        piston = FindDeep(transform, "Piston_Rod");
        needle = FindDeep(transform, "Gauge_Needle");
        ovenLight = FindDeep(transform, "Oven_Light");
        mouth = FindDeep(transform, "Mouth") ?? transform;
        smokePoint = FindDeep(transform, "Smoke");
        if (gearA != null) gearAHome = gearA.localRotation;
        if (gearB != null) gearBHome = gearB.localRotation;
        if (needle != null) needleHome = needle.localRotation;
        if (piston != null) pistonHome = piston.localPosition;
        SetupDoor();

        glow = new GameObject("OvenGlow").AddComponent<Light>();
        glow.transform.SetParent(transform, false);
        glow.transform.position = mouth.position + transform.forward * 4f;
        glow.type = LightType.Point;
        glow.range = 30f;
        glow.intensity = 0f;
    }

    // 문은 경첩이 원점인 맵 물체다. 굴러가게 하려고 둥근 문 한가운데에 축(피벗)을 만들어 그 아래로 옮긴다.
    private void SetupDoor()
    {
        GameObject found = GameObject.Find(DoorName);
        if (found == null) return;
        Transform door = found.transform;
        var renderer = door.GetComponent<Renderer>();
        Vector3 center = renderer != null ? renderer.bounds.center : door.position;
        if (renderer != null) doorRadius = Mathf.Max(1f, renderer.bounds.extents.y);
        doorPivot = new GameObject("MagicOven_DoorPivot").transform;
        doorPivot.SetParent(door.parent, false);
        doorPivot.position = center;
        doorPivot.rotation = Quaternion.identity;
        door.SetParent(doorPivot, true);
        doorPivotHome = doorPivot.position;
        doorPivotRotation = doorPivot.rotation;
    }

    public override void Tick(EscapeState state, double now)
    {
        float c = state != null ? Since(state.CompletedAt, now) : -1f;
        float d = state != null ? Since(state.DepartedAt, now) : -1f;

        // 기계: 완성되면 3초에 걸쳐 힘이 오른다
        float power = c < 0f ? 0f : Mathf.Clamp01(c / WarmupSeconds);
        gearAngle += GearSpeed * power * Time.deltaTime;
        if (gearA != null) gearA.localRotation = gearAHome * Quaternion.Euler(0f, 0f, gearAngle);
        if (gearB != null) gearB.localRotation = gearBHome * Quaternion.Euler(0f, 0f, -gearAngle * GearRatio + 11f);
        if (piston != null) piston.localPosition = pistonHome + Vector3.up * (power * 0.9f * (0.5f + 0.5f * Mathf.Sin(Time.time * 7f)));
        if (needle != null) needle.localRotation = needleHome * Quaternion.Euler(0f, 0f, Mathf.Lerp(70f, -60f, power) + power * 6f * Mathf.Sin(Time.time * 23f));
        UpdateSmoke(power);

        // 문: 3초 뒤부터 3초 동안 옆으로 굴러 열리고, 출발하면 굴러 돌아와 닫힌다
        float open = c < WarmupSeconds ? 0f : Ease((c - WarmupSeconds) / DoorSeconds);
        if (d >= 0f) open *= 1f - Ease(d / CloseSeconds);
        if (doorPivot != null)
        {
            float roll = RollDistance * open;
            doorPivot.position = doorPivotHome + transform.right * roll;
            doorPivot.rotation = Quaternion.AngleAxis(-roll / doorRadius * Mathf.Rad2Deg, transform.forward) * doorPivotRotation;
        }

        // 빛: 불이 밝아짐 → 문이 열리며 하얀 빛 → 출발하면 크게 번쩍
        if (ovenLight != null)
        {
            bool lit = open > 0.02f;
            if (ovenLight.gameObject.activeSelf != lit) ovenLight.gameObject.SetActive(lit);
            if (lit) ovenLight.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, open);
        }
        float burst = d >= FlashAt ? Mathf.Sin(Mathf.Clamp01((d - FlashAt) / FlashSeconds) * Mathf.PI) : 0f;
        glow.color = Color.Lerp(new Color(1f, 0.55f, 0.2f), new Color(1f, 0.97f, 0.9f), Mathf.Max(open, burst));
        glow.intensity = power * 2.5f + open * 4f + burst * 14f;
        glow.range = 30f + burst * 25f;
        if (d >= FlashAt && !flashed) { flashed = true; Flash(); }
        if (d < 0f) flashed = false;
    }

    public override void OnCookieBoarded(Vector3 from) => BoardingFx.Play(from, mouth.position, BoardingFx.Style.Suck);

    private void UpdateSmoke(float power)
    {
        if (smokePoint == null) return;
        if (power > 0.2f && smoke == null) smoke = CreateSmoke();
        if (smoke == null) return;
        var emission = smoke.emission;
        emission.rateOverTime = 14f * power;
    }

    // 굴뚝 연기: 위로 천천히 오르며 커지는 퍼프.
    private ParticleSystem CreateSmoke()
    {
        var go = new GameObject("MachineSmoke");
        go.transform.SetParent(smokePoint, false);
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 2.5f;
        main.startSpeed = 2.2f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
        main.startColor = new Color(0.7f, 0.66f, 0.75f, 0.55f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.4f;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 2.2f));
        var color = ps.colorOverLifetime;
        color.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0.55f, 0f), new GradientAlphaKey(0f, 1f) });
        color.color = grad;
        go.GetComponent<ParticleSystemRenderer>().material = EscapeVisuals.SoftParticleMaterial();
        ps.Play();
        return ps;
    }

    // 출발 순간 오븐 앞에서 하얀 빛 가루가 터진다.
    private void Flash()
    {
        if (flash == null)
        {
            var go = new GameObject("OvenFlash");
            go.transform.SetParent(transform, false);
            go.transform.position = mouth.position + transform.forward * 3f;
            flash = go.AddComponent<ParticleSystem>();
            flash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = flash.main;
            main.playOnAwake = false;
            main.startLifetime = 1.2f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 11f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startColor = new Color(1f, 0.97f, 0.85f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 150;
            var emission = flash.emission;
            emission.rateOverTime = 0f;
            var shape = flash.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 3f;
            var color = flash.colorOverLifetime;
            color.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.8f, 0.5f), 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = grad;
            go.GetComponent<ParticleSystemRenderer>().material = EscapeVisuals.SoftParticleMaterial();
        }
        flash.Emit(120);
    }
}
