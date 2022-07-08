using UnityEngine;
using UnityEngine.UI;

public class TVFullScreenUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private AspectRatioFitter aspectRatioFitter;
    [SerializeField] private RawImage canvasImageRaw;
    
    public void ToggleScreenSharePanel()
    {
        panel.SetActive(!panel.activeSelf);
    }

    public void SetStreamContent(Texture2D tex)
    {
        if (tex && tex.height > 0 && tex.width > 0)
        {
            aspectRatioFitter.aspectRatio = (float)tex.width / tex.height;
            canvasImageRaw.texture = tex;
        }
    }
}