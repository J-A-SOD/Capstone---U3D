using System.Collections;
using UnityEngine;

public class FadeFromBlack : MonoBehaviour
{
    public CanvasGroup cover;
    public float startDelay = 0.5f;
    public float fadeTime = 2f;

    void Start()
    {
        cover.alpha = 1f;              // start fully black
        cover.blocksRaycasts = true;   // block clicks while covered
        StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        yield return new WaitForSeconds(startDelay);

        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            cover.alpha = Mathf.Lerp(1f, 0f, t / fadeTime);
            yield return null;
        }

        cover.alpha = 0f;
        cover.blocksRaycasts = false;  // let clicks through again
    }
}