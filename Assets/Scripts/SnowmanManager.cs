using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

// Manages snowman snow level and temperature-based melting when standing on different Tilemaps
// - Assign the three Tilemap GameObjects (TilemapFrost, TilemapForest, TilemapDessert)
// - Assign optional SpriteRenderer for the snowman to change appearance per temperature
// - Configure three melt speeds (cold->hot) via inspector

public class SnowmanManager : MonoBehaviour
{
    public enum Temperature
    {
        Cold = 0,
        Mild = 1,
        Hot = 2
    }

    [Header("Snow")]
    public float maxSnow = 100f;
    public float snowAmount = 100f;
    // When true, snow will not decrease over time
    public bool pauseMelting = false;

    [Header("Temperature & Speeds")]
    public Temperature currentTemperature = Temperature.Mild;
    // melt speeds for Cold/Mild/Hot (units of snow per second)
    public float[] meltSpeeds = new float[3] { 0.5f, 1f, 2f };

    [Header("Tilemaps (assign in inspector)")]
    public GameObject tilemapFrost;
    public GameObject tilemapForest;
    public GameObject tilemapDessert;

    // cached Tilemap components
    private Tilemap frostTilemap;
    private Tilemap forestTilemap;
    private Tilemap dessertTilemap;

    [Header("Visuals")]
    // Optional: sprites for the snowman in different temperatures (UI Image)
    public Image snowmanImage;
    public Sprite[] temperatureSprites = new Sprite[3];
    // Three UI Images representing temperature overlays (Cold, Mild, Hot). We will change their alpha based on current tilemap.
    public Image[] temperatureImages = new Image[3];

    [Header("Snow UI")]
    // Optional: UI slider to show current snow amount and optional text to display numeric value
    public UnityEngine.UI.Slider snowSlider;
    public UnityEngine.UI.Text snowValueText;
    // Optional: UI text to display the current temperature (Cold/Mild/Hot)
    public UnityEngine.UI.Text temperatureText;

    [Header("Hut (Day) UI")]
    // Slider representing the snow-hut durability/time remaining for the day
    public UnityEngine.UI.Slider hutSlider;
    // Max value shown on hutSlider
    public float hutMax = 100f;
    // Per-day durations in seconds: day0, day1, day2
    public float[] hutDayDurations = new float[3] { 120f, 240f, 300f };
    // Optional: ProgressManager reference so SnowmanManager can use currentDay when starting a day
    public ProgressManager progressManager;

    private float hutAmount = 0f;
    private float currentHutDuration = 0f; // seconds
    private float hutDecreaseRate = 0f; // hut units per second
    // whether we've already triggered end-of-day/gameover due to snow/hut reaching zero
    private bool endTriggered = false;

    [Header("Temperature Image Alpha")]
    public float activeAlpha = 1f;
    public float inactiveAlpha = 0.5f;
    public bool changeTemperatureImageAlpha = true;

    // Temporary override state: when true we won't let tilemap detection overwrite the temperature
    private bool tempOverrideActive = false;
    private float tempOverrideUntil = 0f;
    private Coroutine tempOverrideCoroutine = null;
    // override delta relative to the base tilemap temperature (e.g. -1, -2)
    private int tempOverrideDelta = 0;

    private void Start()
    {
        snowAmount = Mathf.Clamp(snowAmount, 0f, maxSnow);
        ApplyTemperatureVisuals();
        UpdateTemperatureImageAlphas();

        // initialize temperature text UI
        UpdateTemperatureUI();

        // cache Tilemap components if available
        frostTilemap = tilemapFrost != null ? tilemapFrost.GetComponent<Tilemap>() : null;
        forestTilemap = tilemapForest != null ? tilemapForest.GetComponent<Tilemap>() : null;
        dessertTilemap = tilemapDessert != null ? tilemapDessert.GetComponent<Tilemap>() : null;

        // initialize snow UI
        if (snowSlider != null)
        {
            snowSlider.maxValue = maxSnow;
            snowSlider.value = snowAmount;
        }
        UpdateSnowUI();

        // initialize hut UI (do not start countdown until StartDayHutTimer is called)
        hutAmount = hutMax;
        if (hutSlider != null)
        {
            hutSlider.maxValue = hutMax;
            hutSlider.value = hutAmount;
        }
    }

    private void Awake()
    {
        // record initial position on first Awake
        initialPosition = transform.position;
    }

    // stored initial position to allow resetting on game restart
    private Vector3 initialPosition;

