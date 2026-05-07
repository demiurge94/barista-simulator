using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    bool paused = false;
    public GameObject pauseMenu;

    void Start()
    {
        Time.timeScale = 1.0f;
        pauseMenu.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            paused = !paused;

            if (paused == true) { PauseGame(); }
            else { ResumeGame(); }
        }
    }

    public void ResumeGame()
    {
        Time.timeScale = 1.0f;
        pauseMenu.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void PauseGame()
    {
        Time.timeScale = 0.0f;
        pauseMenu.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void GoToMenu()
    {
        SceneManager.LoadScene(0);
    }
}
