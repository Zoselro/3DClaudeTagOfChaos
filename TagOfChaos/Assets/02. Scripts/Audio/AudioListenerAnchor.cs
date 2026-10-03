using UnityEngine;
using UnityEngine.SceneManagement;

// 소리를 듣는 위치(DistanceFadePlan.md §2.1, L1 = 캐릭터 머리). AudioRuntime의 자식에 하나뿐인 AudioListener를 두고,
// 씬 카메라의 AudioListener는 끈다. 위치는 3인칭 카메라가 따라가는 대상의 시선 높이(내 쿠키·괴물, 관전 중이면 관전 대상),
// 괴물 1인칭이면 눈(= 카메라), 따라갈 대상이 없으면(로비·대기실 등) 카메라. 방향은 늘 카메라 방향 — 화면 왼쪽 것은 왼쪽에서 들린다.
// 카메라가 3.2 m 뒤에 있어도 거리 감쇠가 캐릭터 기준으로 맞는다. AudioRuntime이 LateUpdate에서 Tick을 부른다.
public class AudioListenerAnchor : MonoBehaviour
{
    private static AudioListenerAnchor current;

    private Camera lastCamera;
    private Camera_Ctrl thirdPerson;
    private MonsterFirstPersonCamera firstPerson;

    // 지금 듣는 위치(월드). 거리 감쇠·영역 소리·추격음이 모두 이 점을 쓴다.
    public static Vector3 Position => current != null ? current.transform.position : FallbackPosition();

    private static Vector3 FallbackPosition()
    {
        Camera cam = Camera.main;
        return cam != null ? cam.transform.position : Vector3.zero;
    }

    public static AudioListenerAnchor Create(Transform parent)
    {
        var go = new GameObject("Listener");
        go.transform.SetParent(parent, false);
        go.AddComponent<AudioListener>();
        return go.AddComponent<AudioListenerAnchor>();
    }

    private void OnEnable()
    {
        current = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
        MuteSceneListeners();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (current == this) current = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => MuteSceneListeners();

    // 리스너가 둘이면 유니티가 경고하고 소리가 섞이므로, 씬에 있는 다른 리스너(Main Camera 기본)는 끈다.
    private void MuteSceneListeners()
    {
        AudioListener mine = GetComponent<AudioListener>();
        foreach (AudioListener l in FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (l != mine && l.enabled) l.enabled = false;
    }

    public void Tick()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        if (cam != lastCamera)
        {
            lastCamera = cam;
            thirdPerson = cam.GetComponent<Camera_Ctrl>();
            firstPerson = cam.GetComponent<MonsterFirstPersonCamera>();
            AudioListener sceneListener = cam.GetComponent<AudioListener>();
            if (sceneListener != null) sceneListener.enabled = false;
        }
        transform.SetPositionAndRotation(ListenPoint(cam), cam.transform.rotation);
    }

    private Vector3 ListenPoint(Camera cam)
    {
        if (firstPerson != null && firstPerson.enabled) return cam.transform.position;
        if (thirdPerson != null && thirdPerson.enabled && thirdPerson.FollowTarget != null) return thirdPerson.FocusPoint;
        return cam.transform.position;
    }
}
