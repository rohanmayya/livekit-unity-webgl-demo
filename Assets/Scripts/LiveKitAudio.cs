using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class LiveKitAudio : NetworkBehaviour
{
    [SerializeField] private SphereCollider sphereCollider;

    public override void OnStartLocalPlayer()
    {
        sphereCollider.enabled = true;
    }
}

