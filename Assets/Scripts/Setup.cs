using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class Setup : NetworkBehaviour
{
    [Header("Required")]
    public SphereCollider sphereCollider;

    public TextMesh textMesh;
    
    [Header("Sync Vars")]
    [SyncVar(hook = nameof(OnLiveKitSidChanged))]
    public string liveKitSid;

    [SyncVar(hook = nameof(OnPlayerNameChanged))]
    public string playerName;
    
    public override void OnStartLocalPlayer()
    {
        sphereCollider.gameObject.AddComponent<LiveKitAudio>();
    }

    public override void OnStartServer()
    {
        playerName = "Player" + NetworkClient.connection.connectionId;
    }

    [Command]
    public void CmdSetLiveKitSid(string sid)
    {
        liveKitSid = sid;
    }

    private void OnPlayerNameChanged(string oldName, string newName)
    {
        textMesh.text = newName;
    }
    
    private void OnLiveKitSidChanged(string oldLiveKitSid, string newLiveKitSid)
    {
        // Fire Co-routine to update volume fade per frame here.
        StartCoroutine(EnableSphereCollider());
    }
    
    IEnumerator EnableSphereCollider()
    {
        yield return new WaitForSeconds(3);
        
        sphereCollider.enabled = true;
        
        if (!isLocalPlayer) yield break;
        
        yield return GetComponentInChildren<LiveKitAudio>().SetVolumeOfPlayersInRange();
    }
}
