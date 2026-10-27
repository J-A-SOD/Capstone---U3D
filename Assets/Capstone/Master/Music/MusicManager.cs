using System;
using System.Collections;
using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    public float bpm = 120f;
    public int beatsPerBar = 4;
    public float volume = 1f;
    public float fadeInFromSilence = 2f;
    public float defaultFadeBars = 2f;

    class Deck
    {
        public MusicTrack track;
        public AudioSource intro, loop, outro;
    }

    Deck current;
    double gridStart, loopStart, loopLength, busyUntil;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    static double Length(AudioClip c) { return c.samples / (double)c.frequency; }

    AudioSource NewSource(AudioClip clip, bool looping, float vol)
    {
        AudioSource s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.clip = clip;
        s.loop = looping;
        s.volume = vol;
        return s;
    }

    void SetDeckVolume(Deck d, float v)
    {
        if (d.intro != null) d.intro.volume = v;
        if (d.loop != null) d.loop.volume = v;
    }

    // everyBars: 1 = next bar, 4 = next 4-bar boundary, 0 = end of the current loop
    // fadeBars:  crossfade length in bars (0 = no fade). Default comes from the Inspector.
    // Returns the time the new track first becomes audible (start of its intro, or its loop).
    public double PlayTrack(MusicTrack track, int everyBars = 1, float fadeBars = -1f)
    {
        double now = AudioSettings.dspTime;
        if (track == null || track.loop == null)
        {
            Debug.LogError("MusicTrack needs a Loop clip.");
            return now;
        }
        if (now < busyUntil || (current != null && current.track == track)) return now;
        if (fadeBars < 0f) fadeBars = defaultFadeBars;

        double barLength = 60.0 / bpm * beatsPerBar;
        double introLength = (track.intro != null) ? Length(track.intro) : 0.0;
        Deck old = current;
        double boundary;

        if (old == null)
        {
            boundary = now + 0.1 + introLength;
        }
        else
        {
            double span = (everyBars > 0) ? barLength * everyBars : loopLength;
            double origin = (everyBars > 0) ? gridStart : loopStart;
            boundary = origin + (Math.Floor((now - origin) / span) + 1) * span;
            while (boundary - introLength < now + 0.1) boundary += span;
        }
        double introStart = boundary - introLength;

        // Work out fades and what happens to the old track
        double fadeInStart = introStart;
        float fadeInLen = 0f;
        float fadeOutLen = 0f;
        double outroEnd = 0.0;

        if (old == null)
        {
            fadeInLen = fadeInFromSilence;
        }
        else
        {
            if (fadeBars > 0f && track.intro == null)
            {
                fadeInStart = boundary;
                fadeInLen = (float)(barLength * fadeBars);
            }

            if (old.track.outro != null)
            {
                // Old loop stops on the boundary, its outro takes over
                old.loop.SetScheduledEndTime(boundary);
                old.outro = NewSource(old.track.outro, false, volume);
                old.outro.PlayScheduled(boundary);
                outroEnd = boundary + Length(old.track.outro);
            }
            else if (fadeBars > 0f)
            {
                fadeOutLen = (float)(barLength * fadeBars);   // old loop fades out from the boundary
            }
            else
            {
                old.loop.SetScheduledEndTime(boundary);        // hard stop on the boundary
            }
        }

        // Schedule the new track
        Deck next = new Deck { track = track };
        float startVol = (fadeInLen > 0f) ? 0f : volume;

        if (track.intro != null)
        {
            next.intro = NewSource(track.intro, false, startVol);
            next.intro.PlayScheduled(introStart);
        }
        next.loop = NewSource(track.loop, true, startVol);
        next.loop.PlayScheduled(boundary);

        current = next;
        gridStart = boundary;
        loopStart = boundary;
        loopLength = Length(track.loop);

        double fadeInEnd = (fadeInLen > 0f) ? fadeInStart + fadeInLen : 0.0;
        double fadeOutEnd = (fadeOutLen > 0f) ? boundary + fadeOutLen : 0.0;
        busyUntil = Math.Max(boundary, Math.Max(fadeInEnd, fadeOutEnd));
        double cleanupTime = Math.Max(busyUntil, outroEnd) + 0.5;

        StartCoroutine(RunTransition(old, next, fadeInStart, fadeInLen, boundary, fadeOutLen, cleanupTime));
        return introStart;
    }

    IEnumerator RunTransition(Deck old, Deck next, double fadeInStart, float fadeInLen,
                              double fadeOutStart, float fadeOutLen, double cleanupTime)
    {
        bool fadeInDone = (fadeInLen <= 0f);

        while (AudioSettings.dspTime < cleanupTime)
        {
            double t = AudioSettings.dspTime;

            if (!fadeInDone)
            {
                float k = Mathf.Clamp01((float)((t - fadeInStart) / fadeInLen));
                SetDeckVolume(next, volume * k);
                if (k >= 1f) fadeInDone = true;
            }
            if (old != null && fadeOutLen > 0f)
            {
                float k = Mathf.Clamp01((float)((t - fadeOutStart) / fadeOutLen));
                old.loop.volume = volume * (1f - k);
            }
            yield return null;
        }

        if (old != null)
        {
            if (old.intro != null) Destroy(old.intro);
            if (old.loop != null) Destroy(old.loop);
            if (old.outro != null) Destroy(old.outro);
        }
    }
}