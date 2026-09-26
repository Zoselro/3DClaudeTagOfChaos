using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Witch_Cookie_House 모델로 문/연기 애니메이션 클립, Animator Controller, 프리팹을 만들고 검증 결과를 Console에 남긴다.
// 프리팹이 없으면 에디터 로드 시 한 번 자동 실행되고, 이후에는 메뉴에서 다시 실행할 수 있다.
[InitializeOnLoad]
public static class WitchCookieHouseBuilder
{
    private const string MenuPath = "Tools/TagOfChaos/Build Witch Cookie House";
    private const string LogTag = "[WitchCookieHouse]";
    private const string RootFolder = "Assets/09. Environment/WitchCookieHouse";
    private const string AnimFolder = RootFolder + "/Animations";
    private const string PrefabFolder = "Assets/04. Prefabs/Environment";
    private const string PrefabPath = PrefabFolder + "/Witch_Cookie_House.prefab";

    // Blender 작업 기준과 동일: 30fps, 18프레임(0.6초)
    private const float SampleRate = 30f;
    private const int ClipFrames = 18;
    private const float ClipLength = ClipFrames / SampleRate;

    // Unity 좌표계에서 모든 문은 localEulerAngles.y = -90 일 때 집 안쪽으로, +75 일 때 바깥쪽으로 열린다.
    // 바깥쪽은 문짝 바깥 면 소용돌이 장식이 경첩 쪽 문틀에 닿는 76° 직전까지만 연다(Blender에서 1° 간격 검사).
    private const float DoorOpenAngle = -90f;
    private const float DoorOpenOutwardAngle = 75f;
    public const string DoorIsOpenParam = InteractableDoor.DefaultOpenParameter; // 런타임 문 개폐(InteractableDoor)와 같은 파라미터
    public const string DoorOutwardParam = InteractableDoor.DefaultOutwardParameter;

    private const string SmokeRootName = "Chimney_Smoke";
    private const string SmokePuffPrefix = "Chimney_Smoke_Puff_";
    private const string SmokeClipName = "Chimney_Smoke_Loop";

    private static readonly string[] Sides = { "Front", "Back", "Left", "Right" };

    static WitchCookieHouseBuilder()
    {
        EditorApplication.delayCall += BuildIfMissing;
    }

