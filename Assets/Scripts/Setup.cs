using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class Setup : NetworkBehaviour
{
    [Header("Required")]
    public SphereCollider sphereCollider;

    [Header("Sync Vars")]
    [SyncVar(hook = nameof(OnLiveKitSidChanged))]
    public string liveKitSid;

    
    public override void OnStartLocalPlayer()
    {
        sphereCollider.gameObject.AddComponent<LiveKitAudio>();
    }

    [Command]
    public void CmdSetLiveKitSid(string sid)
    {
        liveKitSid = sid;
    }

    private void OnLiveKitSidChanged(string oldLiveKitSid, string newLiveKitSid)
    {
        if (isLocalPlayer)
        {
            // Fire Co-routine to update volume fade per frame here.
            StartCoroutine(EnableSphereCollider());
        }
    }
    
    IEnumerator EnableSphereCollider()
    {
        yield return new WaitForSeconds(3);

        if (!isLocalPlayer) yield break;
        
        sphereCollider.enabled = true;
        yield return GetComponentInChildren<LiveKitAudio>().SetVolumeOfPlayersInRange();
    }
}
