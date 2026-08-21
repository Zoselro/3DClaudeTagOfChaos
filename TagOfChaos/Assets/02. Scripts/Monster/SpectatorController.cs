using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// 파괴된 쿠키(hitCount>=2)가 Space 키로 생존 쿠키 시점을 순환하며 관전한다(GameRule.md §6.3,
// v3(포획)의 IsCaught 트리거를 hitCount>=2로 재설계). HideOrSeekPlayer.prefab에 부착, 로컬
// 소유자 전용 — HideOrSeekPlayer.RequestGrabKill()이 파괴되는 순간 EnterSpectatorMode()를 호출한다.
public class SpectatorController : MonoBehaviourPunCallbacks
{
    [SerializeField] private PhotonView pv;
    [SerializeField] private float followDistance = 4f;
    [SerializeField] private float followHeight = 2f;

    private readonly List<HideOrSeekPlayer> aliveCookies = new List<HideOrSeekPlayer>();
    private int spectateIndex;
    private bool isSpectating;
    private Camera cam;

    private void Awake()
    {
        cam = Camera.main;
    }

    public void EnterSpectatorMode()
    {
        if (!pv.IsMine || isSpectating) return;
        isSpectating = true;

        // Camera_Ctrl(쿠키 3인칭 추적)이 계속 transform을 덮어쓰면 관전 카메라와 충돌하므로 끈다.
        if (cam != null)
        {
            var camCtrl = cam.GetComponent<Camera_Ctrl>();
            if (camCtrl != null) camCtrl.enabled = false;
        }

        RefreshAliveCookies();
        spectateIndex = 0;
    }

    private void Update()
    {
        if (!pv.IsMine || !isSpectating) return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            RefreshAliveCookies();
            if (aliveCookies.Count > 0)
                spectateIndex = (spectateIndex + 1) % aliveCookies.Count;
        }
    }

    private void LateUpdate()
    {
        if (!pv.IsMine || !isSpectating || cam == null) return;
        if (aliveCookies.Count == 0 || spectateIndex >= aliveCookies.Count) return;

        Transform target = aliveCookies[spectateIndex].transform;
        Vector3 offset = -target.forward * followDistance + Vector3.up * followHeight;
        cam.transform.position = target.position + offset;
        cam.transform.LookAt(target.position + Vector3.up * 1.5f);
    }

    private void RefreshAliveCookies()
    {
        aliveCookies.Clear();
        foreach (var cookie in FindObjectsByType<HideOrSeekPlayer>(FindObjectsSortMode.None))
        {
            if (cookie.IsMine) continue; // 자기 자신(이미 파괴됨) 제외

            var otherPv = cookie.GetComponent<PhotonView>();
            int hitCount = otherPv != null && otherPv.Owner != null &&
                           otherPv.Owner.CustomProperties.TryGetValue(NetKeys.HitCount, out object v)
                ? (int)v
                : 0;
            if (hitCount < 2) aliveCookies.Add(cookie);
        }
    }
}
