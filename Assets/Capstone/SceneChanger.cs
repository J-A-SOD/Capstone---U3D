using UnityEngine;
using UnityEngine.SceneManagement; // Required for managing scenes

public class SceneChanger : MonoBehaviour
{
    [SerializeField] private string sceneToLoad; // Type the exact scene name in the Inspector

    // For 3D Objects: Triggers when an object enters this object's collider boundary
    private void OnTriggerEnter(Collider other)
    {
        // Check if the object entering the trigger is tagged as "Player"
        if (other.CompareTag("Player"))
        {
            SceneManager.LoadScene(sceneToLoad);
        }
    }
}
