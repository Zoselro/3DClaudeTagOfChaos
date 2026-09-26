using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// UI 요소가 화면 밖으로 밀려난 배치를 찾아내는 읽기 전용 검사 도구(CLAUDE.md: 에디터 → Assets/Editor/).
// 늘어나는 앵커(stretch)인데 anchoredPosition이 크게 어긋난 요소 때문에 결과 화면·괴물 공지·색칠 패널의
// 남은 시간/버튼이 연달아 보이지 않았다(Bug-fix-plan.md §25 S11, §25 P6, §28.2) — 같은 유형의 재발을 막는다.
// 자동 수정은 하지 않고 목록만 Console에 출력한다.
public static class UILayoutValidator
{
    private const string MenuPath = "Tools/TagOfChaos/Validate UI Layout";
    private static readonly string[] PrefabFolders = { "Assets/Resources/UI", "Assets/04. Prefabs" };

    [MenuItem(MenuPath)]
    public static void ValidateFromMenu()
    {
        int issues = ValidateBuildScenesAndPrefabs();
        if (issues == 0) Debug.Log("[UILayoutValidator] No off-screen UI found.");
        else Debug.LogWarning($"[UILayoutValidator] {issues} suspicious RectTransform(s) found. See warnings above.");
    }

    // 빌드에 포함된 씬과 UI 프리팹을 모두 검사하고 문제 개수를 돌려준다(자동 검증에서도 호출).
    public static int ValidateBuildScenesAndPrefabs()
    {
        int issues = 0;
        var openedHere = new List<Scene>();

        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
        {
            if (!buildScene.enabled) continue;
            Scene scene = SceneManager.GetSceneByPath(buildScene.path);
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Additive);
                openedHere.Add(scene);
            }
            foreach (GameObject root in scene.GetRootGameObjects())
                issues += ValidateHierarchy(root.transform, buildScene.path);
        }

        foreach (Scene scene in openedHere)
            EditorSceneManager.CloseScene(scene, true); // 이 도구가 추가로 연 씬만 닫는다(사용자가 열어 둔 씬은 그대로)

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", PrefabFolders))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) issues += ValidateHierarchy(prefab.transform, path);
        }
        return issues;
    }

    private static int ValidateHierarchy(Transform root, string source)
    {
        int issues = 0;
        foreach (RectTransform rt in root.GetComponentsInChildren<RectTransform>(true))
        {
            if (!(rt.parent is RectTransform parent)) continue;
            if (IsOffsetOutsideParent(rt, parent, out string reason))
            {
                issues++;
                Debug.LogWarning($"[UILayoutValidator] {source} : {GetPath(rt)} — {reason}", rt);
            }
        }
        return issues;
    }

    // 늘어나는 축에서 anchoredPosition이 부모 크기의 절반을 넘게 어긋나 있으면 부모 영역 밖에 그려진다.
    private static bool IsOffsetOutsideParent(RectTransform rt, RectTransform parent, out string reason)
    {
        reason = null;
        Vector2 parentSize = parent.rect.size;
        bool stretchX = !Mathf.Approximately(rt.anchorMin.x, rt.anchorMax.x);
        bool stretchY = !Mathf.Approximately(rt.anchorMin.y, rt.anchorMax.y);

        if (stretchX && parentSize.x > 0f && Mathf.Abs(rt.anchoredPosition.x) > parentSize.x * 0.5f)
            reason = $"stretched on X but anchoredPosition.x={rt.anchoredPosition.x:F0} (parent width {parentSize.x:F0})";
        else if (stretchY && parentSize.y > 0f && Mathf.Abs(rt.anchoredPosition.y) > parentSize.y * 0.5f)
            reason = $"stretched on Y but anchoredPosition.y={rt.anchoredPosition.y:F0} (parent height {parentSize.y:F0})";

        return reason != null;
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }
}
