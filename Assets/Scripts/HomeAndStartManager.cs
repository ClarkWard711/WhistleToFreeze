using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HomeAndStartManager : MonoBehaviour
{
    [Header("Button UI")]
    public GameObject actionButton; // assign the UI button GameObject (will be SetActive true/false)

    [Header("Snowman Detection")]
    public string snowmanTag = "Snowman"; // tag to detect (optional)
    public GameObject EndPanel;
    public Animator endPanelAnimator; // optional animator on the end panel (trigger "Open")
    public SnowmanManager snowmanManager;
    public NoteManager noteManager;
    public ProgressManager progressManager;

    CircleCollider2D circleCol;

    void Start()
    {
        circleCol = GetComponent<CircleCollider2D>();
        if (circleCol == null)
        {
            Debug.LogWarning($"HomeAndStartManager on '{gameObject.name}' expects a CircleCollider2D; adding one automatically.");
            circleCol = gameObject.AddComponent<CircleCollider2D>();
            circleCol.isTrigger = true;
            circleCol.radius = 1f;
        }
        else
        {
            // ensure it's a trigger so OnTriggerEnter2D/Exit2D fire
            if (!circleCol.isTrigger) circleCol.isTrigger = true;
        }

        if (actionButton != null) actionButton.SetActive(false);
    }

    bool snowmanInside = false;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;
        // accept by tag or by presence of SnowmanManager component anywhere in parent chain
        if (other.CompareTag(snowmanTag) || other.GetComponentInParent<SnowmanManager>() != null)
        {
            snowmanInside = true;
            ShowActionButton(true);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other == null) return;
        if (other.CompareTag(snowmanTag) || other.GetComponentInParent<SnowmanManager>() != null)
        {
            snowmanInside = false;
            ShowActionButton(false);
        }
    }

    void ShowActionButton(bool show)
    {
        if (actionButton != null) actionButton.SetActive(show);
    }

    // Called by the UI button (assign OnClick -> HomeAndStartManager.OnActionButtonPressed)
    public void OnActionButtonPressed()
    {
        // hide the action button
        if (actionButton != null) actionButton.SetActive(false);

        // show the end panel
        if (EndPanel != null)
        {
            EndPanel.SetActive(true);
            if (endPanelAnimator != null)
            {
                endPanelAnimator.SetTrigger("Open");
            }
        }

        // pause snow melting and disable note input while settlement panel is up
        if (snowmanManager != null)
        {
            snowmanManager.pauseMelting = true;
        }
        if (noteManager != null)
        {
            noteManager.allowInput = false;
            if (noteManager.playButton != null) noteManager.playButton.interactable = false;
        }

        // clear pass text and update progress panel text for current day requirement
        if (progressManager != null)
        {
            if (progressManager.passText != null) progressManager.passText.text = "";
            if (progressManager.numberText != null)
            {
                int dayIdx = Mathf.Clamp(progressManager.currentDay, 0, progressManager.requiredNotesPerDay.Count - 1);
                int need = progressManager.GetEffectiveNeedForDay(dayIdx);
                progressManager.numberText.text = $"You need {need} Notes in total to upgrade your house and make it to the next day";
            }
        }

        // ensure Continue is visible and NextDay is hidden when opening the end panel
        if (progressManager != null)
        {
            if (progressManager.continueButton != null) progressManager.continueButton.gameObject.SetActive(true);
            if (progressManager.nextDayButton != null) progressManager.nextDayButton.gameObject.SetActive(false);
        }
    }

    // Called by a button on the EndPanel to refill snow and resume gameplay
    public void OnRefillButtonPressed()
    {
        if (snowmanManager != null)
        {
            snowmanManager.snowAmount = snowmanManager.maxSnow;
            snowmanManager.pauseMelting = false;
            // reset hut timer when refilling
            snowmanManager.StartDayHutTimer();
        }

        if (noteManager != null)
        {
            noteManager.allowInput = true;
            if (noteManager.playButton != null) noteManager.playButton.interactable = true;
        }

        // hide end panel
        if (EndPanel != null) EndPanel.SetActive(false);

        // restore action button visibility if snowman still nearby
        if (actionButton != null) actionButton.SetActive(snowmanInside);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        var col = GetComponent<CircleCollider2D>();
        if (col == null) return;
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawSphere(col.offset, 0.01f);
        Gizmos.DrawWireSphere(col.offset, col.radius);
    }
#endif
}
