using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioFader : MonoBehaviour
{
    public float targetVolume = 1f;     // how loud it ends up after fading in
    public float fadeInTime = 3f;       // seconds
    public float fadeOutTime = 3f;      // seconds
    public float startDelay = 0f;       // seconds to wait before fading in
    public bool fadeInOnStart = true;   // tick to fade in automatically when the scene starts
    public AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    AudioSource source;
    Coroutine running;

    void Awake()
    {
        source = GetComponent<AudioSource>();
    }

    void Start()
    {
        if (fadeInOnStart) FadeIn();
    }

    // Can be called from a button's On Click list or from other scripts
    public void FadeIn()  { StartFade(targetVolume, fadeInTime, false, startDelay); }
    public void FadeOut() { StartFade(0f, fadeOutTime, true, 0f); }

    void StartFade(float to, float time, bool stopAtEnd, float delay)
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(FadeRoutine(to, time, stopAtEnd, delay));
    }

    IEnumerator FadeRoutine(float to, float time, bool stopAtEnd, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        // Start the sound silent, so there's no sudden blast
        if (to > 0f && !source.isPlaying)
        {
            source.volume = 0f;
            source.Play();
        }

        float from = source.volume;
        float d = Mathf.Max(time, 0.01f);
        float t = 0f;

        while (t < 1f)
        {
            t = Mathf.Min(t + Time.deltaTime / d, 1f);
            source.volume = Mathf.LerpUnclamped(from, to, curve.Evaluate(t));
            yield return null;
        }

        source.volume = to;
        if (stopAtEnd) source.Stop();
        running = null;
    }
}