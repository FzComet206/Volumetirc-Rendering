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

    public void Quit()
    {
        Debug.Log("quitting");
        Application.Quit();
    }
}
