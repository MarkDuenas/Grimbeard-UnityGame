using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    void Update() {
        PauseMenu.GameIsPaused = false;
    }
    
    // Start is called before the first frame update
    public void PlayGame()
    {
        FindObjectOfType<GameManager>().gameHasEnded = false;
        FindObjectOfType<GameManager>().WinSet = false;
        FindObjectOfType<GameManager>().LoseSet = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
        FindObjectOfType<AudioManager>().Stop("IntroMusic");
        FindObjectOfType<AudioManager>().Play("BackgroundMusic");
        
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
