using UnityEngine; // Unity framework
using UnityEngine.SceneManagement; // Scene management

public class MainMenuUI : MonoBehaviour // Main menu UI controller
{
    public string gameSceneName = "StealthGame"; // Scene to load // Game scene name

    public void PlayGame() // Load game scene
    {
        SceneManager.LoadScene(gameSceneName); // Load scene by name
    }

    public void QuitGame() // Quit application
    {
        Application.Quit(); // Exit game
    }
}
