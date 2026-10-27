using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class IntroSequence : MonoBehaviour
{
    public TMP_Text messageText;
    public CanvasGroup messageGroup;
    public string nextSceneName = "01 - Sky";

    [TextArea] public string[] messages;
    public float fadeInTime = 1f;
    public float holdTime = 2.5f;
    public float fadeOutTime = 1f;
    public float gapTime = 1.5f;   // pause between a message fading out and the next one fading in

    [Header("Music")]
    public MusicTrack introTrack;
    public MusicTrack levelTrack;
    public float levelFadeBars = 2f;

    void Start()
    {
        messageGroup.alpha = 0f;
        MusicManager.Instance.PlayTrack(introTrack);
        StartCoroutine(PlaySequence());
    }

    IEnumerator PlaySequence()
    {
        for (int i = 0; i < messages.Length; i++)
        {
            messageText.text = messages[i];
            yield return Fade(0f, 1f, fadeInTime);
            yield return new WaitForSeconds(holdTime);
            yield return Fade(1f, 0f, fadeOutTime);

            // Only pause between messages, not after the last one
            if (i < messages.Length - 1)
                yield return new WaitForSeconds(gapTime);
        }

        // Start the level music on a bar line, then load the scene at that moment
        double startTime = MusicManager.Instance.PlayTrack(levelTrack, 1, levelFadeBars);
        while (AudioSettings.dspTime < startTime) yield return null;
        SceneManager.LoadScene(nextSceneName);
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            messageGroup.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        messageGroup.alpha = to;
    }
}