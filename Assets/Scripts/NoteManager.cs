using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Reflection;

public class NoteManager : MonoBehaviour
{
    [Header("UI Buttons")]

    public Button playButton;

    [Header("Display")]
    // The Panel GameObject that will contain instantiated note prefabs (drag the Panel here in Inspector)
    public GameObject notePanel;
    // Separate prefabs per note id (index 0..2). These prefabs are required now and will be instantiated directly.
    public GameObject[] notePrefabs = new GameObject[3];

    [Header("Behavior")]
    public int maxNotes = 6;
    // Whether input of notes (U/I/O keys) is accepted
    public bool allowInput = true;

    [Header("Melodies (as comma-separated note ids, e.g. 0,1,2)")]
    // Designer can put specific melodies here (order fixed). Example defaults provided.
    public List<string> allowedMelodies = new List<string>
    {
    };

    [Header("Events")]
    // Fired when a melody is matched; passes the matched melody string
    public UnityEvent<string> OnMelodyMatched;
    // Optional: display messages to player (e.g. "No matching melody")
    public Text messageText;
    // UI texts that show remaining counts for each note resource (index 0=红,1=绿,2=蓝)
    public Text[] resourceTexts = new Text[3];

    // Resource counts for each note id (0..2)
    public int[] noteCounts = new int[3] { 5, 5, 5 };
    // message timing: how long to hold the text before starting fade, and fade duration
    public float messageHoldDuration = 2.5f;
    public float messageFadeDuration = 1f;
    private Coroutine messageCoroutine;
    // GameObjects that will be unlocked when the corresponding melody is played.
    // Each element corresponds to the melody at the same index in `allowedMelodies`.
    public GameObject[] melodyUnlockObjects;
    // Which melody index is initially unlocked (0-based). Default 2 (third melody).
    public int startingUnlockedIndex = 2;

    // runtime unlocked flags
    private bool[] melodyUnlocked;

    // References for applying effects
    public SnowmanManager snowmanManager;
    // movement target whose speed will be increased (optional)
    public GameObject movementTarget;
    public float speedMultiplier = 1.5f; // default speed increase

    private Coroutine speedCoroutine;
    private List<Coroutine> tempCoroutines = new List<Coroutine>();

    // internal sequence (0,1,2 for the three buttons)
    private readonly List<int> sequence = new List<int>();

    private readonly Color[] noteColors = new Color[] { Color.red, Color.green, Color.blue };

    private void Start()
    {
        if (playButton != null) playButton.onClick.AddListener(PlaySequence);
        UpdateDisplay();
        UpdateResourceUI();

        // initialize unlocked state
        melodyUnlocked = new bool[allowedMelodies.Count];
        for (int i = 0; i < melodyUnlocked.Length; i++) melodyUnlocked[i] = false;
        if (startingUnlockedIndex >= 0 && startingUnlockedIndex < melodyUnlocked.Length) melodyUnlocked[startingUnlockedIndex] = true;

        // apply activation state to provided unlock objects
        if (melodyUnlockObjects != null)
        {
            for (int i = 0; i < melodyUnlockObjects.Length && i < melodyUnlocked.Length; i++)
            {
                var go = melodyUnlockObjects[i];
                if (go == null) continue;
                SetChildrenActive(go, melodyUnlocked[i]);
            }
        }

        // If movementTarget wasn't assigned in Inspector, try to auto-find the PlayerController singleton.
        if (movementTarget == null)
        {
            var pc = PlayerController.Instance;
            if (pc != null) movementTarget = pc.gameObject;
        }
    }

    private void Update()
    {
        if (!allowInput) return;
        if (Input.GetKeyDown(KeyCode.U)) AddNoteAndCheck(0);
        if (Input.GetKeyDown(KeyCode.I)) AddNoteAndCheck(1);
        if (Input.GetKeyDown(KeyCode.O)) AddNoteAndCheck(2);

    }

    private void AddNoteAndCheck(int noteId)
    {
        sequence.Add(noteId);
        // keep only last maxNotes
        while (sequence.Count > maxNotes) sequence.RemoveAt(0);

        UpdateDisplay();
        DebugLogSequence();
    }
    private void UpdateDisplay()
    {

        if (notePanel == null)
        {
            Debug.LogWarning("NoteManager: notePanel is not assigned.");
            return;
        }

        var noteContainer = notePanel.transform;

        // clear existing (use Destroy during Play mode, DestroyImmediate in Editor)
        for (int i = noteContainer.childCount - 1; i >= 0; i--)
        {
            var childGo = noteContainer.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(childGo);
            else DestroyImmediate(childGo);
        }

        // instantiate notes in order
        for (int i = 0; i < sequence.Count; i++)
        {
            int id = sequence[i];
            GameObject prefabToUse = null;
            if (id >= 0 && id < notePrefabs.Length) prefabToUse = notePrefabs[id];
            if (prefabToUse == null)
            {
                Debug.LogWarning($"NoteManager: no prefab configured for note id {id}.");
                continue;
            }

            var go = Instantiate(prefabToUse);
            go.transform.SetParent(noteContainer, false);
            go.transform.localScale = Vector3.one;
            go.name = $"Note_{i}_{id}";
            var img = go.GetComponent<UnityEngine.UI.Image>();
            if (img != null)
            {
                // If prefab has an Image, leave its visuals as authored.
            }
        }
    }

