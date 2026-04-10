using UnityEngine;
using Unity.Netcode;

public class SessionBootstrap : MonoBehaviour
{
    public void StartHostSession()
    {
        NetworkManager.Singleton.StartHost();
    }

    public void StartClientSession()
    {
        NetworkManager.Singleton.StartClient();
    }
}