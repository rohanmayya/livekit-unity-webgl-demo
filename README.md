# LiveKit with Unity WebGL - Spatial Audio
This repository will guide you setting up LiveKit for your browser based game Unity game. (WebGL builds only.)

![Screenshot 2022-05-09 at 9 20 05 PM](https://user-images.githubusercontent.com/32911377/167449380-d54e3ad2-b9db-4db9-9dcf-ff951b5923fa.png)

Note the classes:
- LiveKitSetup, on the root of the player prefab,
- LiveKitAudio, this is where connecting to a room & all the other callbacks are/will be registered.
- The setup script enables the audio script on only the local clients, but the sphere colliders are enabled for all other clients.

There are 2 touchpoints for spatial audio:
1. When a player's sphere collider overlaps with another (and their audio tracks are already published) player's sphere collider, we add them to the local player's rangeList and also subscribe to their tracks,
2. When a player publishes their audio tracks, all other players subscribe to their tracks *if* that player happens to be near them.

If you take care of these 2 cases, you will have a neat, working spatial audio implementation, barring edge cases that you'll have to account for.

A few steps in Unity to make sure everything works correctly:


- Ensure in Player Settings -> Physics, LiveKitAudio layer only interacts with LiveKitAudio layer.

<img width="308" alt="Screenshot 2022-06-07 at 7 04 21 PM" src="https://user-images.githubusercontent.com/32911377/172393491-977e6b7d-c6b3-415a-aaca-798611516c67.png">

- The sphere collider on the LiveKitAudio Game Object on the player prefab must be disabled.
- Assign the sphere collider of this GameObject into the Setup script on the root of the player prefab.
