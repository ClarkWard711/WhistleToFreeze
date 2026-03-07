using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

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

    [Header("Melodies (as comma-separated note ids, e.g. 0,1,2)")]
    // Designer can put specific melodies here (order fixed). Example defaults provided.
    public List<string> allowedMelodies = new List<string>
    {
    };

    [Header("Events")]
    // Fired when a melody is matched; passes the matched melody string
    public UnityEvent<string> OnMelodyMatched;
    // Optional: display messages to player (e.g. "没对应乐谱")
    public Text messageText;
    // UI texts that show remaining counts for each note resource (index 0=红,1=绿,2=蓝)
    public Text[] resourceTexts = new Text[3];

    // Resource counts for each note id (0..2)
    public int[] noteCounts = new int[3] { 5, 5, 5 };
    // GameObjects that will be unlocked when the corresponding melody is played.
    // Each element corresponds to the melody at the same index in `allowedMelodies`.
    public GameObject[] melodyUnlockObjects;
    // Which melody index is initially unlocked (0-based). Default 2 (third melody).
    public int startingUnlockedIndex = 2;

    // runtime unlocked flags
    private bool[] melodyUnlocked;

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
    }

    private void Update()
    {
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
                if (messageText != null) messageText.text = "匹配成功: " + melody;
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
            if (messageText != null) messageText.text = "没有音符";
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
                    Debug.Log("音符不够");
                    if (messageText != null) messageText.text = "音符不够";
                    // TODO: 提示玩家音符不足的反馈（弹窗/动画/音效），并允许用户调整
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
                Debug.Log($"Melody matched on Play: {melody}");
                if (messageText != null) messageText.text = "匹配成功: " + melody;
                ClearSequence();
                return;
            }
        }

        if (messageText != null) messageText.text = "没对应乐谱";
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
        if (sequence.Count == 0) return "(空)";
        return string.Join(",", sequence.Select(i => i.ToString()).ToArray());
    }

    // Utility: print current sequence to Debug.Log (can be called from Inspector)
    public void DebugLogSequence()
    {
        Debug.Log("当前乐谱: " + GetSequenceString());
        if (messageText != null) messageText.text = "当前乐谱: " + GetSequenceString();
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
