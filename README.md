# LiveKit with Unity WebGL - Spatial Audio
This repository will guide you setting up LiveKit for your browser based game Unity game. (WebGL builds only.)

![Screenshot 2022-05-09 at 9 20 05 PM](https://user-images.githubusercontent.com/32911377/167449380-d54e3ad2-b9db-4db9-9dcf-ff951b5923fa.png)

Note the classes:
- LiveKitSetup, on the root of the player prefab,
- LiveKitAudio, this is where connecting to a room & all the other callbacks are/will be registered.
- The setup script enables the audio script on only the local clients, but the sphere colliders are enabled for all other clients.


