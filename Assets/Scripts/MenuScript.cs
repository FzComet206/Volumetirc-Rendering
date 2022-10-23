using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuScript: MonoBehaviour
{
    private void Start()
    {
        Debug.Log("script working");
    }

    public void FluidSim()
    {
        SceneManager.LoadScene(1);
    }
    
    public void NBodySIm()
    {
        SceneManager.LoadScene(2);
    }

    public void Options()
    {
        SceneManager.LoadScene(3);
    }

    public void Quit()
    {
        Debug.Log("quitting");
        Application.Quit();
    }
}