    public void ResetToInitialPosition()
    {
        transform.position = initialPosition;
    }

    private void Update()
    {
        DetectCurrentTilemapAndApply();
        // melt snow based on current temperature (unless paused)
        if (!pauseMelting)
        {
            float speed = meltSpeeds[Mathf.Clamp((int)currentTemperature, 0, meltSpeeds.Length - 1)];
            if (snowAmount > 0f)
            {
                snowAmount = Mathf.Max(0f, snowAmount - speed * Time.deltaTime);
            }
            // hut countdown (based on current day duration). Uses same pause flag to pause during panels.
            if (currentHutDuration > 0f && hutDecreaseRate > 0f)
            {
                hutAmount = Mathf.Max(0f, hutAmount - hutDecreaseRate * Time.deltaTime);
            }
        }
        UpdateSnowUI();
        UpdateHutUI();

        // If snow or hut reached zero, trigger end-of-day behavior once
        if (!endTriggered && (snowAmount <= 0f || hutAmount <= 0f))
        {
            endTriggered = true;
            // Pause melting and notify ProgressManager to handle end-of-day (force failure)
            pauseMelting = true;
            if (progressManager != null)
            {
                string reason = snowAmount <= 0f ? "Snow depleted" : "Hut timer expired";
                progressManager.HandleDayEndByTimer(true, reason);
            }
        }
    }

    // Public helper to add snow (e.g., player action)
    public void AddSnow(float amount)
    {
        snowAmount = Mathf.Clamp(snowAmount + amount, 0f, maxSnow);
        UpdateSnowUI();
    }

    private void UpdateSnowUI()
    {
        if (snowSlider != null)
        {
            snowSlider.value = snowAmount;
        }
        if (snowValueText != null)
        {
            snowValueText.text = string.Format("{0:0}/{1:0}", snowAmount, maxSnow);
        }
    }

    // Detect which assigned tilemap we're standing on and set currentTemperature
    private void DetectCurrentTilemapAndApply()
    {
        var pos = transform.position;

        // Determine the base temperature from the tilemap at this position
        Temperature baseTemp = GetBaseTilemapTemperature(pos);

        if (tempOverrideActive && Time.time < tempOverrideUntil)
        {
            // While override is active, compute overridden temperature relative to baseTemp
            int baseIdx = (int)baseTemp;
            int overIdx = Mathf.Clamp(baseIdx + tempOverrideDelta, 0, 2);
            var overridden = (Temperature)overIdx;
            if (overridden != currentTemperature)
            {
                Debug.Log($"Temperature overridden (base {baseTemp}) -> {overridden}");
                currentTemperature = overridden;
                ApplyTemperatureVisuals();
                UpdateTemperatureImageAlphas();
                UpdateTemperatureUI();
            }
            return;
        }

        // Normal behavior: set temperature to baseTemp
        if (baseTemp != currentTemperature)
        {
            Debug.Log($"Temperature changed from {currentTemperature} to {baseTemp}");
            currentTemperature = baseTemp;
            ApplyTemperatureVisuals();
            UpdateTemperatureImageAlphas();
            UpdateTemperatureUI();
        }
    }

    // Return the temperature determined by tilemaps/colliders at `pos` (ignores overrides)
    private Temperature GetBaseTilemapTemperature(Vector3 pos)
    {
        // First: try Tilemap.GetTile method (more reliable)
        if (frostTilemap != null)
        {
            var cell = frostTilemap.WorldToCell(pos);
            if (frostTilemap.GetTile(cell) != null) return Temperature.Cold;
        }

        if (forestTilemap != null)
        {
            var cell = forestTilemap.WorldToCell(pos);
            if (forestTilemap.GetTile(cell) != null) return Temperature.Mild;
        }

        if (dessertTilemap != null)
        {
            var cell = dessertTilemap.WorldToCell(pos);
            if (dessertTilemap.GetTile(cell) != null) return Temperature.Hot;
        }

        // Fallback: collider-based detection
        Collider2D[] hits = Physics2D.OverlapPointAll(pos);
        foreach (var c in hits)
        {
            if (c == null) continue;
            var go = c.gameObject;
            if (go == tilemapFrost) return Temperature.Cold;
            if (go == tilemapForest) return Temperature.Mild;
            if (go == tilemapDessert) return Temperature.Hot;
            if (tilemapFrost != null && go.transform.IsChildOf(tilemapFrost.transform)) return Temperature.Cold;
            if (tilemapForest != null && go.transform.IsChildOf(tilemapForest.transform)) return Temperature.Mild;
            if (tilemapDessert != null && go.transform.IsChildOf(tilemapDessert.transform)) return Temperature.Hot;
        }

        // Default to currentTemperature if nothing determinable
        return currentTemperature;
    }