    private void CheckForMelodyMatch()
    {
        if (sequence.Count == 0) return;

        foreach (var melody in GetOrderedMelodies())
        {
            if (string.IsNullOrWhiteSpace(melody)) continue;
            var parts = melody.Split(',').Select(s => s.Trim()).Where(s => s != "").ToArray();
            int mlen = parts.Length;
            if (mlen == 0) continue;

            // require exact length match: only match when melody length equals current sequence length
            if (mlen != sequence.Count) continue;

            bool match = true;
            for (int i = 0; i < mlen; i++)
            {
                int expected;
                if (!int.TryParse(parts[i], out expected)) { match = false; break; }
                int seqIndex = sequence.Count - mlen + i;
                if (sequence[seqIndex] != expected) { match = false; break; }
            }

            if (match)
            {
                // Matched a melody. Fire event and return (only first match).
                OnMelodyMatched?.Invoke(melody);
                Debug.Log($"Melody matched: {melody}");
                // Do not show matched melody in UI per design.
                return;
            }
        }
    }

    // Called when the user presses the play button: check last notes and show message if none matched
    public void PlaySequence()
    {
        // refresh resource UI when Play starts
        UpdateResourceUI();

        if (sequence.Count == 0)
        {
            ShowMessage("No notes");
            ClearSequence();
            return;
        }

        // Try to match any allowed melody against the suffix of the sequence
        foreach (var melody in GetOrderedMelodies())
        {
            if (string.IsNullOrWhiteSpace(melody)) continue;
            var parts = melody.Split(',').Select(s => s.Trim()).Where(s => s != "").ToArray();
            int mlen = parts.Length;
            if (mlen == 0) continue;

            // require exact length match: only match when melody length equals current sequence length
            if (mlen != sequence.Count) continue;

            bool match = true;
            for (int i = 0; i < mlen; i++)
            {
                int expected;
                if (!int.TryParse(parts[i], out expected)) { match = false; break; }
                int seqIndex = sequence.Count - mlen + i;
                if (sequence[seqIndex] != expected) { match = false; break; }
            }

            if (match)
            {
                // Count required notes for this melody
                var ids = parts.Select(p => int.Parse(p)).ToArray();
                int[] need = new int[3];
                foreach (var nid in ids) if (nid >= 0 && nid < need.Length) need[nid]++;

                // Check availability
                bool enough = true;
                for (int k = 0; k < need.Length; k++) if (need[k] > noteCounts[k]) { enough = false; break; }

                if (!enough)
                {
                    Debug.Log("Not enough notes");
                    ShowMessage("Not enough notes");
                    // TODO: show a clearer in-game feedback for insufficient notes (popup/animation/sfx)
                    ClearSequence();
                    return; // don't consume resources
                }

                // Consume notes
                for (int k = 0; k < need.Length; k++) noteCounts[k] -= need[k];
                UpdateResourceUI();

                OnMelodyMatched?.Invoke(melody);
                // Try to unlock the matched melody (if applicable)
                int melodyIndex = allowedMelodies.IndexOf(melody);
                if (melodyIndex >= 0) TryUnlockMelody(melodyIndex);
                if (melodyIndex >= 0) ApplyMelodyEffect(melodyIndex);
                Debug.Log($"Melody matched on Play: {melody}");
                // Do not show matched melody in UI per design.
                ClearSequence();
                return;
            }
        }

        ShowMessage("No matching melody");
        Debug.Log("No matching melody on Play");
        // TODO: 当没有对应乐谱时，添加更显眼的玩家提示（例如声音/高亮/反馈）
        ClearSequence();
    }

    // Return melodies ordered by descending length (number of note ids), preserving original order for ties
    private IEnumerable<string> GetOrderedMelodies()
    {
        return allowedMelodies
            .Select((m, idx) => new { Melody = m, Index = idx, Length = m.Split(',').Select(s => s.Trim()).Count(s => s != "") })
            .OrderByDescending(x => x.Length)
            .ThenBy(x => x.Index)
            .Select(x => x.Melody);
    }

