using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneFader : MonoBehaviour
{
    [SerializeField] private string sceneName;                  // exact name of the scene to load
    [SerializeField] private Color fadeColor = Color.black;     // colour to fade out to and in from
    [SerializeField] private float fadeOutDuration = 1f;        // seconds to fade to the colour
    [SerializeField] private float fadeInDuration = 1f;         // seconds to fade back in

    private static FadeOverlay overlay;
    private static bool isChanging;

    // Call this from your trigger (or from any other script)
    public void ChangeScene()
    {
        if (isChanging) return;   // ignore repeat triggers while a change is running
        if (overlay == null) overlay = FadeOverlay.Create();

        isChanging = true;
        overlay.StartCoroutine(Sequence(sceneName, fadeColor, fadeOutDuration, fadeInDuration));
    }

    private static IEnumerator Sequence(string scene, Color color, float outTime, float inTime)
    {
        // 1. Fade the screen to the colour
        yield return overlay.Fade(0f, 1f, color, outTime);

        // 2. Load the new scene while the screen is fully covered
        AsyncOperation load = SceneManager.LoadSceneAsync(scene);
        while (!load.isDone) yield return null;
        yield return null;   // let the new scene draw one frame underneath

        // 3. Fade the colour away to reveal the new scene
        yield return overlay.Fade(1f, 0f, color, inTime);

        isChanging = false;
    }

    // A hidden object that holds the colour overlay and survives scene changes
    private class FadeOverlay : MonoBehaviour
    {
        private Image image;

        public static FadeOverlay Create()
        {
            GameObject root = new GameObject("SceneFadeOverlay");
            DontDestroyOnLoad(root);   // keeps it alive when the scene changes

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767;   // draw on top of everything

            GameObject imageObject = new GameObject("Fade");
            imageObject.transform.SetParent(root.transform, false);
            Image img = imageObject.AddComponent<Image>();
            img.rectTransform.anchorMin = Vector2.zero;   // stretch to fill the screen
            img.rectTransform.anchorMax = Vector2.one;
            img.rectTransform.offsetMin = Vector2.zero;
            img.rectTransform.offsetMax = Vector2.zero;
            img.color = Color.clear;
            img.raycastTarget = false;

            FadeOverlay overlay = root.AddComponent<FadeOverlay>();
            overlay.image = img;
            return overlay;
        }

        public IEnumerator Fade(float from, float to, Color color, float duration)
        {
            image.raycastTarget = true;   // block UI clicks while fading
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(color, Mathf.Lerp(from, to, t / duration));
                yield return null;
            }
            SetAlpha(color, to);
            image.raycastTarget = to > 0f;
        }

        private void SetAlpha(Color color, float alpha)
        {
            color.a = alpha;
            image.color = color;
        }
    }
}