using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class LiveKitSetup : NetworkBehaviour
{
    public string liveKitSid;
    
    public override void OnStartLocalPlayer()
    {
        var rangeDetectionCollider = GetComponentInChildren<SphereCollider>();
        if (rangeDetectionCollider != null)
            rangeDetectionCollider.gameObject.AddComponent<LiveKitAudio>();
    }

    [Command]
    public void SetLiveKitSid(string sid)
    {
        liveKitSid = sid;

        if (isLocalPlayer)
        {
            GetComponentInChildren<SphereCollider>().enabled = true;
            // Fire Co-routine to update volume fade per frame here.
        }
    }
}