    // Utility: get sequence as comma-separated string
    public string GetSequenceString()
    {
        if (sequence.Count == 0) return "(empty)";
        return string.Join(",", sequence.Select(i => i.ToString()).ToArray());
    }

    // Utility: print current sequence to Debug.Log (can be called from Inspector)
    public void DebugLogSequence()
    {
        Debug.Log("Current melody: " + GetSequenceString());
    }

    // Update the resource UI texts from `noteCounts` (one Text per color)
    public void UpdateResourceUI()
    {
        if (resourceTexts == null) return;
        for (int i = 0; i < resourceTexts.Length && i < noteCounts.Length; i++)
        {
            if (resourceTexts[i] == null) continue;
            resourceTexts[i].text = noteCounts[i].ToString();
        }
    }

    // Public API to add notes to a specific note id (used by in-world pickups)
    public void AddNotes(int noteId, int amount)
    {
        if (noteId < 0 || noteId >= noteCounts.Length) return;
        if (amount <= 0) return;
        noteCounts[noteId] += amount;
        UpdateResourceUI();
    }

    // Show a message in the UI: hold it visible for `holdDuration` seconds, then fade out over `fadeDuration` seconds.
    // If either parameter is negative, the corresponding default field is used.
    public void ShowMessage(string text, float holdDuration = -1f, float fadeDuration = -1f)
    {
        if (messageText == null) return;
        if (messageCoroutine != null) StopCoroutine(messageCoroutine);
        messageText.text = text;
        var col = messageText.color;
        col.a = 1f;
        messageText.color = col;
        float useHold = holdDuration > 0f ? holdDuration : messageHoldDuration;
        float useFade = fadeDuration > 0f ? fadeDuration : messageFadeDuration;
        messageCoroutine = StartCoroutine(FadeMessage(useHold, useFade));
    }

