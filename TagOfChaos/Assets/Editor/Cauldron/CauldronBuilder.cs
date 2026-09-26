using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// 가마솥 프리팹 빌더(Plan.md/Cauldron.md). 재질 추출, 반복(Idle)·풍덩(Splash) 클립, Animator Controller, 프리팹(0.75배,
// 충돌체, 숯불 빛, 풍덩 트리거)을 만들고 결과를 스스로 검증한다. 과자집 빌더(WitchCookieHouseBuilder)와 같은 방식이다.
// 모든 연출은 모델에 들어 있는 메시만 움직여 표현한다(파티클 없음, 결정 D3).
public static class CauldronBuilder
{
    private const string MenuBuild = "Tools/TagOfChaos/Build Cauldron";
    private const string MenuVerify = "Tools/TagOfChaos/Verify Cauldron";
    private const string LogTag = "[Cauldron]";

    private const string AnimFolder = CauldronImportPostprocessor.Root + "/Animations";
    private const string PrefabPath = "Assets/04. Prefabs/Environment/Cauldron.prefab";
    private const string ControllerPath = AnimFolder + "/Cauldron.controller";
    private const string IdleClipName = "Cauldron_Idle";
    private const string SplashClipName = "Cauldron_Splash";

    // 크기(결정 D1: 테두리 1.93 m → 약 1.45 m로 쿠키가 점프해 들어갈 수 있게)
    private const float ModelScale = 0.75f;

    // 반복 클립 길이. 모든 반복 주기가 이 값을 나눠 떨어지게 골라 이음새가 없다.
    private const float LoopLength = 2.4f;
    private const float SplashLength = 1.0f;
    private const float KeysPerSecond = 30f;

    // 모델 로컬 단위(축소 전) 치수 — 메시 실측(솥 안쪽 반지름 약 1.0 m, 바깥 약 1.25 m, 안쪽 바닥 0.56 m, 수면 1.58 m)
    private const int WallSegments = 16;
    private const float WallRadius = 1.12f;
    private const float WallThickness = 0.25f;
    private const float WallBottom = 0.2f;
    private const float WallTop = 1.93f;
    private const float InnerFloorTop = 0.72f;
    private const float SplashZoneCenterY = 1.45f;

    private static readonly float[] FlamePeriods = { 0.6f, 0.8f, 0.48f, 1.2f, 0.4f, 0.6f, 0.8f, 0.48f, 0.6f, 0.8f, 1.2f, 0.48f, 0.6f };

    [MenuItem(MenuBuild)]
    public static void Build()
    {
        EnsureFolder(CauldronImportPostprocessor.MaterialFolder);
        EnsureFolder(AnimFolder);
        ExtractMaterials();

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(CauldronImportPostprocessor.ModelPath);
        if (model == null)
        {
            Debug.LogError($"{LogTag} Model not found: {CauldronImportPostprocessor.ModelPath}");
            return;
        }

        AnimationClip idle = WriteIdleClip(model.transform);
        AnimationClip splash = WriteSplashClip(model.transform);
        AnimatorController controller = WriteController(idle, splash);

        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("Cauldron");
            SceneManager.MoveGameObjectToScene(root, preview);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
            instance.name = "Model";
            instance.transform.SetParent(root.transform, false);
            instance.transform.localScale = Vector3.one * ModelScale;

            var animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms; // 보이지 않으면 연출을 멈춘다(연출 전용)

            AddColliders(instance.transform);
            AddSplashTrigger(instance.transform, animator);
            AddFireLight(instance.transform);
            MarkStatic(instance.transform, "Cauldron_Body", "Cauldron_Runes", "Fire_Logs", "Colliders");

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
            if (!saved) Debug.LogError($"{LogTag} Failed to save prefab: {PrefabPath}");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }

