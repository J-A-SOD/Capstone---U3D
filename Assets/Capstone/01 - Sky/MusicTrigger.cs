using UnityEngine;

public class MusicTrigger : MonoBehaviour
{
    public MusicTrack track;
    public int everyBars = 1;       // 1 = next bar, 4 = next 4 bars, 0 = end of the current loop
    public float fadeBars = 2f;     // 0 = no fade (use 0 when an intro or outro handles the blend)
    public bool onlyOnce = false;

    bool used;

    void OnTriggerEnter(Collider other) { Activate(other.gameObject); }
    void OnCollisionEnter(Collision collision) { Activate(collision.gameObject); }

    void Activate(GameObject other)
    {
        if (!other.CompareTag("Player")) return;
        if (onlyOnce && used) return;
        used = true;
        MusicManager.Instance.PlayTrack(track, everyBars, fadeBars);
    }
}