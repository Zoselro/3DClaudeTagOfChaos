using UnityEngine;
using UnityEngine.Rendering;

// 괴물 본인 클라이언트의 시점 선택(GameFixPlan.md F6). 기본값은 GameSettings.MonsterDefaultView이고,
// GameSettings.AllowMonsterViewToggle이 켜져 있으면 전환 키(기본 V)로 1인칭/3인칭을 바꿀 수 있다(테스트용).
// 1인칭에서는 자기 몸이 화면을 가리지 않도록 본인 화면에서만 몸 렌더러를 그림자만 남긴다(다른 사람 화면은 그대로).
// MonsterController.Awake(IsMine)가 AddComponent로 붙인다.
public class MonsterViewSwitcher : MonoBehaviour
{
    private MonsterController monster;
    private Camera_Ctrl thirdPerson;
    private MonsterFirstPersonCamera firstPerson;
    private Renderer[] bodyRenderers;
    private ShadowCastingMode[] originalShadowModes;
    private MonsterViewMode current;

    public bool IsFirstPerson => current == MonsterViewMode.FirstPerson && firstPerson != null && firstPerson.enabled;
    public Quaternion YawRotation => firstPerson != null ? firstPerson.YawRotation : transform.rotation;

    public void Init(MonsterController owner, Transform eyeSocket)
    {
        monster = owner;
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[MonsterViewSwitcher] Main Camera not found.");
            enabled = false;
            return;
        }

        thirdPerson = cam.GetComponent<Camera_Ctrl>();
        firstPerson = cam.GetComponent<MonsterFirstPersonCamera>();
        if (firstPerson == null) firstPerson = cam.gameObject.AddComponent<MonsterFirstPersonCamera>();
        firstPerson.Attach(owner.transform, eyeSocket);

        bodyRenderers = owner.GetComponentsInChildren<Renderer>(true);
        originalShadowModes = new ShadowCastingMode[bodyRenderers.Length];
        for (int i = 0; i < bodyRenderers.Length; i++) originalShadowModes[i] = bodyRenderers[i].shadowCastingMode;

        GrabAimReticle.Create(owner);
        Apply(GameSettings.Current.MonsterDefaultView);
    }

    private void Update()
    {
        if (firstPerson == null || !GameSettings.Current.AllowMonsterViewToggle || !PlayerInput.ToggleViewPressed) return;
        Apply(current == MonsterViewMode.FirstPerson ? MonsterViewMode.ThirdPerson : MonsterViewMode.FirstPerson);
    }

    private void OnDestroy()
    {
        // 괴물이 사라지면(판 종료·퇴장) 카메라를 3인칭 기본 상태로 되돌려 다음 대상(관전 등)이 그대로 쓸 수 있게 한다.
        if (firstPerson != null) firstPerson.enabled = false;
        if (thirdPerson != null) thirdPerson.enabled = true;
    }

    private void Apply(MonsterViewMode view)
    {
        current = view;
        bool fp = view == MonsterViewMode.FirstPerson;

        if (fp) firstPerson.SyncYaw(thirdPerson != null ? thirdPerson.transform.eulerAngles.y : monster.transform.eulerAngles.y);
        if (thirdPerson != null)
        {
            thirdPerson.enabled = !fp;
            if (!fp) thirdPerson.SetFollowTarget(monster.gameObject, monster.CameraTargetHeight, monster.CameraDistance, keepRotation: false);
        }
        firstPerson.enabled = fp;

        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            if (bodyRenderers[i] == null) continue;
            bodyRenderers[i].shadowCastingMode = fp ? ShadowCastingMode.ShadowsOnly : originalShadowModes[i];
        }
    }
}
