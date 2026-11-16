using UnityEngine;

public class TrackScroller : MonoBehaviour
{
    [SerializeField] private float speed = 10f;                    // how fast the tracks move
    [SerializeField] private Vector3 direction = Vector3.back;    // which way they move (0, 0, -1)
    [SerializeField] private float resetDistance = 5f;            // distance travelled before snapping back

    private Vector3 startPosition;

    void Start()
    {
        // Remember where the tracks began
        startPosition = transform.localPosition;
    }

    void Update()
    {
        // Move the tracks a little each frame (Time.deltaTime keeps speed consistent on any frame rate)
        transform.localPosition += direction.normalized * speed * Time.deltaTime;

        // Once they've travelled far enough, jump back by exactly that distance
        if (Vector3.Distance(transform.localPosition, startPosition) >= resetDistance)
        {
            transform.localPosition -= direction.normalized * resetDistance;
        }
    }
}