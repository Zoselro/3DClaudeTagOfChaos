using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 쿠키 애니메이터(PlayerAnimator.controller)에 들기 관련 상태를 맞춘다(Request1009Plan.md §16.3·§16.4). 여러 번 실행해도 같은 결과.
// - Base Layer "Carried": 다른 쿠키에게 안겨 들린 몸(PlayerMoveState.Carried 트리거). 매달림 자세(Cookie_Hanging_Idle — 무릎이 굽고 팔이 머리 위)를 눕히면 팔 위에 안긴 모습이 된다(Play Mode 사진 비교로 고름).
public static class CookieAnimatorSetup
{
    public const string ControllerPath = "Assets/Animation/PlayerAnimator.controller";
    public const string CarriedState = "Carried";
    public const string CarriedClipFbx = "Assets/Animation/Cookie/Cookie_Hanging_Idle.fbx";
    private const float CarriedBlend = 0.15f;

    [MenuItem("Tools/Cookie/Setup Carry Animator States")]
    public static void SetupMenu() => Debug.Log("[CookieAnimatorSetup] " + Setup());

    public static string Setup()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) return "controller missing";
        string report = EnsureCarried(controller);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return report;
    }

    private static string EnsureCarried(AnimatorController controller)
    {
        if (!controller.parameters.Any(p => p.name == CarriedState))
            controller.AddParameter(CarriedState, AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState state = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == CarriedState);
        if (state == null) state = sm.AddState(CarriedState, new Vector3(500f, 330f, 0f));
        state.motion = LoadClip(CarriedClipFbx);

        AnimatorStateTransition any = sm.anyStateTransitions.FirstOrDefault(t => t.destinationState == state);
        if (any == null) any = sm.AddAnyStateTransition(state);
        any.hasExitTime = false;
        any.duration = CarriedBlend;
        any.canTransitionToSelf = false;
        any.conditions = new[] { new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = CarriedState } };
        return "Carried -> " + (state.motion != null ? state.motion.name : "null");
    }

    private static AnimationClip LoadClip(string fbx) =>
        AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
}
