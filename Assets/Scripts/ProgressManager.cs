using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ProgressManager : MonoBehaviour
{
    [Header("Day Note Requirements")]
    // requiredNotesPerDay[0..2] represent the number of notes required for day 1,2,3 respectively
    public List<int> requiredNotesPerDay = new List<int> { 3, 4, 5 };

    [Header("UI References")]
    public Text numberText;
    public Text passText;

    public NoteManager noteManager;
    public SnowmanManager snowmanManager;
    public Button finishButton;
    public Text scoreText; // optional UI element to display final extra-note score on last-day success
    public GameObject gameOverPanel; // assign a GameOver panel to show on finish
    public GameOver gameOverController; // optional controller that manages a gameOverPanel

    // whether the current day's result was a pass
    public bool hasPassedCurrentDay = false;
    // whether the overall flow has finished (used by finish button)
    public bool isFinished = false;

    // runtime adjusted needs (do not modify source list) - -1 means no adjustment
    private List<int> adjustedNeeds = new List<int>();

    [Header("Buttons")]
    public Button continueButton;
    public Button nextDayButton;
    public MapObjectManager mapObjectManager;
    public HomeAndStartManager homeAndStartManager;

    // current selected day index (0-based)
    public int currentDay = 0;

    void Start()
    {
        // clamp list size to at least 3 entries
        while (requiredNotesPerDay.Count < 3) requiredNotesPerDay.Add(0);

        // initialize adjustedNeeds with -1 (no override)
        adjustedNeeds = new List<int>(requiredNotesPerDay.Count);
        for (int i = 0; i < requiredNotesPerDay.Count; i++) adjustedNeeds.Add(-1);

        UpdateButtonInteractables();

    }

    // Called by Continue button: compute total notes and set passText accordingly
    public void OnContinuePressed()
    {
        int totalCollected = 0;
        if (noteManager != null)
        {
            foreach (var v in noteManager.noteCounts) totalCollected += v;
        }
        int need = ConsumeEffectiveNeedForDay(Mathf.Clamp(currentDay, 0, requiredNotesPerDay.Count - 1));

        if (totalCollected >= need)
        {
            hasPassedCurrentDay = true;
            // passed
            if (currentDay < requiredNotesPerDay.Count - 1)
            {
                int surplus = totalCollected - need;
                // propagate surplus forward across future days and get remaining surplus
                int remaining = ApplySurplusToFutureDays(currentDay + 1, surplus);
                int adjustedNext = GetEffectiveNeedForDay(currentDay + 1);
                passText.text = $"You gained {totalCollected} Notes and make it to the next day. Tomorrow you'll need {adjustedNext} Notes";
                if (remaining > 0)
                {
                    passText.text += $" (you have {remaining} extra Notes left)";
                }
                // hide continue, show nextDay button
                if (continueButton != null) continueButton.gameObject.SetActive(false);
                if (nextDayButton != null) nextDayButton.gameObject.SetActive(true);
            }
            else
            {
                int extra = totalCollected - need;
                passText.text = $"You gained {totalCollected} Notes and make it to the next day. You have {extra} extra Notes";
                if (continueButton != null) continueButton.gameObject.SetActive(false);
                if (nextDayButton != null) nextDayButton.gameObject.SetActive(true);
            }
        }
        else
        {
            hasPassedCurrentDay = false;
            passText.text = "You didn't make it.";
        }

        // If passed, clear collected notes
        if (hasPassedCurrentDay && noteManager != null)
        {
            for (int i = 0; i < noteManager.noteCounts.Length; i++) noteManager.noteCounts[i] = 0;
            noteManager.UpdateResourceUI();
            noteManager.ClearSequence();
        }

        // enable finish button for finalization (either fail or last-day settle)
        if (finishButton != null)
        {
            // allow Finish only when the player failed the current day, or when this is the last day (settlement)
            bool allowFinish = (!hasPassedCurrentDay) || (currentDay >= requiredNotesPerDay.Count - 1);
            finishButton.interactable = allowFinish;
        }
    }

    // Called by external timers (e.g., SnowmanManager) when snow or hut reaches zero
    // If `forceFail` is true the day is treated as failed regardless of collected notes.
    // `reason` can provide a human-readable death reason (e.g., "Snow depleted").
    public void HandleDayEndByTimer(bool forceFail = false, string reason = null)
    {
        int totalCollected = 0;
        if (noteManager != null)
        {
            foreach (var v in noteManager.noteCounts) totalCollected += v;
        }

        int need = ConsumeEffectiveNeedForDay(Mathf.Clamp(currentDay, 0, requiredNotesPerDay.Count - 1));

        bool passed = totalCollected >= need;
        if (forceFail) passed = false;
        string useReason = reason;
        if (string.IsNullOrEmpty(useReason) && !passed) useReason = "You didn't make it.";

        // If this is the last day, always go to game over panel (success if passed)
        bool isLastDay = currentDay >= requiredNotesPerDay.Count - 1;

        if (isLastDay)
        {
            isFinished = true;
            hasPassedCurrentDay = passed;
            int extra = Mathf.Max(0, totalCollected - need);
            if (gameOverController != null)
            {
                gameOverController.ShowGameOver(passed, useReason, extra);
            }
            else
            {
                if (passed)
                {
                    if (passText != null) passText.text = $"You survived! Extra Notes: {extra}";
                    if (scoreText != null) scoreText.text = $"Score: {extra}";
                }
                else
                {
                    if (passText != null) passText.text = useReason;
                    if (scoreText != null) scoreText.text = "Score: 0";
                }
                if (gameOverPanel != null) gameOverPanel.SetActive(true);
            }
            return;
        }

        // Not last day: if failed, go to game over; if passed, behave like Continue
        if (!passed)
        {
            hasPassedCurrentDay = false;
            if (gameOverController != null)
            {
                gameOverController.ShowGameOver(false, useReason, 0);
            }
            else
            {
                if (passText != null) passText.text = useReason;
                if (gameOverPanel != null) gameOverPanel.SetActive(true);
            }
            isFinished = true;
            return;
        }

        // passed and not last day: propagate surplus and show nextDay
        hasPassedCurrentDay = true;
        int surplus = totalCollected - need;
        int remaining = ApplySurplusToFutureDays(currentDay + 1, surplus);
        int adjustedNext = GetEffectiveNeedForDay(currentDay + 1);
        if (passText != null)
        {
            passText.text = $"You gained {totalCollected} Notes and make it to the next day. Tomorrow you'll need {adjustedNext} Notes";
            if (remaining > 0) passText.text += $" (you have {remaining} extra Notes left)";
        }
        if (continueButton != null) continueButton.gameObject.SetActive(false);
        if (nextDayButton != null) nextDayButton.gameObject.SetActive(true);

        // clear collected notes
        if (hasPassedCurrentDay && noteManager != null)
        {
            for (int i = 0; i < noteManager.noteCounts.Length; i++) noteManager.noteCounts[i] = 0;
            noteManager.UpdateResourceUI();
            noteManager.ClearSequence();
        }

        // set finish button state
        if (finishButton != null)
        {
            bool allowFinish = (!hasPassedCurrentDay) || (currentDay >= requiredNotesPerDay.Count - 1);
            finishButton.interactable = allowFinish;
        }
    }

    // Reset game to initial state (called by GameOver.Restart)
    public void ResetGame()
    {
        currentDay = 0;
        hasPassedCurrentDay = false;
        isFinished = false;

        adjustedNeeds = new List<int>(requiredNotesPerDay.Count);
        for (int i = 0; i < requiredNotesPerDay.Count; i++) adjustedNeeds.Add(-1);

        if (noteManager != null)
        {
            for (int i = 0; i < noteManager.noteCounts.Length; i++) noteManager.noteCounts[i] = 0;
            noteManager.UpdateResourceUI();
            noteManager.ClearSequence();
        }

        if (snowmanManager != null)
        {
            snowmanManager.snowAmount = snowmanManager.maxSnow;
            snowmanManager.pauseMelting = false;
            snowmanManager.StartDayHutTimer(0);
            snowmanManager.ResetToInitialPosition();
        }

        if (mapObjectManager != null) mapObjectManager.RegenerateAll();

        if (passText != null) passText.text = "";
        if (numberText != null)
        {
            int need = GetEffectiveNeedForDay(0);
            numberText.text = $"You need {need} Notes in total to upgrade your house and make it to the next day";
        }

        UpdateButtonInteractables();

        // Ensure end/settlement panels are closed and UI buttons reset
        if (finishButton != null) finishButton.interactable = false;
        if (continueButton != null) continueButton.gameObject.SetActive(false);
        if (nextDayButton != null) nextDayButton.gameObject.SetActive(false);

        if (homeAndStartManager != null)
        {
            if (homeAndStartManager.EndPanel != null) homeAndStartManager.EndPanel.SetActive(false);
            if (homeAndStartManager.actionButton != null) homeAndStartManager.actionButton.SetActive(false);
        }
        else
        {
            var ham = FindObjectOfType<HomeAndStartManager>();
            if (ham != null)
            {
                if (ham.EndPanel != null) ham.EndPanel.SetActive(false);
                if (ham.actionButton != null) ham.actionButton.SetActive(false);
            }
        }

        if (noteManager != null)
        {
            noteManager.allowInput = true;
            if (noteManager.playButton != null) noteManager.playButton.interactable = true;
        }

        // Ensure player movement is enabled after reset
        var pc = PlayerController.Instance;
        if (pc != null) pc.allowMovement = true;

        if (gameOverController != null && gameOverController.gameOverPanel != null) gameOverController.gameOverPanel.SetActive(false);
        else if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    // Called by Finish button: used for 'didn't make it' finalization or last-day settlement
    public void OnFinishPressed()
    {
        // mark finished
        isFinished = true;
        int totalCollected = 0;
        if (noteManager != null)
        {
            foreach (var v in noteManager.noteCounts) totalCollected += v;
        }
        int need = ConsumeEffectiveNeedForDay(Mathf.Clamp(currentDay, 0, requiredNotesPerDay.Count - 1));
        bool passed = totalCollected >= need;
        bool isLastDay = currentDay >= requiredNotesPerDay.Count - 1;

        if (gameOverController != null)
        {
            if (isLastDay)
            {
                int extra = Mathf.Max(0, totalCollected - need);
                gameOverController.ShowGameOver(passed, passed ? "" : "You didn't make it.", extra);
            }
            else
            {
                gameOverController.ShowGameOver(false, "End of day.", 0);
            }
        }
        else
        {
            if (isLastDay)
            {
                int extra = Mathf.Max(0, totalCollected - need);
                if (passed)
                {
                    if (passText != null) passText.text = $"You survived! Extra Notes: {extra}";
                    if (scoreText != null) scoreText.text = extra.ToString();
                }
                else
                {
                    if (passText != null) passText.text = "You didn't make it.";
                    if (scoreText != null) scoreText.text = "0";
                }
            }
            else
            {
                if (passText != null) passText.text = "End of day.";
            }
            if (gameOverPanel != null) gameOverPanel.SetActive(true);
        }
    }
    // advance to next day (wired to nextDayButton)
    public void NextDay()
    {
        currentDay = Mathf.Clamp(currentDay + 1, 0, 2);
        UpdateButtonInteractables();
        // regenerate map objects when day advances
        if (mapObjectManager != null)
        {
            mapObjectManager.RegenerateAll();
        }
        // start hut timer for new day if SnowmanManager assigned
        if (snowmanManager != null)
        {
            snowmanManager.StartDayHutTimer(currentDay);
        }
    }

    private void UpdateButtonInteractables()
    {
        if (nextDayButton != null) nextDayButton.interactable = currentDay < 2;
        if (continueButton != null) continueButton.interactable = currentDay >= 0;
    }

    // Apply surplus starting at startDayIndex across subsequent days.
    // Returns remaining surplus after reducing future days (0 if fully consumed).
    private int ApplySurplusToFutureDays(int startDayIndex, int surplus)
    {
        if (surplus <= 0) return surplus;
        int n = requiredNotesPerDay.Count;
        for (int i = startDayIndex; i < n && surplus > 0; i++)
        {
            int currentNeed = GetEffectiveNeedForDay(i);
            if (currentNeed <= 0)
            {
                // already zero or adjusted to zero
                adjustedNeeds[i] = 0;
                continue;
            }
            if (surplus >= currentNeed)
            {
                // fully cover this day's need
                adjustedNeeds[i] = 0;
                surplus -= currentNeed;
            }
            else
            {
                // partially cover
                adjustedNeeds[i] = currentNeed - surplus;
                surplus = 0;
                break;
            }
        }
        return surplus;
    }

    // Return the runtime-effective need for a day (non-destructive). If an adjusted need exists use it, otherwise original.
    public int GetEffectiveNeedForDay(int dayIndex)
    {
        if (dayIndex < 0) dayIndex = 0;
        if (dayIndex >= requiredNotesPerDay.Count) dayIndex = requiredNotesPerDay.Count - 1;
        if (adjustedNeeds != null && dayIndex < adjustedNeeds.Count && adjustedNeeds[dayIndex] >= 0) return adjustedNeeds[dayIndex];
        return requiredNotesPerDay[dayIndex];
    }

    // Consume (read-and-clear) the effective need for the given day. This prevents reuse of the same adjusted value later.
    private int ConsumeEffectiveNeedForDay(int dayIndex)
    {
        int val = GetEffectiveNeedForDay(dayIndex);
        if (adjustedNeeds != null && dayIndex >= 0 && dayIndex < adjustedNeeds.Count) adjustedNeeds[dayIndex] = -1;
        return val;
    }
}

