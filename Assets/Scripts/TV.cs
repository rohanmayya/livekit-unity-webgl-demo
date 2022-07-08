using UnityEngine;

public class TV : MonoBehaviour
{
    private TVFullScreenUI screenShareUICanvas;

    private void Awake()
    {
#if UNITY_WEBGL
        screenShareUICanvas = FindObjectOfType<TVFullScreenUI>();
#endif
    }

    private void Update()
    {
        if (Input.GetKey(KeyCode.Z))
        {
            screenShareUICanvas.ToggleScreenSharePanel();
        }
        
        if (Input.GetKey(KeyCode.X))
        {
            OnScreenShareButtonClicked();
        }
    }
    public void OnLeftClicked()
    {
        screenShareUICanvas.ToggleScreenSharePanel();
    }
    


    void OnScreenShareButtonClicked()
    {
#if UNITY_WEBGL
        LiveKitAudio.ToggleScreenShareAction?.Invoke();
#endif
    }
}