using UnityEngine.SceneManagement;
using UnityEngine;

public class WinScreen : MonoBehaviour
{
    public GameObject WinScreenUI;
    // Start is called before the first frame update
    void update()
    {
        // if(FindObjectOfType<GameManager>().WinSet)
        // {
        //     Pause();
        // }
    }    
    public void Resume()
    {
        WinScreenUI.SetActive(false);
        Time.timeScale = 1f;
        PauseMenu.GameIsPaused = true;
    }
    public void Pause()
    {
        WinScreenUI.SetActive(true);
        Time.timeScale = 0f;
        PauseMenu.GameIsPaused = true;
    }
    public void LoadMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex - 1);
        FindObjectOfType<AudioManager>().Stop("BackgroundMusic");
        FindObjectOfType<AudioManager>().Play("IntroMusic");
    }
    public void QuitGame()
    {
        Application.Quit();
    }
}
