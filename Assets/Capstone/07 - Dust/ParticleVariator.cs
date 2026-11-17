using System.Collections;
using UnityEngine;

/// <summary>
/// Changes a Particle System's noise strength (and a few other values)
/// after set amounts of time, blending smoothly between "stages".
/// </summary>
public class ParticleVariator : MonoBehaviour
{
    [System.Serializable]
    public class Stage
    {
        public string label = "Stage";

        [Header("Noise module")]
        public float noiseStrength = 1f;
        public float noiseFrequency = 0.5f;
        public float noiseScrollSpeed = 0f;

        [Header("Other values")]
        public float emissionRate = 10f;   // Emission > Rate over Time
        public float startSpeed = 1f;      // Main > Start Speed

        [Header("Timing")]
        public float holdTime = 5f;        // seconds to stay on this stage before moving on
    }

    [Header("Setup")]
    public ParticleSystem target;          // drag your Particle System here

    [Header("Stages (the list of looks to cycle through)")]
    public Stage[] stages;

    [Header("Behaviour")]
    [Tooltip("Seconds taken to blend from one stage to the next. 0 = instant jump.")]
    public float blendTime = 2f;
    public bool loop = true;
    [Tooltip("Pick the next stage at random instead of in order.")]
    public bool randomOrder = false;

    private int currentIndex = 0;

    void Reset()
    {
        // Runs when you first add the script: grab a Particle System on the same object if there is one.
        target = GetComponent<ParticleSystem>();
    }

    void Start()
    {
        if (target == null) target = GetComponent<ParticleSystem>();

        if (target == null || stages == null || stages.Length == 0)
        {
            Debug.LogWarning("ParticleVariator: assign a Particle System and add at least one stage.", this);
            enabled = false;
            return;
        }

        // The Noise module must be switched on for any noise values to have an effect.
        var noise = target.noise;
        noise.enabled = true;

        ApplyStage(stages[0]);
        StartCoroutine(RunStages());
    }

    IEnumerator RunStages()
    {
        while (true)
        {
            // Stay on the current stage for its hold time.
            yield return new WaitForSeconds(stages[currentIndex].holdTime);

            // Work out which stage comes next.
            int next = currentIndex;
            if (randomOrder && stages.Length > 1)
            {
                while (next == currentIndex) next = Random.Range(0, stages.Length);
            }
            else
            {
                next = currentIndex + 1;
                if (next >= stages.Length)
                {
                    if (!loop) yield break;   // stop at the last stage
                    next = 0;
                }
            }

            yield return BlendTo(stages[next]);
            currentIndex = next;
        }
    }

    IEnumerator BlendTo(Stage to)
    {
        var noise = target.noise;
        var emission = target.emission;
        var main = target.main;

        // Remember the values we're starting from.
        float fromStrength = noise.strength.constant;
        float fromFrequency = noise.frequency;
        float fromScroll = noise.scrollSpeed.constant;
        float fromRate = emission.rateOverTime.constant;
        float fromSpeed = main.startSpeed.constant;

        if (blendTime <= 0f)
        {
            ApplyStage(to);
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / blendTime;
            float s = Mathf.SmoothStep(0f, 1f, t);

            SetValues(
                Mathf.Lerp(fromStrength, to.noiseStrength, s),
                Mathf.Lerp(fromFrequency, to.noiseFrequency, s),
                Mathf.Lerp(fromScroll, to.noiseScrollSpeed, s),
                Mathf.Lerp(fromRate, to.emissionRate, s),
                Mathf.Lerp(fromSpeed, to.startSpeed, s));

            yield return null;   // wait one frame
        }

        ApplyStage(to);   // make sure we land exactly on the target values
    }

    void ApplyStage(Stage s)
    {
        SetValues(s.noiseStrength, s.noiseFrequency, s.noiseScrollSpeed, s.emissionRate, s.startSpeed);
    }

    void SetValues(float strength, float frequency, float scroll, float rate, float speed)
    {
        var noise = target.noise;
        noise.strength = strength;
        noise.frequency = frequency;
        noise.scrollSpeed = scroll;

        var emission = target.emission;
        emission.rateOverTime = rate;

        var main = target.main;
        main.startSpeed = speed;
    }
}