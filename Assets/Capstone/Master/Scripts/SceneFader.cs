using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneFader : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string sceneName;                  // exact name of the scene to load

    [Header("Fade")]
    [SerializeField] private Color fadeColor = Color.black;     // colour to fade out to and in from
    [SerializeField] private float fadeOutDuration = 1f;        // seconds to fade to the colour
    [SerializeField] private float fadeInDuration = 1f;         // seconds to fade back in

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";       // tag the player's collider must have
    [SerializeField] private bool hideMeshInPlay = true;        // hides the plane when the game runs

    private static FadeOverlay overlay;
    private static bool isChanging;

    // Clears leftover state each time you press Play
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        overlay = null;
        isChanging = false;
    }

    void Awake()
    {
        // Hide the plane only when the game runs, so you can still see it in the Scene view
        if (hideMeshInPlay)
        {
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null) meshRenderer.enabled = false;
        }
    }

    // Runs when something enters the plane's trigger collider
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        Debug.Log("SceneFader: player entered the trigger");
        ChangeScene();
    }

    // Also callable from other scripts or a UnityEvent
    public void ChangeScene()
    {
        if (isChanging) return;

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("SceneFader: scene '" + sceneName + "' is not in the Build Profiles list, or the name is wrong.");
            return;
        }

        if (overlay == null) overlay = FadeOverlay.Create();

        isChanging = true;
        overlay.StartCoroutine(Sequence(sceneName, fadeColor, fadeOutDuration, fadeInDuration));
    }

    // Finds the headset camera even if it isn't tagged MainCamera
    private static Camera FindCamera()
    {
        if (Camera.main != null) return Camera.main;
        foreach (Camera c in Camera.allCameras)   // enabled cameras only
        {
            if (c.stereoTargetEye != StereoTargetEyeMask.None) return c;
        }
        return Camera.allCamerasCount > 0 ? Camera.allCameras[0] : null;
    }

    private static IEnumerator Sequence(string scene, Color color, float outTime, float inTime)
    {
        Camera cam = FindCamera();
        if (cam == null) Debug.LogWarning("SceneFader: no camera found, the fade can't be seen.");
        else Debug.Log("SceneFader: fading on camera '" + cam.name + "'");

        // 1. Fade the view to the colour
        yield return overlay.Fade(0f, 1f, color, outTime);

        // 2. Load the new scene while the view is fully covered
        AsyncOperation load = SceneManager.LoadSceneAsync(scene);
        while (!load.isDone) yield return null;

        // 3. Wait for the new scene's camera, then fade back in
        while (FindCamera() == null) yield return null;
        yield return null;

        yield return overlay.Fade(1f, 0f, color, inTime);
        isChanging = false;
    }

    // A hidden panel that sits in front of the headset camera and survives scene changes
    private class FadeOverlay : MonoBehaviour
    {
        private const float DistanceFromCamera = 0.5f;
        private Image image;

        public static FadeOverlay Create()
        {
            GameObject root = new GameObject("SceneFadeOverlay");
            DontDestroyOnLoad(root);

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 32767;

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(100f, 100f);
            rect.localScale = Vector3.one * 0.05f;

            GameObject imageObject = new GameObject("Fade");
            imageObject.transform.SetParent(root.transform, false);
            Image img = imageObject.AddComponent<Image>();
            img.rectTransform.anchorMin = Vector2.zero;
            img.rectTransform.anchorMax = Vector2.one;
            img.rectTransform.offsetMin = Vector2.zero;
            img.rectTransform.offsetMax = Vector2.zero;
            img.color = Color.clear;
            img.raycastTarget = false;

            FadeOverlay overlay = root.AddComponent<FadeOverlay>();
            overlay.image = img;
            return overlay;
        }

        private void LateUpdate()
        {
            Camera cam = FindCamera();
            if (cam == null) return;
            Transform t = cam.transform;
            transform.SetPositionAndRotation(t.position + t.forward * DistanceFromCamera, t.rotation);
        }

        public IEnumerator Fade(float from, float to, Color color, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(color, Mathf.Lerp(from, to, t / duration));
                yield return null;
            }
            SetAlpha(color, to);
        }

        private void SetAlpha(Color color, float alpha)
        {
            color.a = alpha;
            image.color = color;
        }
    }
}