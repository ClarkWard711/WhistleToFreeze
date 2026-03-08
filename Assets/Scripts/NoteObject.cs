using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// When a snowman enters this object's CircleCollider2D (IsTrigger=true),
// find a child SpriteRenderer on the snowman (by name or first found) and
// make it visible by changing its alpha. When the snowman exits, restore
// the original alpha.
public class NoteObject : MonoBehaviour
{
    [Header("Snowman Detection")]
    // Optional: identify snowman by tag if it doesn't have SnowmanManager component
    public string snowmanTag = "Snowman";

    [Header("Note Sprite Lookup")]
    // substring to match the sprite GameObject's name (case-insensitive). If empty, first SpriteRenderer is used.
    public string noteSpriteName = "Note";

    [Header("Alpha Values")]
    public float visibleAlpha = 1f;
    public float hiddenAlpha = 0f; // used if restoring original not available

    [Header("Collectible")]
    // Whether this object can be collected by pressing E while inside the trigger
    public bool collectible = true;
    // Select the note color/state in the Inspector
    public NoteColor noteColor = NoteColor.Red;
    // How many notes are granted when collected
    public int dropAmount = 1;
    // After collection: destroy or disable the object
    public bool destroyOnCollect = true;
    public bool disableOnCollect = false;

    // Event invoked when collected; passes note id (0=Red,1=Green,2=Blue)
    public UnityEvent<int> OnCollected;

    // track original colors so we can restore on exit/disable
    private readonly Dictionary<SpriteRenderer, Color> originalColors = new Dictionary<SpriteRenderer, Color>();

    // track whether player is inside trigger
    private bool playerInside = false;
    private Transform playerTransform;
    private bool collected = false;

    public enum NoteColor { Red = 0, Green = 1, Blue = 2 }
    [Header("Press Prompt Lookup")]
    // name substring to match the press-E sprite on the target (snowman)
    // e.g. "PressE" or "press_icon". If empty, fallback tries name contains "press".
    public string pressSpriteName = "PressE";
    [Header("Decorative")]
    // If true this object is only decorative: it cannot be collected and any CircleCollider2D on it/children will be disabled.
    public bool decorativeOnly = false;
    [Header("Collection Mode")]
    // When set to AllColors, collecting grants `dropAmount` for each of the three colors.
    public CollectMode collectMode = CollectMode.Single;

    public enum CollectMode { Single = 0, AllColors = 1 }

    private SpriteRenderer FindNoteSprite(Transform target)
    {
        if (target == null) return null;
        var srs = target.GetComponentsInChildren<SpriteRenderer>(true);
        if (srs == null || srs.Length == 0) return null;

        if (!string.IsNullOrEmpty(noteSpriteName))
        {
            string lower = noteSpriteName.ToLower();
            foreach (var sr in srs)
            {
                if (sr == null) continue;
                if (sr.gameObject.name.ToLower().Contains(lower)) return sr;
            }
        }

        // fallback to first SpriteRenderer
        return srs[0];
    }

    private SpriteRenderer FindPressSprite(Transform target)
    {
        if (target == null) return null;
        var srs = target.GetComponentsInChildren<SpriteRenderer>(true);
        if (srs == null || srs.Length == 0) return null;

        if (!string.IsNullOrEmpty(pressSpriteName))
        {
            string lower = pressSpriteName.ToLower();
            foreach (var sr in srs)
            {
                if (sr == null) continue;
                if (sr.gameObject.name.ToLower().Contains(lower)) return sr;
            }
        }

        // fallback: look for any child whose name contains "press"
        foreach (var sr in srs)
        {
            if (sr == null) continue;
            if (sr.gameObject.name.ToLower().Contains("press")) return sr;
        }

        return null;
    }

    private void SetSpriteAlpha(SpriteRenderer sr, float alpha)
    {
        if (sr == null) return;
        var c = sr.color;
        c.a = Mathf.Clamp01(alpha);
        sr.color = c;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (decorativeOnly) return;
        if (other == null) return;

        // Accept if it has SnowmanManager or matches tag
        var sm = other.GetComponent<SnowmanManager>();
        if (sm == null && !other.CompareTag(snowmanTag)) return;

        var sr = FindNoteSprite(other.transform);
        if (sr == null) return;

        if (!originalColors.ContainsKey(sr)) originalColors[sr] = sr.color;
        SetSpriteAlpha(sr, visibleAlpha);
        // mark player inside so Update() can listen for E
        playerInside = true;
        playerTransform = other.transform;
        // try to show a "press E" sprite on the target (snowman)
        var pressSr = FindPressSprite(other.transform);

        if (pressSr != null)
        {
            if (!originalColors.ContainsKey(pressSr)) originalColors[pressSr] = pressSr.color;
            SetSpriteAlpha(pressSr, visibleAlpha);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (decorativeOnly) return;
        if (other == null) return;

        var sm = other.GetComponent<SnowmanManager>();
        if (sm == null && !other.CompareTag(snowmanTag)) return;

        var sr = FindNoteSprite(other.transform);
        if (sr == null) return;

        if (originalColors.TryGetValue(sr, out var orig))
        {
            sr.color = orig;
            originalColors.Remove(sr);
        }
        else
        {
            // fallback: hide
            SetSpriteAlpha(sr, hiddenAlpha);
        }
        // also hide/restore press sprite on the target
        var pressSrExit = FindPressSprite(other.transform);
        if (pressSrExit != null)
        {
            if (originalColors.TryGetValue(pressSrExit, out var origP))
            {
                pressSrExit.color = origP;
                originalColors.Remove(pressSrExit);
            }
            else
            {
                SetSpriteAlpha(pressSrExit, hiddenAlpha);
            }
        }
        // leaving trigger
        if (playerTransform != null && other.transform == playerTransform)
        {
            playerInside = false;
            playerTransform = null;
        }
    }

    private void OnDisable()
    {
        // restore any modified sprites
        foreach (var kv in originalColors)
        {
            if (kv.Key != null) kv.Key.color = kv.Value;
        }
        originalColors.Clear();
    }

    private void Update()
    {
        if (decorativeOnly) return;
        if (!collectible || collected) return;
        if (playerInside && Input.GetKeyDown(KeyCode.E))
        {
            Collect();
        }
    }

    private void Start()
    {
        if (decorativeOnly)
        {
            // ensure not collectible
            collectible = false;
            // disable any CircleCollider2D on this object or children so it does not receive trigger events
            var cols = GetComponentsInChildren<CircleCollider2D>(true);
            foreach (var c in cols)
            {
                if (c != null) c.enabled = false;
            }
        }
    }

    private void Collect()
    {
        collected = true;
        int id = (int)noteColor;
        var nm = FindObjectOfType<NoteManager>();
        if (collectMode == CollectMode.AllColors)
        {
            for (int nid = 0; nid < 3; nid++)
            {
                if (OnCollected != null) OnCollected.Invoke(nid);
                if (nm != null && dropAmount > 0) nm.AddNotes(nid, dropAmount);
            }
        }
        else
        {
            if (OnCollected != null) OnCollected.Invoke(id);
            if (nm != null && dropAmount > 0) nm.AddNotes(id, dropAmount);
        }

        if (destroyOnCollect)
        {
            Destroy(gameObject);
            return;
        }
        if (disableOnCollect)
        {
            gameObject.SetActive(false);
            return;
        }
    }
}
