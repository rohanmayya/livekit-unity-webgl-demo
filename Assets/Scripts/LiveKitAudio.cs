using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using LiveKit;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

public struct LiveKitAuth
{
    public string token;
}

public class LiveKitAudio : MonoBehaviour
{
    Room room;
    Setup setup;

    Dictionary<string, Setup> playerSetupsInRangeMap =
        new Dictionary<string, Setup>();
    
    void Start()
    {
        setup = GetComponentInParent<Setup>();

#if UNITY_WEBGL
		StartCoroutine(SetupLiveKit());
#endif
    }

    public IEnumerator SetVolumeOfPlayersInRange()
    {
        Debug.Log("In this call -- SetVolumeOfPlayersInRange");

        while (true)
        {
            yield return new WaitForEndOfFrame();

            try
            {
                foreach (var playerSetup in playerSetupsInRangeMap)
                {
                    if (playerSetup.Value == null)
                        continue;

                    float distance = Vector3.Distance(transform.position, playerSetup.Value.transform.position);
                    float maxDistance = playerSetup.Value.sphereCollider.radius;

                    if (distance <= maxDistance)
                        SetVolume(playerSetup.Value, 1);
                    else
                        SetVolume(playerSetup.Value, 1 - Mathf.Clamp01((distance - maxDistance) / maxDistance));
                }
            }
            catch (Exception e)
            {
                Debug.Log("WE GOT AN EXCEPTION! -- " + e.GetType());
            }
        }
    }

    void SetVolume(Setup playerSetup, float volume)
    {
        if (playerSetup.liveKitSid == null) return;
        
        
        var remoteParticipantLiveKitSid = playerSetup.liveKitSid;
        
        
        if (room?.Participants != null && room.Participants.ContainsKey(remoteParticipantLiveKitSid) && room.Participants[remoteParticipantLiveKitSid].AudioTracks != null)
        {
            room.Participants[remoteParticipantLiveKitSid].SetVolume(volume);
            
            foreach (var track in room.Participants[remoteParticipantLiveKitSid].AudioTracks)
        	{
        		if (track.Value?.Track?.AttachedElements == null) continue;
        	
        		foreach (var audioElement in track.Value.Track.AttachedElements)
        		{
        			((HTMLAudioElement)audioElement).Volume = volume;
        		}
        	}
        }
    }


    public void OnTriggerEnter(Collider other)
    {
        var remotePlayerSetup = other.GetComponentInParent<Setup>();

#if UNITY_EDITOR
        SetVolume(remotePlayerSetup, 1);
#endif

        if (!remotePlayerSetup || remotePlayerSetup.liveKitSid == null)
            return;

        var remoteParticipantLiveKitSid = remotePlayerSetup.liveKitSid;
        AddToVolumeChecking(remoteParticipantLiveKitSid, remotePlayerSetup.gameObject);

        if (room != null && room.Participants != null)
        {
            if (!room.Participants.ContainsKey(remoteParticipantLiveKitSid))
                return;

            Participant remoteParticipant = room.Participants[remoteParticipantLiveKitSid];

            if (remoteParticipant.AudioTracks != null)
            {
                var audioTracks = remoteParticipant.AudioTracks;

                foreach (var audioTrack in audioTracks)
                    if (audioTrack.Value != null)
                        ((RemoteTrackPublication)audioTrack.Value).SetSubscribed(true);
            }
        }
    }

    public void OnTriggerExit(Collider other)
    {
        var remotePlayerSetup = other.GetComponentInParent<Setup>();

#if UNITY_EDITOR
        SetVolume(remotePlayerSetup,0);
#endif

        if (!remotePlayerSetup || remotePlayerSetup.liveKitSid == null)
            return;

        var remoteParticipantLiveKitSid = remotePlayerSetup.liveKitSid;
        RemoveFromVolumeChecking(remoteParticipantLiveKitSid, remotePlayerSetup.gameObject);

        if (room != null && room.Participants != null)
        {
            if (!room.Participants.ContainsKey(remoteParticipantLiveKitSid))
                return;

            Participant remoteParticipant = room.Participants[remoteParticipantLiveKitSid];

            if (remoteParticipant.AudioTracks != null)
            {
                var audioTracks = remoteParticipant.AudioTracks;
                foreach (var audioTrack in audioTracks)
                    if (audioTrack.Value != null)
                        ((RemoteTrackPublication)audioTrack.Value).SetSubscribed(false);
            }
        }
    }

