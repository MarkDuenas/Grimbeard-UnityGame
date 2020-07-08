using UnityEngine.SceneManagement;
using UnityEngine;

public class LossScreen : MonoBehaviour
{
    public GameObject LossScreenUI;
    
    // Start is called before the first frame update
    void update()
    {
        // if(FindObjectOfType<GameManager>().LoseSet)
        // {
        //     Pause();
        // }
    }    
    public void Resume()
    {
        LossScreenUI.SetActive(false);
        Time.timeScale = 1f;
        PauseMenu.GameIsPaused = true;
    }
    public void Pause()
    {
        LossScreenUI.SetActive(true);
        Time.timeScale = 0f;
        Cursor.visible = true;
        PauseMenu.GameIsPaused = true;
    }
    public void LoadMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex - 1);
        FindObjectOfType<AudioManager>().Stop("BackgroundMusic");
        FindObjectOfType<AudioManager>().Play("IntroMusic");
        FindObjectOfType<AudioManager>().Stop("BossBattle");
    }
    public void QuitGame()
    {
        Application.Quit();
    }
}
