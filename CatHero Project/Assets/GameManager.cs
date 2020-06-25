using UnityEngine;

public class GameManager : MonoBehaviour
{
    public bool gameHasEnded = false;
    public bool WinSet = false;
    public bool LoseSet = false;
    // Start is called before the first frame update
    public void Win()
    {
        if(gameHasEnded == false)
        {
            WinSet = true;
            gameHasEnded = true;
            FindObjectOfType<WinScreen>().Pause();
        }
    }

    public void Loss()
    {
        if(gameHasEnded == false)
        {
            LoseSet = true;
            gameHasEnded = true;
            FindObjectOfType<LossScreen>().Pause();
        }
    }
}