    private IEnumerator FadeMessage(float holdSeconds, float fadeSeconds)
    {
        if (messageText == null) yield break;
        // hold fully visible
        float t = 0f;
        while (t < holdSeconds)
        {
            t += Time.deltaTime;
            yield return null;
        }
        // then fade over fadeSeconds
        if (fadeSeconds <= 0f)
        {
            // immediate clear
            var endc0 = messageText.color;
            endc0.a = 0f;
            messageText.color = endc0;
            messageText.text = "";
            messageCoroutine = null;
            yield break;
        }
        t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(1f, 0f, t / fadeSeconds);
            var c = messageText.color;
            c.a = a;
            messageText.color = c;
            yield return null;
        }
        var endc = messageText.color;
        endc.a = 0f;
        messageText.color = endc;
        messageText.text = "";
        messageCoroutine = null;
    }

    // Try to unlock melody at index; when unlocking, set all children of the provided GameObject active.
    private void TryUnlockMelody(int index)
    {
        if (index < 0 || index >= melodyUnlocked.Length) return;
        if (melodyUnlocked[index]) return; // already unlocked

        melodyUnlocked[index] = true;
        if (melodyUnlockObjects != null && index < melodyUnlockObjects.Length)
        {
            var go = melodyUnlockObjects[index];
            if (go != null) SetChildrenActive(go, true);
        }
        Debug.Log($"Melody unlocked: index {index}");
    }

    // Apply gameplay effects for a melody index
    private void ApplyMelodyEffect(int index)
    {
        switch (index)
        {
            case 0: // first melody: increase movement speed for 30s
                if (movementTarget != null) StartSpeedBoost(speedMultiplier, 30f);
                ShowMessage("Speed Boost (30s)");
                break;
            case 1: // second melody: decrease temperature by 1 level for 30s
                if (snowmanManager != null) StartTempDelta(-1, 30f);
                ShowMessage("Temperature -1 Lv (30s)");
                break;
            case 2: // third melody: restore 10% snow
                if (snowmanManager != null) snowmanManager.AddSnow(snowmanManager.maxSnow * 0.10f * 2f);
                ShowMessage("Restore Snow 20%");
                break;
            case 3: // fourth melody: reserved
                if (snowmanManager != null) snowmanManager.AddSnow(snowmanManager.maxSnow * 0.20f * 2f);
                ShowMessage("Restore Snow 40%");
                break;
            case 4: // fifth melody: apply 1,2,3 simultaneously
                if (movementTarget != null) StartSpeedBoost(speedMultiplier, 30f);
                if (snowmanManager != null) StartTempDelta(-1, 30f);
                if (snowmanManager != null) snowmanManager.AddSnow(snowmanManager.maxSnow * 0.10f * 2f);
                ShowMessage("Speed Boost + Temperature -1 Lv + Restore Snow 20%");
                break;
            case 5: // sixth melody: decrease temperature by 2 levels for 30s
                if (snowmanManager != null) StartTempDelta(-2, 30f);
                ShowMessage("Temperature -2 Lv (30s)");
                break;
            case 6: // seventh melody: restore 30% snow
                if (snowmanManager != null) snowmanManager.AddSnow(snowmanManager.maxSnow * 0.30f * 2f);
                ShowMessage("Restore Snow 60%");
                break;
            default:
                break;
        }
    }

    private void StartSpeedBoost(float multiplier, float duration)
    {
        if (speedCoroutine != null) StopCoroutine(speedCoroutine);
        speedCoroutine = StartCoroutine(SpeedBoostCoroutine(multiplier, duration));
    }

    private IEnumerator SpeedBoostCoroutine(float multiplier, float duration)
    {
        if (movementTarget == null) yield break;

        // Prefer explicit PlayerController handling when present (safer than reflection)
        float original = 0f;
        var pc = movementTarget.GetComponent<PlayerController>();
        if (pc != null)
        {
            original = pc.speed;
            pc.speed = original * multiplier;
        }
        else
        {
            // Fallback: try reflection across components for common field/property names
            Component targetComp = null;
            FieldInfo field = null;
            PropertyInfo prop = null;

            var comps = movementTarget.GetComponents<Component>();
            foreach (var c in comps)
            {
                var t = c.GetType();
                field = t.GetField("moveSpeed") ?? t.GetField("speed") ?? t.GetField("MoveSpeed");
                if (field != null && field.FieldType == typeof(float)) { targetComp = c; break; }
                prop = t.GetProperty("moveSpeed") ?? t.GetProperty("speed") ?? t.GetProperty("MoveSpeed");
                if (prop != null && prop.PropertyType == typeof(float)) { targetComp = c; break; }
            }

            if (targetComp == null) yield break;

            if (field != null)
            {
                original = (float)field.GetValue(targetComp);
                field.SetValue(targetComp, original * multiplier);
            }
            else if (prop != null)
            {
                original = (float)prop.GetValue(targetComp);
                prop.SetValue(targetComp, original * multiplier);
            }
            else yield break;
        }

        yield return new WaitForSeconds(duration);

        // restore
        var pcRestore = movementTarget.GetComponent<PlayerController>();
        if (pcRestore != null)
        {
            pcRestore.speed = original;
        }
        else
        {
            // restore via reflection if necessary
            Component targetComp = null;
            FieldInfo field = null;
            PropertyInfo prop = null;
            var comps = movementTarget.GetComponents<Component>();
            foreach (var c in comps)
            {
                var t = c.GetType();
                field = t.GetField("moveSpeed") ?? t.GetField("speed") ?? t.GetField("MoveSpeed");
                if (field != null && field.FieldType == typeof(float)) { targetComp = c; break; }
                prop = t.GetProperty("moveSpeed") ?? t.GetProperty("speed") ?? t.GetProperty("MoveSpeed");
                if (prop != null && prop.PropertyType == typeof(float)) { targetComp = c; break; }
            }
            if (targetComp != null)
            {
                if (field != null) field.SetValue(targetComp, original);
                else if (prop != null) prop.SetValue(targetComp, original);
            }
        }
        speedCoroutine = null;
    }

    private void StartTempDelta(int delta, float duration)
    {
        var co = StartCoroutine(TempDeltaCoroutine(delta, duration));
        tempCoroutines.Add(co);
    }

    private IEnumerator TempDeltaCoroutine(int delta, float duration)
    {
        if (snowmanManager == null) yield break;
        // Use SnowmanManager's temporary override so tilemap detection won't immediately overwrite it
        snowmanManager.ApplyTempDelta(delta, duration);
        yield break;
    }

    private void SetChildrenActive(GameObject parent, bool active)
    {
        if (parent == null) return;
        for (int i = 0; i < parent.transform.childCount; i++)
        {
            var child = parent.transform.GetChild(i).gameObject;
            if (child == null) continue;

            // Prefer toggling Image component visibility so object hierarchy remains intact
            var img = child.GetComponent<UnityEngine.UI.Image>();
            if (img != null)
            {
                img.enabled = active;
                continue;
            }

            // Try to find an Image in descendants
            var imgDesc = child.GetComponentInChildren<UnityEngine.UI.Image>(true);
            if (imgDesc != null)
            {
                imgDesc.enabled = active;
                continue;
            }

            // Fallback removed: do not toggle GameObject active state; leave object as authored.
            // If no Image exists, we don't modify the child's active state.
        }
    }

    // Public helper: clear the current sequence and display
    public void ClearSequence()
    {
        sequence.Clear();
        UpdateDisplay();
    }
}
