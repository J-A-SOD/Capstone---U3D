using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimedObjectEntrance : MonoBehaviour
{
    [System.Serializable]
    public class TimedObject
    {
        [Tooltip("The object that will appear.")]
        public GameObject target;

        [Tooltip("Seconds after the scene loads before the object appears.")]
        public float enterTime = 0f;
    }

    [Tooltip("Add one entry per object. Order doesn't matter.")]
    public List<TimedObject> objects = new List<TimedObject>();

    void Start()
    {
        foreach (TimedObject item in objects)
        {
            if (item.target == null) continue;

            // Hide the object right away, then bring it in at its time.
            item.target.SetActive(false);
            StartCoroutine(EnterAfterDelay(item));
        }
    }

    IEnumerator EnterAfterDelay(TimedObject item)
    {
        yield return new WaitForSeconds(item.enterTime);
        item.target.SetActive(true);
    }
}