        AssetDatabase.SaveAssets();
        Verify();
    }

    // ---------------- materials ----------------

    // FBX에 들어 있는 재질을 .mat으로 꺼낸다(이미 있으면 건너뜀). 꺼내면 임포터가 자동으로 리맵한다.
    private static void ExtractMaterials()
    {
        bool extracted = false;
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(CauldronImportPostprocessor.ModelPath))
        {
            if (!(asset is Material material)) continue;
            string target = $"{CauldronImportPostprocessor.MaterialFolder}/{material.name}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(target) != null) continue;
            string error = AssetDatabase.ExtractAsset(material, target);
            if (!string.IsNullOrEmpty(error)) Debug.LogError($"{LogTag} Extract {material.name}: {error}");
            else extracted = true;
        }
        if (extracted)
        {
            AssetDatabase.WriteImportSettingsIfDirty(CauldronImportPostprocessor.ModelPath);
            AssetDatabase.ImportAsset(CauldronImportPostprocessor.ModelPath, ImportAssetOptions.ForceUpdate);
        }
    }

    // ---------------- clips ----------------

    private static AnimationClip WriteIdleClip(Transform model)
    {
        AnimationClip clip = LoadOrCreateClip(IdleClipName);
        clip.ClearCurves();
        clip.frameRate = KeysPerSecond;

        // A1 불꽃: 밑동(피벗) 기준 세로로 늘었다 줄고, 가로는 반대로 살짝 좁아지며, 앞뒤·좌우로 흔들린다. 불꽃마다 주기·위상이 다르다.
        for (int i = 0; i < 13; i++)
        {
            string path = $"Fire_Flame_{i + 1:00}";
            float period = FlamePeriods[i];
            float phase = i * 1.37f;
            Func<float, float> wave = t => Mathf.Sin(Tau * t / period + phase);
            SetSampled(clip, path, typeof(Transform), "m_LocalScale.y", LoopLength,
                t => 1f + 0.25f * wave(t) + 0.08f * Mathf.Sin(Tau * 2f * t / period + phase * 2f));
            SetSampled(clip, path, typeof(Transform), "m_LocalScale.x", LoopLength, t => 1f - 0.1f * wave(t));
            SetSampled(clip, path, typeof(Transform), "m_LocalScale.z", LoopLength, t => 1f - 0.1f * wave(t));
            SetSampled(clip, path, typeof(Transform), "localEulerAnglesRaw.x", LoopLength, t => 6f * Mathf.Sin(Tau * t / period + phase + 1.3f));
            SetSampled(clip, path, typeof(Transform), "localEulerAnglesRaw.y", LoopLength, t => 0f);
            SetSampled(clip, path, typeof(Transform), "localEulerAnglesRaw.z", LoopLength, t => 6f * Mathf.Cos(Tau * t / period + phase));
        }

        // A2 숯불 일렁임: 발광 세기 맥동 + 솥 밑 빛 깜빡임
        Material ember = AssetDatabase.LoadAssetAtPath<Material>($"{CauldronImportPostprocessor.MaterialFolder}/M_Ember.mat");
        Color emberBase = ember != null && ember.HasProperty("_EmissionColor") ? ember.GetColor("_EmissionColor") : new Color(4f, 1.4f, 0.2f);
        Func<float, float> glow = t => 1f + 0.15f * Mathf.Sin(Tau * t / 1.2f) + 0.08f * Mathf.Sin(Tau * t / 0.4f + 1f);
        SetSampled(clip, "Fire_Coals", typeof(MeshRenderer), "material._EmissionColor.r", LoopLength, t => emberBase.r * glow(t));
        SetSampled(clip, "Fire_Coals", typeof(MeshRenderer), "material._EmissionColor.g", LoopLength, t => emberBase.g * glow(t));
        SetSampled(clip, "Fire_Coals", typeof(MeshRenderer), "material._EmissionColor.b", LoopLength, t => emberBase.b * glow(t));
        SetSampled(clip, FireLightName, typeof(Light), "m_Intensity", LoopLength,
            t => 1.2f + 0.25f * Mathf.Sin(Tau * t / 0.48f) + 0.15f * Mathf.Sin(Tau * t / 0.8f + 2f));

        // A3 연기 모락모락: 연기마다 한 주기 동안 아래에서 생겨(크기 0) 떠오르며 커졌다가 위에서 사라진다(크기 0). 위상을 나눠 끊김 없이 이어진다.
        for (int i = 0; i < 6; i++)
        {
            Transform puff = model.Find($"Smoke_Puff_{i + 1:00}");
            if (puff == null) continue;
            Vector3 p0 = puff.localPosition;
            Vector3 s0 = puff.localScale;
            float offset = i / 6f;
            float swirl = i * 1.1f;
            Func<float, float> u = t => Frac(t / LoopLength + offset);
            Func<float, float> size = t => Mathf.Pow(Mathf.Sin(Mathf.PI * u(t)), 0.8f);
            string path = puff.name;
            SetSampled(clip, path, typeof(Transform), "m_LocalPosition.x", LoopLength, t => p0.x + 0.08f * Mathf.Sin(Tau * u(t) + swirl), linear: true);
            SetSampled(clip, path, typeof(Transform), "m_LocalPosition.y", LoopLength, t => p0.y - 0.35f + 0.9f * u(t), linear: true);
            SetSampled(clip, path, typeof(Transform), "m_LocalPosition.z", LoopLength, t => p0.z + 0.08f * Mathf.Cos(Tau * u(t) + swirl), linear: true);
            SetSampled(clip, path, typeof(Transform), "m_LocalScale.x", LoopLength, t => s0.x * size(t), linear: true);
            SetSampled(clip, path, typeof(Transform), "m_LocalScale.y", LoopLength, t => s0.y * size(t), linear: true);
            SetSampled(clip, path, typeof(Transform), "m_LocalScale.z", LoopLength, t => s0.z * size(t), linear: true);
        }

        // A4 수면 부글부글: 잔물결 두 모양을 번갈아 섞고, 거품은 커졌다가 톡 터진다(크기 0).
        SetSampled(clip, "Liquid_Surface", typeof(SkinnedMeshRenderer), "blendShape.Idle_A", LoopLength, t => 30f + 30f * Mathf.Sin(Tau * t / LoopLength));
        SetSampled(clip, "Liquid_Surface", typeof(SkinnedMeshRenderer), "blendShape.Idle_B", LoopLength, t => 30f - 30f * Mathf.Sin(Tau * t / LoopLength));
        foreach (string shape in SplashShapes)
            SetConstant(clip, "Liquid_Surface", typeof(SkinnedMeshRenderer), $"blendShape.{shape}", LoopLength, 0f);
        for (int i = 0; i < 3; i++)
        {
            Transform bubble = model.Find($"Liquid_Bubble_{i + 1:00}");
            if (bubble == null) continue;
            Vector3 b0 = bubble.localPosition;
            float offset = i / 3f;
            Func<float, float> u = t => Frac(t / 1.2f + offset);
            Func<float, float> size = t => u(t) < 0.8f ? Mathf.SmoothStep(0f, 1f, u(t) / 0.8f) : Mathf.Max(0f, 1f - (u(t) - 0.8f) / 0.06f);
            SetSampled(clip, bubble.name, typeof(Transform), "m_LocalPosition.y", LoopLength, t => b0.y + 0.03f * u(t), linear: true);
            foreach (string axis in Axes)
                SetSampled(clip, bubble.name, typeof(Transform), $"m_LocalScale.{axis}", LoopLength, t => size(t), linear: true);
        }

        // 풍덩 오브젝트는 평소에 숨긴다(크기 0). 풍덩 레이어가 재생될 때만 덮어써서 보인다.
        foreach (Transform part in SplashParts(model))
        {
            foreach (string axis in Axes) SetConstant(clip, part.name, typeof(Transform), $"m_LocalScale.{axis}", LoopLength, 0f);
            SetConstant(clip, part.name, typeof(Transform), "m_LocalPosition.x", LoopLength, part.localPosition.x);
            SetConstant(clip, part.name, typeof(Transform), "m_LocalPosition.y", LoopLength, part.localPosition.y);
            SetConstant(clip, part.name, typeof(Transform), "m_LocalPosition.z", LoopLength, part.localPosition.z);
        }

        SetLoop(clip, true);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    // A5 풍덩(1초): 수면이 움푹 꺼지고 → 왕관 물보라가 솟았다 가라앉고 → 물방울이 포물선으로 튀었다 떨어지며 → 파문이 네 방향으로 퍼지며 잦아든다.
    private static AnimationClip WriteSplashClip(Transform model)
    {
        AnimationClip clip = LoadOrCreateClip(SplashClipName);
        clip.ClearCurves();
        clip.frameRate = 60f;
        const float len = SplashLength;

        SetSampled(clip, "Liquid_Surface", typeof(SkinnedMeshRenderer), "blendShape.Splash_Dip", len,
            t => t < 0.1f ? 100f * Mathf.SmoothStep(0f, 1f, t / 0.1f) : 100f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.1f) / 0.25f))), 60f, true);
        for (int k = 0; k < 4; k++)
        {
            int ring = k;
            SetSampled(clip, "Liquid_Surface", typeof(SkinnedMeshRenderer), $"blendShape.{RippleShapes[ring]}", len, t =>
            {
                if (t < 0.2f) return 0f;
                float decay = Mathf.Exp(-(t - 0.2f) * 3f) * (1f - Mathf.SmoothStep(0.85f, 1f, t));
                return 100f * decay * Mathf.Max(0f, Mathf.Cos(Tau * (t - 0.2f) / 0.4f - ring * Mathf.PI * 0.5f));
            }, 60f, true);
        }

        Transform crown = model.Find("Splash_Crown");
        if (crown != null)
        {
            Func<float, float> width = t => t < 0.05f ? 0f : t < 0.25f ? Mathf.Lerp(0.5f, 1f, (t - 0.05f) / 0.2f) : t < 0.7f ? Mathf.Lerp(1f, 1.25f, (t - 0.25f) / 0.45f) : 0f;
            Func<float, float> height = t => t < 0.05f ? 0f : t < 0.25f ? Mathf.Lerp(0.2f, 1.1f, (t - 0.05f) / 0.2f) : t < 0.7f ? Mathf.Lerp(1.1f, 0f, (t - 0.25f) / 0.45f) : 0f;
            SetSampled(clip, crown.name, typeof(Transform), "m_LocalScale.x", len, width, 60f, true);
            SetSampled(clip, crown.name, typeof(Transform), "m_LocalScale.y", len, height, 60f, true);
            SetSampled(clip, crown.name, typeof(Transform), "m_LocalScale.z", len, width, 60f, true);
        }

        int index = 0;
        foreach (Transform drop in SplashParts(model))
        {
            if (drop == crown) continue;
            Vector3 b = drop.localPosition;
            Vector3 dir = new Vector3(b.x, 0f, b.z).normalized;
            float start = 0.08f + index * 0.01f;
            index++;
            Func<float, float> tau = t => Mathf.Clamp(t - start, 0f, 0.8f);
            Func<float, float> active = t => t >= start && t <= start + 0.8f ? 1f : 0f;
            Func<float, float> size = t =>
            {
                if (active(t) == 0f) return 0f;
                float x = t - start;
                return x < 0.05f ? x / 0.05f : x < 0.65f ? 1f : Mathf.Max(0f, 1f - (x - 0.65f) / 0.13f);
            };
            SetSampled(clip, drop.name, typeof(Transform), "m_LocalPosition.x", len, t => b.x + dir.x * 0.9f * tau(t) / 0.8f, 60f, true);
            SetSampled(clip, drop.name, typeof(Transform), "m_LocalPosition.y", len, t => b.y + 2.4f * tau(t) - 3f * tau(t) * tau(t), 60f, true);
            SetSampled(clip, drop.name, typeof(Transform), "m_LocalPosition.z", len, t => b.z + dir.z * 0.9f * tau(t) / 0.8f, 60f, true);
            foreach (string axis in Axes)
                SetSampled(clip, drop.name, typeof(Transform), $"m_LocalScale.{axis}", len, size, 60f, true);
        }

        SetLoop(clip, false);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    // ---------------- controller ----------------

    // Base: 반복(Idle). Splash(덮어쓰기, 가중치 1): 평소 빈 상태 → Splash 트리거로 풍덩 1회 → 빈 상태.
    // 빈 상태는 모션이 없고 Write Defaults를 끄므로 Base 값을 그대로 둔다.
    private static AnimatorController WriteController(AnimationClip idle, AnimationClip splash)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null) AssetDatabase.DeleteAsset(ControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter(CauldronSplash.SplashTriggerParameter, AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine baseMachine = controller.layers[0].stateMachine;
        AnimatorState idleState = baseMachine.AddState("Idle");
        idleState.motion = idle;
        idleState.writeDefaultValues = false;
        baseMachine.defaultState = idleState;

        controller.AddLayer("Splash");
        AnimatorControllerLayer[] layers = controller.layers;
        layers[1].defaultWeight = 1f;
        layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
        controller.layers = layers;

        AnimatorStateMachine splashMachine = controller.layers[1].stateMachine;
        AnimatorState empty = splashMachine.AddState("Empty");
        empty.writeDefaultValues = false;
        AnimatorState splashState = splashMachine.AddState("Splash");
        splashState.motion = splash;
        splashState.writeDefaultValues = false;
        splashMachine.defaultState = empty;

        AnimatorStateTransition enter = splashMachine.AddAnyStateTransition(splashState);
        enter.hasExitTime = false;
        enter.duration = 0f;
        enter.canTransitionToSelf = true;
        enter.AddCondition(AnimatorConditionMode.If, 0f, CauldronSplash.SplashTriggerParameter);

        AnimatorStateTransition exit = splashState.AddTransition(empty);
        exit.hasExitTime = true;
        exit.exitTime = 1f;
        exit.duration = 0f;

        EditorUtility.SetDirty(controller);
        return controller;
    }

    // ---------------- prefab parts ----------------

    // 속이 빈 통: 원형으로 둘러싼 상자 16개(쿠키가 안으로 뛰어들 수 있음) + 솥 안 바닥 + 장작(볼록 메시).
    private static void AddColliders(Transform model)
    {
        var group = new GameObject("Colliders").transform;
        group.SetParent(model, false);

        float height = WallTop - WallBottom;
        float width = 2f * Mathf.PI * WallRadius / WallSegments + 0.08f; // 조각 사이 틈이 없도록 조금 겹친다
        for (int i = 0; i < WallSegments; i++)
        {
            float angle = 360f * i / WallSegments;
            var wall = new GameObject($"Wall_{i:00}").transform;
            wall.SetParent(group, false);
            wall.localRotation = Quaternion.Euler(0f, angle, 0f);
            wall.localPosition = wall.localRotation * new Vector3(0f, WallBottom + height * 0.5f, WallRadius);
            wall.gameObject.AddComponent<BoxCollider>().size = new Vector3(width, height, WallThickness);
        }

        var floor = new GameObject("InnerFloor").transform;
        floor.SetParent(group, false);
        floor.localPosition = new Vector3(0f, InnerFloorTop - 0.1f, 0f);
        floor.gameObject.AddComponent<BoxCollider>().size = new Vector3(1.5f, 0.2f, 1.5f);

        Transform logs = model.Find("Fire_Logs");
        var filter = logs != null ? logs.GetComponent<MeshFilter>() : null;
        if (filter != null && filter.sharedMesh != null)
        {
            var logCollider = logs.gameObject.AddComponent<MeshCollider>();
            logCollider.sharedMesh = filter.sharedMesh;
            logCollider.convex = true;
        }
    }

    private static void AddSplashTrigger(Transform model, Animator animator)
    {
        var trigger = new GameObject("SplashTrigger");
        trigger.transform.SetParent(model, false);
        trigger.transform.localPosition = new Vector3(0f, SplashZoneCenterY, 0f);
        var box = trigger.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(1.5f, 0.5f, 1.5f);

        var splash = trigger.AddComponent<CauldronSplash>();
        var so = new SerializedObject(splash);
        so.FindProperty("animator").objectReferenceValue = animator;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private const string FireLightName = "FireLight";

    private static void AddFireLight(Transform model)
    {
        var go = new GameObject(FireLightName);
        go.transform.SetParent(model, false);
        go.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.62f, 0.3f, 1f);
        light.range = 3f;
        light.intensity = 1.2f;
        light.shadows = LightShadows.None;
        light.lightmapBakeType = LightmapBakeType.Realtime;
    }

    // 움직이지 않는 부분만 정적으로 표시한다(애니메이션 대상은 제외).
    private static void MarkStatic(Transform model, params string[] names)
    {
        foreach (string n in names)
        {
            Transform t = model.Find(n);
            if (t == null) continue;
            foreach (Transform c in t.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(c.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
        }
    }

    // ---------------- verification ----------------

    [MenuItem(MenuVerify)]
    public static void Verify()
    {
        int errors = 0;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimFolder}/{IdleClipName}.anim");
        var splash = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimFolder}/{SplashClipName}.anim");
        if (prefab == null || idle == null || splash == null)
        {
            Debug.LogError($"{LogTag} VERIFY prefab or clip missing");
            return;
        }

        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
            Transform model = go.transform.Find("Model");
            var animator = model != null ? model.GetComponent<Animator>() : null;
            if (model == null || animator == null) { Debug.LogError($"{LogTag} VERIFY Model/Animator missing"); return; }

            // 1) 크기: 테두리 높이
            float rim = model.Find("Cauldron_Body").GetComponent<Renderer>().bounds.max.y;
            bool sizeOk = Mathf.Abs(model.localScale.x - ModelScale) < 1e-4f && rim > 1.35f && rim < 1.55f;
            Debug.Log($"{LogTag} VERIFY scale={model.localScale.x:F2} rimHeight={rim:F2} m {(sizeOk ? "OK" : "FAIL")}");
            if (!sizeOk) errors++;

            // 2) 클립: 모든 커브가 실제 오브젝트·속성을 가리키는지, 길이·반복
            foreach (AnimationClip clip in new[] { idle, splash })
            {
                int bindings = 0, broken = 0;
                foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(clip))
                {
                    bindings++;
                    if (AnimationUtility.GetAnimatedObject(model.gameObject, b) == null) { broken++; Debug.LogError($"{LogTag} VERIFY {clip.name}: unresolved {b.path}.{b.propertyName}"); }
                }
                bool loopOk = clip == idle ? clip.isLooping && Mathf.Abs(clip.length - LoopLength) < 1e-3f : !clip.isLooping && Mathf.Abs(clip.length - SplashLength) < 1e-3f;
                bool ok = broken == 0 && bindings > 0 && loopOk;
                Debug.Log($"{LogTag} VERIFY clip {clip.name} length={clip.length:F2}s loop={clip.isLooping} curves={bindings} unresolved={broken} {(ok ? "OK" : "FAIL")}");
                if (!ok) errors++;
            }

            // 3) 반복 이음새(불꽃·연기·거품 위치/크기가 처음과 끝에서 같은지) + 평소 풍덩 숨김
            var tracked = new List<Transform>();
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("Fire_Flame_") || t.name.StartsWith("Smoke_Puff_") || t.name.StartsWith("Liquid_Bubble_")) tracked.Add(t);
            idle.SampleAnimation(model.gameObject, 0f);
            var start = new List<Vector3>();
            foreach (Transform t in tracked) start.Add(t.localScale + t.localPosition);
            float crownIdle = model.Find("Splash_Crown").localScale.magnitude;
            idle.SampleAnimation(model.gameObject, LoopLength);
            float seam = 0f;
            for (int i = 0; i < tracked.Count; i++) seam = Mathf.Max(seam, (tracked[i].localScale + tracked[i].localPosition - start[i]).magnitude);
            bool seamOk = seam < 0.02f && crownIdle < 1e-4f;
            Debug.Log($"{LogTag} VERIFY idle seamError={seam:F4} animated={tracked.Count} splashHiddenAtRest={crownIdle < 1e-4f} {(seamOk ? "OK" : "FAIL")}");
            if (!seamOk) errors++;

            // 4) 풍덩: 0.3초에 물보라가 보이고 물방울이 수면 위로 떠 있는지, 끝나면 다시 숨는지
            Transform crown = model.Find("Splash_Crown");
            Transform drop = model.Find("Splash_Droplet_01");
            float dropBaseY = drop.localPosition.y;
            splash.SampleAnimation(model.gameObject, 0.3f);
            float crownMid = crown.localScale.y;
            float dropMid = drop.localPosition.y - dropBaseY;
            splash.SampleAnimation(model.gameObject, SplashLength);
            float crownEnd = crown.localScale.magnitude + drop.localScale.magnitude;
            bool splashOk = crownMid > 0.5f && dropMid > 0.2f && crownEnd < 1e-3f;
            Debug.Log($"{LogTag} VERIFY splash crownScaleY@0.3s={crownMid:F2} dropletRise@0.3s={dropMid:F2} m hiddenAtEnd={crownEnd < 1e-3f} {(splashOk ? "OK" : "FAIL")}");
            if (!splashOk) errors++;

            // 5) 충돌체·트리거·빛·컨트롤러
            int walls = 0, triggers = 0, convex = 0;
            foreach (Collider c in model.GetComponentsInChildren<Collider>(true))
            {
                if (c.isTrigger) triggers++;
                else if (c is BoxCollider) walls++;
                else if (c is MeshCollider mc && mc.convex) convex++;
            }
            var splashTrigger = model.GetComponentInChildren<CauldronSplash>(true);
            var so = splashTrigger != null ? new SerializedObject(splashTrigger) : null;
            bool linked = so != null && so.FindProperty("animator").objectReferenceValue == animator;
            var controller = animator.runtimeAnimatorController as AnimatorController;
            bool hasTrigger = false;
            if (controller != null) foreach (var p in controller.parameters) if (p.name == CauldronSplash.SplashTriggerParameter && p.type == AnimatorControllerParameterType.Trigger) hasTrigger = true;
            bool layerOk = controller != null && controller.layers.Length == 2 && controller.layers[1].defaultWeight > 0.99f;
            bool light = model.Find(FireLightName) != null && model.Find(FireLightName).GetComponent<Light>() != null;
            bool partsOk = walls == WallSegments + 1 && triggers == 1 && convex == 1 && linked && hasTrigger && layerOk && light;
            Debug.Log($"{LogTag} VERIFY boxColliders={walls} triggers={triggers} convexMesh={convex} splashLinked={linked} splashParam={hasTrigger} layers={(controller != null ? controller.layers.Length : 0)} fireLight={light} {(partsOk ? "OK" : "FAIL")}");
            if (!partsOk) errors++;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }

        if (errors == 0) Debug.Log($"{LogTag} VERIFY ALL OK");
        else Debug.LogError($"{LogTag} VERIFY {errors} problem(s) found");
    }

    // ---------------- helpers ----------------

    private const float Tau = Mathf.PI * 2f;
    private static readonly string[] Axes = { "x", "y", "z" };
    private static readonly string[] RippleShapes = { "Ripple_000", "Ripple_090", "Ripple_180", "Ripple_270" };
    private static readonly string[] SplashShapes = { "Splash_Dip", "Ripple_000", "Ripple_090", "Ripple_180", "Ripple_270" };

    private static float Frac(float v) => v - Mathf.Floor(v);

    private static IEnumerable<Transform> SplashParts(Transform model)
    {
        Transform crown = model.Find("Splash_Crown");
        if (crown != null) yield return crown;
        for (int i = 1; i <= 8; i++)
        {
            Transform drop = model.Find($"Splash_Droplet_{i:00}");
            if (drop != null) yield return drop;
        }
    }

    // 함수를 일정 간격으로 샘플링해 커브를 만든다. linear=true면 키 사이를 직선으로 잇는다(순간 전환·되감기 구간의 튐 방지).
    private static void SetSampled(AnimationClip clip, string path, Type type, string property, float length,
        Func<float, float> f, float keysPerSecond = KeysPerSecond, bool linear = false)
    {
        int count = Mathf.CeilToInt(length * keysPerSecond);
        var keys = new Keyframe[count + 1];
        for (int i = 0; i <= count; i++)
        {
            float t = length * i / count;
            keys[i] = new Keyframe(t, f(t));
        }
        var curve = new AnimationCurve(keys);
        for (int i = 0; i < curve.length; i++)
        {
            var mode = linear ? AnimationUtility.TangentMode.Linear : AnimationUtility.TangentMode.ClampedAuto;
            AnimationUtility.SetKeyLeftTangentMode(curve, i, mode);
            AnimationUtility.SetKeyRightTangentMode(curve, i, mode);
        }
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property), curve);
    }

    private static void SetConstant(AnimationClip clip, string path, Type type, string property, float length, float value)
    {
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property), AnimationCurve.Constant(0f, length, value));
    }

    private static void SetLoop(AnimationClip clip, bool loop)
    {
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
    }

    private static AnimationClip LoadOrCreateClip(string name)
    {
        string path = $"{AnimFolder}/{name}.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip != null) return clip;
        clip = new AnimationClip { name = name };
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
