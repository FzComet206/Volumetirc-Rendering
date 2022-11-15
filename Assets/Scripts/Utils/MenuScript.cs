using UnityEngine;
using UnityEngine.SceneManagement;


public class MenuScript: MonoBehaviour
{
    public void FluidSim()
    {
        SceneManager.LoadScene(1);
    }
    
    public void NBodySIm()
    {
        SceneManager.LoadScene(2);
    }

    public void SetWindowed()
    {
        Screen.SetResolution(Screen.width, Screen.height, !Screen.fullScreen);
        Debug.Log("toggled fullscreen");
    }
    
    public void SetFHD()
    {
        Screen.SetResolution(1920, 1080, Screen.fullScreen);
        Debug.Log("toggled FHD");
    }
    
    public void SetQHD()
    {
        Screen.SetResolution(2560, 1440, Screen.fullScreen);
        Debug.Log("toggled QHD");
    }

    public void Quit()
    {
        Debug.Log("quitting");
        Application.Quit();
    }
}
