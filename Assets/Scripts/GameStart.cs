using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameStart : MonoBehaviour
{
    [Header("Intro UI")]
    public Button continueButton;
    public Text continueButtonText; // assign the Text element used in the button
    public Text introText; // area to show intro/tutorial pages
    public GameObject introContainer; // assign the GameObject (e.g. panel) to disable when Start is pressed

    [Header("Gameplay References")]
    public NoteManager noteManager;
    public SnowmanManager snowmanManager;
    public MapObjectManager mapObjectManager;

    private string[] introPages = new string[] {
        "You play as a snowman with a magical whistle, trying to survive for three days on this land while collecting musical notes scattered across the map. There are three regions you can explore: the snowfield, the forest, and the desert, with temperatures increasing from low to high. By using the notes you collect to compose melodies, you can gain abilities that help you explore more effectively.",
        "On the right side of the screen, the UI shows your health bar, the current temperature level, the number of notes collected, and sheet music (which can be unlocked). Your health gradually decreases as the temperature rises. You can also try composing melodies by pressing the U, I, and O keys, which correspond to the red, green, and blue notes respectively. As you play notes, the melody will appear on the right side of the screen. At the start of the game, one melody is already unlocked: three blue notes (pressing O three times). At the top of the screen, a timer shows the remaining time before your snow hut melts.\n\nYou also have a snow hut. Each day you need to upgrade it, but it slowly melts over time. This limits how long you can stay outside exploring each day. Upgrading the snow hut allows you to stay out longer and explore for a greater amount of time on the following days."
    };

    private int introIndex = 0;

    void Start()
    {
        // initialize intro UI
        if (introText != null) introText.text = introPages.Length > 0 ? introPages[0] : "";
        if (continueButtonText != null) continueButtonText.text = "Continue";
        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(HandleContinueClicked);
        }

        // disable gameplay input and melting until player starts
        if (noteManager != null)
        {
            noteManager.allowInput = false;
            if (noteManager.playButton != null) noteManager.playButton.interactable = false;
        }
        if (snowmanManager != null)
        {
            snowmanManager.pauseMelting = true;
        }

        // disable player movement during intro if PlayerController exists
        if (PlayerController.Instance != null)
        {
            PlayerController.Instance.allowMovement = false;
        }

        // ensure intro container is visible at start
        if (introContainer != null) introContainer.SetActive(true);
    }

    void HandleContinueClicked()
    {
        if (introIndex < introPages.Length - 1)
        {
            introIndex++;
            if (introText != null) introText.text = introPages[introIndex];
            if (introIndex == introPages.Length - 1 && continueButtonText != null) continueButtonText.text = "Start";
            return;
        }

        // last page pressed -> start game
        StartGame();
    }

    void StartGame()
    {
        // hide intro UI
        if (introText != null) introText.gameObject.SetActive(false);
        if (continueButton != null) continueButton.gameObject.SetActive(false);
        if (introContainer != null) introContainer.SetActive(false);

        // enable gameplay input and melting
        if (noteManager != null)
        {
            noteManager.allowInput = true;
            if (noteManager.playButton != null) noteManager.playButton.interactable = true;
        }
        if (snowmanManager != null)
        {
            snowmanManager.pauseMelting = false;
        }
        // start the hut timer for the current day
        if (snowmanManager != null)
        {
            snowmanManager.StartDayHutTimer();
        }
        if (PlayerController.Instance != null)
        {
            PlayerController.Instance.allowMovement = true;
        }
        if (mapObjectManager != null)
        {
            mapObjectManager.GenerateAll();
        }
    }
}