    IEnumerator SetupLiveKit()
    {

        string url = "https://YOURAPI.com/api/token";
        
        string liveKitConnectJson = $"{{ \"participantIdentity\":\"{setup.netId}\", \"room\":\"TestingRoom\" }}";

        using (UnityWebRequest www = UnityWebRequest.Post(url, liveKitConnectJson))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(liveKitConnectJson);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
                Debug.Log(www.error);
            else
            {
                string response = Encoding.UTF8.GetString(www.downloadHandler.data);
                LiveKitAuth liveKitAuth = JsonConvert.DeserializeObject<LiveKitAuth>(response);

                yield return SetupLiveKitRoom(liveKitAuth.token);
            }
        }
    }

    IEnumerator SetupLiveKitRoom(string token)
    {
        RoomConnectOptions roomConnectOptions = new RoomConnectOptions { AutoSubscribe = false };

        room = new Room();
        RegisterLiveKitCallbacks();

        var c = room.Connect("wss://yoururl.livekit.cloud", token, roomConnectOptions);
        yield return c;

        Debug.Log("Finished setup and connected to room");

        setup.CmdSetLiveKitSid(room.LocalParticipant.Sid);

        room.LocalParticipant.IsSpeakingChanged += (isSpeaking) => HandleLocalParticipantSpeakingChanged(isSpeaking);
    }

    void RegisterLiveKitCallbacks()
    {
        room.ParticipantConnected += (participant) => HandleParticipantConnected(participant);
        room.ParticipantDisconnected += (participant) => HandleParticipantDisconnected(participant);
        room.TrackPublished += (publication, participant) => HandleRemoteTrackPublished(publication.Track, publication, participant);
        room.TrackSubscribed += (track, publication, participant) => HandleTrackSubscribed(track, publication);
        room.TrackUnsubscribed += (track, publication, participant) => HandleTrackUnsubscribed(track, publication);
        room.ActiveSpeakersChanged += (speakers) => HandleActiveSpeakersChanged(speakers);
    }

    void HandleActiveSpeakersChanged(JSArray<Participant> speakers)
    {
        foreach (var setup in playerSetupsInRangeMap)
            if (room.Participants.TryGetValue(setup.Value.liveKitSid, out RemoteParticipant participant))
                if (speakers.Contains(participant))
                    setup.Value.ActivateTalkingIndicator();
                else
                    setup.Value.DeactivateTalkingIndicator();
    }

    void HandleLocalParticipantSpeakingChanged(bool isSpeaking)
    {
        if (isSpeaking)
            setup.ActivateTalkingIndicator();
        else
            setup.DeactivateTalkingIndicator();
    }


    void HandleParticipantConnected(Participant remoteParticipant)
    {
        if (playerSetupsInRangeMap.ContainsKey(remoteParticipant.Sid))
            if (remoteParticipant.AudioTracks != null)
                foreach (var audioTrack in remoteParticipant.AudioTracks)
                    if (!((RemoteTrackPublication)audioTrack.Value).IsSubscribed)
                        ((RemoteTrackPublication)audioTrack.Value).SetSubscribed(true);
    }

    void HandleParticipantDisconnected(Participant remoteParticipant)
    {
        if (playerSetupsInRangeMap.ContainsKey(remoteParticipant.Sid))
            playerSetupsInRangeMap.Remove(remoteParticipant.Sid);
    }

    void HandleRemoteTrackPublished(Track track, TrackPublication publication, Participant participant)
    {
        // Publishing before colliders enabled is fine. Then collider trigger enter takes care of subscribing to each other
        // This callback is needed for when colliders enabled. New guy subscribes to the old, already connected guy who has already published his audio tracks. Hence the old guy can listen to the new guy.
        // But this is required for when new guy is trying to listen to others. Whenever he wants to publish his audio tracks, he's free to do so. (By hitting unmute.)
        // As soon as he does so, every other client in his vicinity fires this callback. They end up subscribing to him. This prevents the need for new guy to walk in and out to be heard. 
        // Happens once when mic track is published on clicking unmute button for the first time.
        if (playerSetupsInRangeMap.ContainsKey(participant.Sid))
        {
            Participant otherParticipant = room?.Participants[participant.Sid];

            if (otherParticipant?.AudioTracks != null)
                foreach (var audioTrack in otherParticipant.AudioTracks)
                    if (audioTrack.Value != null)
                        ((RemoteTrackPublication)audioTrack.Value).SetSubscribed(true);
        }
    }

    void HandleTrackSubscribed(Track track, TrackPublication publication)
    {
        if (track.Kind == TrackKind.Audio && publication is RemoteTrackPublication)
            track.Attach();
    }

    void HandleTrackUnsubscribed(Track track, TrackPublication publication) => track?.Detach();

    void AddToVolumeChecking(string remoteParticipantId, GameObject playerGameobject)
    {
        var otherPlayerSetup = playerGameobject.GetComponent<Setup>();

        if (otherPlayerSetup)
        {
            playerSetupsInRangeMap.Add(remoteParticipantId, otherPlayerSetup);
            // otherPlayerSetup.ShowTalkingIndicator();
        }
    }

    void RemoveFromVolumeChecking(string remoteParticipantId, GameObject playerGameobject)
    {
        var otherPlayerSetup = playerGameobject.GetComponent<Setup>();

        if (otherPlayerSetup)
        {
            playerSetupsInRangeMap.Remove(remoteParticipantId);
            // playerSetup.HideTalkingIndicator();
        }
    }
}