using System.Collections;
using UnityEngine;

[System.Serializable]
public class SkyChange
{
    public string propertyName = "_SkyColour";   // the Reference name from Shader Graph
    public bool isColor;                         // tick for colour parameters
    public float targetFloat = 1f;               // used when Is Color is off
    [ColorUsage(true, true)] public Color targetColor = Color.white;  // used when Is Color is on
}

public class SkyboxChanger : MonoBehaviour
{
    [Header("What to change")]
    public SkyChange[] changes;

    [Header("How")]
    public float delay = 0f;         // seconds to wait before starting
    public float duration = 5f;      // seconds the change takes
    public AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("When")]
    public bool triggerOnTouch = true;   // untick to start it only from events or other scripts
    public bool onlyOnce = true;

    bool used;
    Coroutine running;
    static Material skyInstance;

    void OnTriggerEnter(Collider other) { Touched(other.gameObject); }
    void OnCollisionEnter(Collision collision) { Touched(collision.gameObject); }

    void Touched(GameObject other)
    {
        if (!triggerOnTouch) return;
        if (!other.CompareTag("Player")) return;
        Go();
    }

    // Can also be called from a button's On Click list or another script
    [ContextMenu("Test Change")]
    public void Go()
    {
        if (onlyOnce && used) return;
        used = true;
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        Material sky = GetSkyInstance();
        if (sky == null) yield break;

        // Remember where each parameter starts
        float[] fromFloat = new float[changes.Length];
        Color[] fromColor = new Color[changes.Length];
        for (int i = 0; i < changes.Length; i++)
        {
            string n = changes[i].propertyName;
            if (!sky.HasProperty(n))
            {
                Debug.LogError("Skybox material has no property called " + n + ". Check the Reference name in Shader Graph.");
                continue;
            }
            if (changes[i].isColor) fromColor[i] = sky.GetColor(n);
            else fromFloat[i] = sky.GetFloat(n);
        }

        float d = Mathf.Max(duration, 0.01f);
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(t + Time.deltaTime / d, 1f);
            Apply(sky, fromFloat, fromColor, curve.Evaluate(t));
            yield return null;
        }

        Apply(sky, fromFloat, fromColor, curve.Evaluate(1f));
        DynamicGI.UpdateEnvironment();   // refreshes lighting that uses the sky
        running = null;
    }

    void Apply(Material sky, float[] fromFloat, Color[] fromColor, float k)
    {
        for (int i = 0; i < changes.Length; i++)
        {
            string n = changes[i].propertyName;
            if (!sky.HasProperty(n)) continue;

            if (changes[i].isColor)
                sky.SetColor(n, Color.LerpUnclamped(fromColor[i], changes[i].targetColor, k));
            else
                sky.SetFloat(n, Mathf.LerpUnclamped(fromFloat[i], changes[i].targetFloat, k));
        }
    }

    // Works on a copy so your original material file is never permanently changed
    static Material GetSkyInstance()
    {
        if (RenderSettings.skybox == null)
        {
            Debug.LogError("This scene has no skybox material set.");
            return null;
        }
        if (skyInstance == null || RenderSettings.skybox != skyInstance)
        {
            skyInstance = new Material(RenderSettings.skybox);
            RenderSettings.skybox = skyInstance;
        }
        return skyInstance;
    }
}