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
    public static Room room;
    public static Action ToggleScreenShareAction;

    private Setup setup;
    private TVFullScreenUI screenShareUICanvas;

    private readonly Dictionary<string, Setup> _playerSetupsInRangeMap =
        new Dictionary<string, Setup>();

    private void Awake()
    {
        ToggleScreenShareAction = ToggleScreenShare;
    }
    
    void Start()
    {
        setup = GetComponentInParent<Setup>();

#if UNITY_WEBGL && !UNITY_EDITOR
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
                foreach (var playerSetup in _playerSetupsInRangeMap)
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
        Debug.Log($"Trigger Entered with {other.GetComponentInParent<Setup>().playerName}");

        var remotePlayerSetup = other.GetComponentInParent<Setup>();

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

        string url = Constants.LiveKitTokenEndpoint;
        
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
        RegisterAudioCallbacks();
        RegisterScreenShareCallbacks();
        
        var c = room.Connect(Constants.LiveKitRoomUrl, token, roomConnectOptions);
        yield return c;

        yield return room.LocalParticipant.SetMicrophoneEnabled(true);
        
        Debug.Log("Finished setup and connected to room");

        setup.CmdSetLiveKitSid(room.LocalParticipant.Sid);

        room.LocalParticipant.IsSpeakingChanged += (isSpeaking) => HandleLocalParticipantSpeakingChanged(isSpeaking);
        
        SubscribeToExistingScreenShareTracks();
    }

    void RegisterAudioCallbacks()
    {
        room.ParticipantConnected += HandleParticipantConnected;
        room.ParticipantDisconnected += HandleParticipantDisconnected;
        room.TrackPublished += HandleRemoteAudioTrackPublished;
        room.TrackSubscribed += HandleAudioTrackSubscribed;
        room.TrackUnsubscribed += HandleAudioTrackUnsubscribed;
        room.ActiveSpeakersChanged += HandleActiveSpeakersChanged;
    }

    void RegisterScreenShareCallbacks()
    {
        room.LocalTrackPublished += HandleLocalScreenShareTrackPublished;
        room.TrackPublished += HandleRemoteScreenShareTrackPublished;
        room.TrackSubscribed += HandleScreenShareTrackSubscribed;
    }

    
    void HandleParticipantConnected(Participant remoteParticipant)
    {
        if (_playerSetupsInRangeMap.ContainsKey(remoteParticipant.Sid))
            if (remoteParticipant.AudioTracks != null)
                foreach (var audioTrack in remoteParticipant.AudioTracks)
                    if (!((RemoteTrackPublication)audioTrack.Value).IsSubscribed)
                        ((RemoteTrackPublication)audioTrack.Value).SetSubscribed(true);
    }

    void HandleParticipantDisconnected(Participant remoteParticipant)
    {
        if (_playerSetupsInRangeMap.ContainsKey(remoteParticipant.Sid))
            _playerSetupsInRangeMap.Remove(remoteParticipant.Sid);
    }
    
    void HandleRemoteAudioTrackPublished(TrackPublication publication, Participant participant)
    {
        if (publication.Source == TrackSource.Microphone)
        {
            if (_playerSetupsInRangeMap.ContainsKey(participant.Sid))
            {
                Participant otherParticipant = room.Participants[participant.Sid];

                foreach (var audioTrack in otherParticipant.AudioTracks)
                    if (audioTrack.Value != null)
                        ((RemoteTrackPublication)audioTrack.Value)?.SetSubscribed(true);
            }
        }
    }
    
    void HandleAudioTrackSubscribed(Track track, TrackPublication publication, RemoteParticipant participant)
    {
        if (track.Source == TrackSource.Microphone && track.Kind == TrackKind.Audio &&
            publication is RemoteTrackPublication)
            track.Attach();
    }
    
    void HandleAudioTrackUnsubscribed(Track track, TrackPublication publication, RemoteParticipant participant) =>
        track?.Detach();
    
    void HandleActiveSpeakersChanged(JSArray<Participant> speakers)
    {
        foreach (var setup in _playerSetupsInRangeMap)
        {
            if (setup.Value != null && setup.Value.liveKitSid != null)
            {
                if (room.Participants.TryGetValue(setup.Value.liveKitSid, out RemoteParticipant participant))
                    if (speakers.Contains(participant))
                        setup.Value.ActivateTalkingIndicator();
                    else
                        setup.Value.DeactivateTalkingIndicator();
            }
            else
            {
                Debug.Log("In fn [HandleActiveSpeakersChanged] - Either player or player's LiveKitSid is null.");
            }
        }
    }

    void HandleLocalParticipantSpeakingChanged(bool isSpeaking)
    {
        if (isSpeaking)
            setup.ActivateTalkingIndicator();
        else
            setup.DeactivateTalkingIndicator();
    }

    
    void AddToVolumeChecking(string remoteParticipantId, GameObject playerGameobject)
    {
        var otherPlayerSetup = playerGameobject.GetComponent<Setup>();

        if (otherPlayerSetup)
        {
            _playerSetupsInRangeMap.Add(remoteParticipantId, otherPlayerSetup);
            // otherPlayerSetup.ShowTalkingIndicator();
        }
    }

    void RemoveFromVolumeChecking(string remoteParticipantId, GameObject playerGameobject)
    {
        var otherPlayerSetup = playerGameobject.GetComponent<Setup>();

        if (otherPlayerSetup)
        {
            _playerSetupsInRangeMap.Remove(remoteParticipantId);
            // playerSetup.HideTalkingIndicator();
        }
    }
    
      #region Screen Sharing

    void HandleLocalScreenShareTrackPublished(TrackPublication publication, Participant participant)
    {
        Track track = publication.Track;
        CheckForScreenShare(track, publication, true);
    }


    private void HandleRemoteScreenShareTrackPublished(TrackPublication publication,
        RemoteParticipant participant)
    {
        if (publication.Source == TrackSource.ScreenShare || publication.Source == TrackSource.ScreenShareAudio)
        {
            if (room?.LocalParticipant?.IsScreenShareEnabled == true)
            {
                StartCoroutine(DisableScreenShare());
            }

            SubscribeToPublishedScreenShareTracks(participant);
        }
    }

    void HandleScreenShareTrackSubscribed(Track track, TrackPublication publication, RemoteParticipant participant)
    {
        CheckForScreenShare(track, publication, false);
    }

    private void ToggleScreenShare()
    {
        StartCoroutine(room.LocalParticipant.IsScreenShareEnabled ? DisableScreenShare() : EnableScreenShare());
    }

    private IEnumerator EnableScreenShare()
    {
        ScreenShareCaptureOptions options = new ScreenShareCaptureOptions
        {
            Audio = true
        };

        yield return room?.LocalParticipant?.SetScreenShareEnabled(true, options);
    }

    private IEnumerator DisableScreenShare()
    {
        yield return room?.LocalParticipant?.SetScreenShareEnabled(false);
    }

    void SubscribeToExistingScreenShareTracks()
    {
        foreach (var participant in room.Participants)
        {
            SubscribeToPublishedScreenShareTracks(participant.Value);
        }
    }

    void SubscribeToPublishedScreenShareTracks(RemoteParticipant participant)
    {
        Debug.Log("In [SubscribeToPublishedScreenShareTracks]");

        foreach (var audioTrack in participant.AudioTracks)
        {
            if (audioTrack.Value?.Source == TrackSource.ScreenShareAudio)
                ((RemoteTrackPublication) audioTrack.Value)?.SetSubscribed(true);
        }

        foreach (var videoTrack in participant.VideoTracks)
        {
            if (videoTrack.Value?.Source == TrackSource.ScreenShare)
                ((RemoteTrackPublication) videoTrack.Value)?.SetSubscribed(true);
        }
    }

    private void CheckForScreenShare(Track track, TrackPublication publication, bool isLocalParticipant)
    {
        if (publication.Source == TrackSource.ScreenShare)
        {
            EnableScreenShareVideo(track);
        }

        if (!isLocalParticipant && publication.Source == TrackSource.ScreenShareAudio)
        {
            EnableScreenShareAudio(track, publication);
        }
    }


    private void EnableScreenShareVideo(Track track)
    {
        if (track.Kind != TrackKind.Video) return;

        var video = track.Attach() as HTMLVideoElement;

        if (screenShareUICanvas == null)
            screenShareUICanvas = FindObjectOfType<TVFullScreenUI>();

        if (video == null) return;
        
        try
        {
            var tvObjects = FindObjectsOfType<TV>();
            
            video.VideoReceived += tex =>
            {

                foreach (var tv in tvObjects)
                {
                    if (tv.transform.GetChild(0).TryGetComponent(out MeshRenderer meshRenderer))
                    {
                        meshRenderer.material.mainTexture = tex;
                    }
                }
                Debug.Log("Before set stream content");

                screenShareUICanvas.SetStreamContent(tex);
            };
        }
        catch (Exception e)
        {
            Debug.Log("Exception in getting TVs and setting texture -- " + e.Message);
        }
    }

    private void EnableScreenShareAudio(Track track, TrackPublication publication)
    {
        if (track.Kind == TrackKind.Audio && track.Source == TrackSource.ScreenShareAudio &&
            publication is RemoteTrackPublication)
        {
            var htmlAudioElement = track.Attach() as HTMLAudioElement;
            if (htmlAudioElement != null) htmlAudioElement.Volume = 0.37f;
        }
    }

    #endregion
}