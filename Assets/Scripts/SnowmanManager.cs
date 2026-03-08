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

    [Header("Temperature Image Alpha")]
    public float activeAlpha = 1f;
    public float inactiveAlpha = 0.5f;
    public bool changeTemperatureImageAlpha = true;

    private void Start()
    {
        snowAmount = Mathf.Clamp(snowAmount, 0f, maxSnow);
        ApplyTemperatureVisuals();
        UpdateTemperatureImageAlphas();

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
    }

    private void Update()
    {
        DetectCurrentTilemapAndApply();
        // melt snow based on current temperature
        float speed = meltSpeeds[Mathf.Clamp((int)currentTemperature, 0, meltSpeeds.Length - 1)];
        if (snowAmount > 0f)
        {
            snowAmount = Mathf.Max(0f, snowAmount - speed * Time.deltaTime);
        }
        UpdateSnowUI();
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
        Temperature newTemp = currentTemperature;

        // First: try Tilemap.GetTile method (more reliable)
        if (frostTilemap != null)
        {
            var cell = frostTilemap.WorldToCell(pos);
            if (frostTilemap.GetTile(cell) != null) newTemp = Temperature.Cold;
        }

        if (newTemp == currentTemperature && forestTilemap != null)
        {
            var cell = forestTilemap.WorldToCell(pos);
            if (forestTilemap.GetTile(cell) != null) newTemp = Temperature.Mild;
        }

        if (newTemp == currentTemperature && dessertTilemap != null)
        {
            var cell = dessertTilemap.WorldToCell(pos);
            if (dessertTilemap.GetTile(cell) != null) newTemp = Temperature.Hot;
        }

        // Fallback: collider-based detection if tilemap check found nothing
        if (newTemp == currentTemperature)
        {
            Collider2D[] hits = Physics2D.OverlapPointAll(pos);
            foreach (var c in hits)
            {
                if (c == null) continue;
                var go = c.gameObject;
                if (go == tilemapFrost) { newTemp = Temperature.Cold; break; }
                if (go == tilemapForest) { newTemp = Temperature.Mild; break; }
                if (go == tilemapDessert) { newTemp = Temperature.Hot; break; }
                if (tilemapFrost != null && go.transform.IsChildOf(tilemapFrost.transform)) { newTemp = Temperature.Cold; break; }
                if (tilemapForest != null && go.transform.IsChildOf(tilemapForest.transform)) { newTemp = Temperature.Mild; break; }
                if (tilemapDessert != null && go.transform.IsChildOf(tilemapDessert.transform)) { newTemp = Temperature.Hot; break; }
            }
        }

        if (newTemp != currentTemperature)
        {
            Debug.Log($"Temperature changed from {currentTemperature} to {newTemp}");
            currentTemperature = newTemp;
            ApplyTemperatureVisuals();
            UpdateTemperatureImageAlphas();
        }
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