    private static void BuildIfMissing()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += BuildIfMissing;
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(WitchCookieHouseImportPostprocessor.ModelPath) == null) return;
        Build();
    }

    [MenuItem(MenuPath)]
    public static void Build()
    {
        var importer = AssetImporter.GetAtPath(WitchCookieHouseImportPostprocessor.ModelPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"{LogTag} Model not found: {WitchCookieHouseImportPostprocessor.ModelPath}");
            return;
        }
        if (WitchCookieHouseImportPostprocessor.RemapMaterials(importer)) importer.SaveAndReimport();

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(WitchCookieHouseImportPostprocessor.ModelPath);
        EnsureFolder(AnimFolder);
        EnsureFolder(PrefabFolder);

        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);

            foreach (string side in Sides)
            {
                Transform door = FindDeep(instance.transform, $"Door_{side}");
                if (door == null)
                {
                    Debug.LogError($"{LogTag} Door_{side} missing in model.");
                    continue;
                }
                AnimationClip open = WriteDoorClip($"Door_{side}_Open", 0f, DoorOpenAngle, easeOut: true);
                AnimationClip close = WriteDoorClip($"Door_{side}_Close", DoorOpenAngle, 0f, easeOut: false);
                AnimationClip openOut = WriteDoorClip($"Door_{side}_OpenOut", 0f, DoorOpenOutwardAngle, easeOut: true);
                AnimationClip closeOut = WriteDoorClip($"Door_{side}_CloseOut", DoorOpenOutwardAngle, 0f, easeOut: false);
                AnimatorController controller = WriteDoorController($"Door_{side}", open, close, openOut, closeOut);

                var animator = door.gameObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // 화면 밖에서도 문 콜라이더가 움직여야 함
                animator.updateMode = AnimatorUpdateMode.Fixed; // 키네마틱 문짝이 물리 스텝에 맞춰 움직이도록(research.md G3-3)

                var body = door.gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true; // 애니메이션으로 움직이는 콜라이더
                body.useGravity = false;

                // 쿠키·괴물이 상호작용 키로 여닫는다(GameLobbyScene.md §14). 프리팹을 다시 만들어도 유지되도록 빌더가 붙인다.
                door.gameObject.AddComponent<InteractableDoor>();
            }

            Transform smoke = FindDeep(instance.transform, SmokeRootName);
            if (smoke != null)
            {
                AnimationClip smokeClip = WriteSmokeClip(smoke);
                var smokeAnimator = smoke.gameObject.AddComponent<Animator>();
                smokeAnimator.runtimeAnimatorController = WriteLoopController(SmokeClipName, smokeClip);
                smokeAnimator.cullingMode = AnimatorCullingMode.CullCompletely;
            }

            MarkStatic(instance.transform);

            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath, out bool saved);
            if (!saved) Debug.LogError($"{LogTag} Failed to save prefab: {PrefabPath}");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }

        AssetDatabase.SaveAssets();
        Verify();
    }

    // ---------------- clips ----------------

    private static AnimationClip WriteDoorClip(string name, float from, float to, bool easeOut)
    {
        AnimationClip clip = LoadOrCreateClip(name);
        clip.ClearCurves();
        clip.frameRate = SampleRate;

        // 열기: 빠르게 시작해 부드럽게 멈춤, 닫기: ease-in-out
        AnimationCurve y = easeOut
            ? new AnimationCurve(new Keyframe(0f, from, 0f, 1.8f * (to - from) / ClipLength), new Keyframe(ClipLength, to, 0f, 0f))
            : AnimationCurve.EaseInOut(0f, from, ClipLength, to);

        SetCurve(clip, "", "localEulerAnglesRaw.x", AnimationCurve.Constant(0f, ClipLength, 0f));
        SetCurve(clip, "", "localEulerAnglesRaw.y", y);
        SetCurve(clip, "", "localEulerAnglesRaw.z", AnimationCurve.Constant(0f, ClipLength, 0f));
        SetLoop(clip, false);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    // 연기 덩어리 N개가 18프레임 동안 한 칸씩 위 슬롯으로 이동한다.
    // 맨 아래는 크기 0에서 생겨나고, 맨 위는 크기 0으로 줄어들며 사라져 끊김 없이 반복된다(투명 재질 없이 표현).
    private static AnimationClip WriteSmokeClip(Transform smokeRoot)
    {
        var puffs = new List<Transform>();
        for (int i = 0; ; i++)
        {
            Transform puff = smokeRoot.Find(SmokePuffPrefix + i);
            if (puff == null) break;
            puffs.Add(puff);
        }

        int n = puffs.Count;
        var slotPos = new Vector3[n + 1];
        var slotScale = new float[n + 1];
        for (int i = 0; i < n; i++)
        {
            slotPos[i] = puffs[i].localPosition;
            slotScale[i] = puffs[i].localScale.x;
        }
        slotPos[n] = slotPos[n - 1] + (slotPos[n - 1] - slotPos[n - 2]);
        slotScale[0] = 0f;
        slotScale[n] = 0f;

        AnimationClip clip = LoadOrCreateClip(SmokeClipName);
        clip.ClearCurves();
        clip.frameRate = SampleRate;
        for (int i = 0; i < n; i++)
        {
            string path = puffs[i].name;
            for (int axis = 0; axis < 3; axis++)
            {
                string c = "xyz"[axis].ToString();
                SetCurve(clip, path, "m_LocalPosition." + c, Linear(slotPos[i][axis], slotPos[i + 1][axis]));
                SetCurve(clip, path, "m_LocalScale." + c, Linear(slotScale[i], slotScale[i + 1]));
            }
        }
        SetLoop(clip, true);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimationCurve Linear(float from, float to)
    {
        var curve = new AnimationCurve(new Keyframe(0f, from), new Keyframe(ClipLength, to));
        for (int k = 0; k < curve.length; k++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.Linear);
        }
        return curve;
    }

    private static void SetCurve(AnimationClip clip, string path, string property, AnimationCurve curve)
    {
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
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

    // ---------------- controllers ----------------

    // Closed(기본, 모션 없음) -> Open/OpenOut -> Close/CloseOut.
    // IsOpen으로 여닫고, OpenOutward로 여는 방향을 고른다(InteractableDoor는 열 때만 방향을 정한다).
    // 상태 이름은 InteractableDoor가 입장 시 끝 자세로 바로 맞출 때(Animator.Play) 그대로 쓴다.
    private static AnimatorController WriteDoorController(string name, AnimationClip open, AnimationClip close,
        AnimationClip openOut, AnimationClip closeOut)
    {
        AnimatorController controller = RecreateController(name);
        controller.AddParameter(DoorIsOpenParam, AnimatorControllerParameterType.Bool);
        controller.AddParameter(DoorOutwardParam, AnimatorControllerParameterType.Bool);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState closed = AddDoorState(sm, InteractableDoor.ClosedStateName, null);
        AnimatorState opening = AddDoorState(sm, InteractableDoor.OpenStateName, open);
        AnimatorState closing = AddDoorState(sm, "Close", close);
        AnimatorState openingOut = AddDoorState(sm, InteractableDoor.OpenOutwardStateName, openOut);
        AnimatorState closingOut = AddDoorState(sm, "CloseOut", closeOut);
        sm.defaultState = closed;

        foreach (AnimatorState from in new[] { closed, closing, closingOut })
        {
            AddOpenTransition(from, opening, outward: false);
            AddOpenTransition(from, openingOut, outward: true);
        }
        AddBoolTransition(opening, closing, false);
        AddBoolTransition(openingOut, closingOut, false);
        return controller;
    }

    private static AnimatorState AddDoorState(AnimatorStateMachine sm, string name, Motion motion)
    {
        AnimatorState state = sm.AddState(name);
        state.motion = motion;
        state.writeDefaultValues = false;
        return state;
    }

    private static void AddOpenTransition(AnimatorState from, AnimatorState to, bool outward)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0f;
        t.AddCondition(AnimatorConditionMode.If, 0f, DoorIsOpenParam);
        t.AddCondition(outward ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, DoorOutwardParam);
    }

    private static AnimatorController WriteLoopController(string name, AnimationClip clip)
    {
        AnimatorController controller = RecreateController(name);
        AnimatorState state = controller.layers[0].stateMachine.AddState("Loop");
        state.motion = clip;
        controller.layers[0].stateMachine.defaultState = state;
        return controller;
    }

    private static AnimatorController RecreateController(string name)
    {
        string path = $"{AnimFolder}/{name}.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);
        return AnimatorController.CreateAnimatorControllerAtPath(path);
    }

    private static void AddBoolTransition(AnimatorState from, AnimatorState to, bool value)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0f;
        t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, DoorIsOpenParam);
    }

    // ---------------- prefab helpers ----------------

    // 움직이는 문/연기를 제외한 나머지를 정적 오브젝트로 표시한다(배칭·라이트맵·오클루전).
    private static void MarkStatic(Transform t)
    {
        if (t.name == "Doors" || t.name == SmokeRootName) return;
        GameObjectUtility.SetStaticEditorFlags(t.gameObject,
            StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic |
            StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
        foreach (Transform child in t) MarkStatic(child);
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }

    // ---------------- verification ----------------

    [MenuItem("Tools/TagOfChaos/Verify Witch Cookie House")]
    public static void Verify()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"{LogTag} VERIFY prefab missing.");
            return;
        }

        Scene preview = EditorSceneManager.NewPreviewScene();
        int errors = 0;
        try
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
            Transform root = go.transform;

            // 1) 문 회전: 경첩 고정 + 안쪽(Open)/바깥쪽(OpenOut)으로 열림 + 콜라이더 동반 회전
            foreach (string side in Sides)
            {
                Transform door = FindDeep(root, $"Door_{side}");
                var col = FindDeep(root, $"COL_Door_{side}");
                if (door == null || col == null) { Debug.LogError($"{LogTag} VERIFY Door_{side}: missing door/collider"); errors++; continue; }

                var doorAnimator = door.GetComponent<Animator>();
                bool interactable = door.GetComponent<InteractableDoor>() != null;
                bool physicsUpdate = doorAnimator != null && doorAnimator.updateMode == AnimatorUpdateMode.Fixed;
                bool outwardParam = false;
                var controller = doorAnimator != null ? doorAnimator.runtimeAnimatorController as AnimatorController : null;
                if (controller != null)
                    foreach (AnimatorControllerParameter p in controller.parameters)
                        if (p.name == DoorOutwardParam) outwardParam = true;

                foreach (bool outward in new[] { false, true })
                {
                    string clipName = outward ? $"Door_{side}_OpenOut" : $"Door_{side}_Open";
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimFolder}/{clipName}.anim");
                    if (clip == null) { Debug.LogError($"{LogTag} VERIFY {clipName}: missing clip"); errors++; continue; }

                    Vector3 hinge = door.position;
                    Vector3 restEuler = door.localRotation.eulerAngles;
                    float before = Flat(door.GetComponent<Renderer>().bounds.center - root.position).magnitude;
                    Vector3 colLocal = col.GetComponent<MeshCollider>().sharedMesh.bounds.center;
                    Vector3 colBefore = col.TransformPoint(colLocal);
                    clip.SampleAnimation(door.gameObject, clip.length);
                    float after = Flat(door.GetComponent<Renderer>().bounds.center - root.position).magnitude;
                    Vector3 colAfter = col.TransformPoint(colLocal);
                    bool rightWay = outward ? after > before + 0.3f : after < before - 0.3f;
                    bool ok = restEuler.sqrMagnitude < 1e-4f && (door.position - hinge).sqrMagnitude < 1e-6f && rightWay && (colAfter - colBefore).sqrMagnitude > 0.1f
                              && interactable && physicsUpdate && outwardParam;
                    Debug.Log($"{LogTag} VERIFY {clipName} restRot={restEuler} openY={door.localEulerAngles.y:F1} " +
                              $"hinge={hinge} centerDist {before:F2}->{after:F2} ({(outward ? "outward" : "inward")}={rightWay}) colliderMoved={(colAfter - colBefore).magnitude:F2} " +
                              $"clip={clip.length * clip.frameRate:F0}f interactableDoor={interactable} fixedUpdate={physicsUpdate} outwardParam={outwardParam} {(ok ? "OK" : "FAIL")}");
                    if (!ok) errors++;
                    clip.SampleAnimation(door.gameObject, 0f);
                }
            }

            // 2) 연기 루프 이음새: t=끝에서 i번째 덩어리 == t=0에서 i+1번째 덩어리
            Transform smoke = FindDeep(root, SmokeRootName);
            var smokeClip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimFolder}/{SmokeClipName}.anim");
            if (smoke != null && smokeClip != null)
            {
                int n = smoke.childCount;
                var start = new Vector4[n];
                smokeClip.SampleAnimation(smoke.gameObject, 0f);
                for (int i = 0; i < n; i++) start[i] = Pack(smoke.Find(SmokePuffPrefix + i));
                smokeClip.SampleAnimation(smoke.gameObject, smokeClip.length);
                float seam = 0f;
                for (int i = 0; i < n - 1; i++) seam = Mathf.Max(seam, (Pack(smoke.Find(SmokePuffPrefix + i)) - start[i + 1]).magnitude);
                float topScale = smoke.Find(SmokePuffPrefix + (n - 1)).localScale.x;
                bool ok = seam < 1e-3f && topScale < 1e-3f && start[0].w < 1e-3f && smokeClip.isLooping;
                Debug.Log($"{LogTag} VERIFY Smoke puffs={n} frames={smokeClip.length * smokeClip.frameRate:F0} loop={smokeClip.isLooping} seamError={seam:F4} topEndScale={topScale:F3} {(ok ? "OK" : "FAIL")}");
                if (!ok) errors++;
            }
            else { Debug.LogError($"{LogTag} VERIFY smoke root or clip missing"); errors++; }

            // 3) 메시/머티리얼/충돌체
            int renderers = 0, badMats = 0, emptyMeshes = 0, colliders = 0, colWithRenderer = 0;
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh == null || mf.sharedMesh.vertexCount == 0) emptyMeshes++;
            foreach (MeshRenderer r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderers++;
                if (r.name.StartsWith(WitchCookieHouseImportPostprocessor.CollisionPrefix)) colWithRenderer++;
                foreach (Material m in r.sharedMaterials)
                    if (m == null || !AssetDatabase.GetAssetPath(m).StartsWith(WitchCookieHouseImportPostprocessor.MaterialFolder)) badMats++;
            }
            foreach (MeshCollider c in go.GetComponentsInChildren<MeshCollider>(true))
                if (c.sharedMesh != null && c.convex) colliders++;
            bool meshOk = badMats == 0 && emptyMeshes == 0 && colWithRenderer == 0 && colliders > 0;
            Debug.Log($"{LogTag} VERIFY renderers={renderers} badMaterialSlots={badMats} emptyMeshes={emptyMeshes} convexColliders={colliders} collisionWithRenderer={colWithRenderer} {(meshOk ? "OK" : "FAIL")}");
            if (!meshOk) errors++;

            Bounds b = new Bounds(root.position, Vector3.zero);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) b.Encapsulate(r.bounds);
            Debug.Log($"{LogTag} VERIFY bounds size={b.size} min={b.min} max={b.max}");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }

        if (errors == 0) Debug.Log($"{LogTag} VERIFY ALL OK");
        else Debug.LogError($"{LogTag} VERIFY {errors} problem(s) found");
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    private static Vector4 Pack(Transform t) => new Vector4(t.localPosition.x, t.localPosition.y, t.localPosition.z, t.localScale.x);
}