    private void ApplyTemperatureVisuals()
    {
        int idx = Mathf.Clamp((int)currentTemperature, 0, temperatureSprites.Length - 1);
        if (snowmanImage != null && temperatureSprites != null && temperatureSprites.Length > idx && temperatureSprites[idx] != null)
        {
            snowmanImage.sprite = temperatureSprites[idx];
        }
    }

    private void UpdateTemperatureImageAlphas()
    {
        if (!changeTemperatureImageAlpha) return;
        SetImageAlpha(temperatureImages, 0, currentTemperature == Temperature.Cold ? activeAlpha : inactiveAlpha);
        SetImageAlpha(temperatureImages, 1, currentTemperature == Temperature.Mild ? activeAlpha : inactiveAlpha);
        SetImageAlpha(temperatureImages, 2, currentTemperature == Temperature.Hot ? activeAlpha : inactiveAlpha);
    }

    // Public setter to change temperature and update visuals
    public void SetTemperature(Temperature t)
    {
        currentTemperature = t;
        ApplyTemperatureVisuals();
        UpdateTemperatureImageAlphas();
        UpdateTemperatureUI();
    }

    // Apply a temporary temperature delta for `duration` seconds.
    // While active, tilemap detection will not overwrite the temperature.
    public void ApplyTempDelta(int delta, float duration)
    {
        if (tempOverrideCoroutine != null) StopCoroutine(tempOverrideCoroutine);
        tempOverrideDelta = delta;
        tempOverrideCoroutine = StartCoroutine(TempOverrideCoroutine(delta, duration));
    }

    private IEnumerator TempOverrideCoroutine(int delta, float duration)
    {
        // Determine base temperature at current position and apply delta relative to it
        var pos = transform.position;
        var baseTemp = GetBaseTilemapTemperature(pos);
        int baseIdx = (int)baseTemp;
        int newIdx = Mathf.Clamp(baseIdx + delta, 0, 2);

        tempOverrideActive = true;
        tempOverrideUntil = Time.time + duration;
        tempOverrideDelta = delta;

        SetTemperature((Temperature)newIdx);

        yield return new WaitForSeconds(duration);

        tempOverrideActive = false;
        tempOverrideUntil = 0f;

        // After override expires, re-evaluate tilemap to pick the proper temperature
        DetectCurrentTilemapAndApply();
        tempOverrideCoroutine = null;
    }

    // Update the optional temperature text UI
    private void UpdateTemperatureUI()
    {
        if (temperatureText == null) return;
        temperatureText.text = currentTemperature.ToString();
    }

    private void UpdateHutUI()
    {
        if (hutSlider != null)
        {
            hutSlider.value = hutAmount;
        }
    }

    // Start the hut timer for the given day (dayIndex 0-based). If dayIndex < 0 and progressManager assigned, uses progressManager.currentDay.
    public void StartDayHutTimer(int dayIndex = -1)
    {
        int idx = dayIndex;
        if (idx < 0 && progressManager != null) idx = progressManager.currentDay;
        idx = Mathf.Clamp(idx, 0, hutDayDurations.Length - 1);
        currentHutDuration = Mathf.Max(0.0001f, hutDayDurations[idx]);
        hutAmount = hutMax;
        hutDecreaseRate = hutMax / currentHutDuration;
        // reset any previous end trigger so timer can fire again
        endTriggered = false;
        // update UI
        if (hutSlider != null)
        {
            hutSlider.maxValue = hutMax;
            hutSlider.value = hutAmount;
        }
    }

    private void SetImageAlpha(Image[] imgs, int index, float alpha)
    {
        if (imgs == null) return;
        if (index < 0 || index >= imgs.Length) return;
        var img = imgs[index];
        if (img == null) return;
        var c = img.color;
        c.a = alpha;
        img.color = c;
    }

    private void SetTilemapAlpha(GameObject tmGO, float alpha)
    {
        if (tmGO == null) return;
        var tilemap = tmGO.GetComponent<Tilemap>();
        if (tilemap != null)
        {
            var c = tilemap.color;
            c.a = alpha;
            tilemap.color = c;
            return;
        }

        var sr = tmGO.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            var c = sr.color;
            c.a = alpha;
            sr.color = c;
        }
    }
}
