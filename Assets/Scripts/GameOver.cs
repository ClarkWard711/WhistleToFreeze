using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GameOver : MonoBehaviour
{
    public Text resultText; // main message
    public Text scoreText; // optional score display (extra notes)
    public Button restartButton;
    public Button quitButton;
    public ProgressManager progressManager;
    public GameObject gameOverPanel; // the actual UI panel GameObject (controller will show/hide this)

    void Start()
    {
        if (restartButton != null)
        {
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(OnRestartClicked);
        }
        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(QuitGame);
        }
    }

    // Show the game over panel. success=true means player survived final day.
    public void ShowGameOver(bool success, string reason, int extraNotes = 0)
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        else gameObject.SetActive(true);
        if (resultText != null)
        {
            if (success)
            {
                resultText.text = "Success!";
            }
            else
            {
                resultText.text = string.IsNullOrEmpty(reason) ? "You didn't make it." : reason;
            }
        }
        if (scoreText != null)
        {
            scoreText.text = success ? $"Score: {extraNotes}" : "Score: 0";
        }
    }

    public void OnRestartClicked()
    {
        // Hide panel first
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        else gameObject.SetActive(false);
        // Ask ProgressManager to reset game state
        if (progressManager == null)
        {
            progressManager = FindObjectOfType<ProgressManager>();
        }
        if (progressManager != null)
        {
            progressManager.ResetGame();
        }
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
