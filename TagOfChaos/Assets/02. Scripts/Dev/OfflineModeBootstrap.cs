using Photon.Pun;
using UnityEngine;

public class OfflineModeBootstrap : MonoBehaviour
{
    private void Awake()
    {
        PhotonNetwork.OfflineMode = true;
    }
}
