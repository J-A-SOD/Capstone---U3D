using UnityEngine;

public class LightJiggle : MonoBehaviour
{
    [SerializeField] private Vector3 range = new Vector3(0.2f, 0.2f, 0.2f);   // how far it can move from its start, per axis
    [SerializeField] private float speed = 1f;                                // how fast it wanders
    [SerializeField] private bool randomiseStart = true;                      // stops several lights moving in sync

    private Vector3 startLocalPosition;
    private Vector3 seed;

    void Start()
    {
        // Runs when the scene loads: remember where the light began
        startLocalPosition = transform.localPosition;

        // A random starting point in the noise, so each light moves differently
        seed = randomiseStart
            ? new Vector3(Random.value, Random.value, Random.value) * 100f
            : Vector3.zero;
    }

    void Update()
    {
        float t = Time.time * speed;

        // Perlin noise gives smooth, wandering values from 0 to 1; "* 2 - 1" turns them into -1 to 1.
        // The different added numbers make x, y and z move independently of each other.
        float x = Mathf.PerlinNoise(seed.x, t) * 2f - 1f;
        float y = Mathf.PerlinNoise(seed.y + 17.3f, t) * 2f - 1f;
        float z = Mathf.PerlinNoise(seed.z + 41.9f, t) * 2f - 1f;

        transform.localPosition = startLocalPosition + new Vector3(x * range.x, y * range.y, z * range.z);
    }
}