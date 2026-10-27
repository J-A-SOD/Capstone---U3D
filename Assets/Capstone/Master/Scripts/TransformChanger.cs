using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class TransformChange
{
    public Transform target;                 // the object to move or rotate

    [Header("Position")]
    public bool changePosition = true;
    public Vector3 position;

    [Header("Rotation (degrees)")]
    public bool changeRotation;
    public Vector3 rotation;

    [Header("Options")]
    public bool relative;            // add to where it is now, instead of going to exact values
    public bool useLocalSpace = true;
    public float delay = 0f;         // seconds before this object starts
    public float duration = 2f;      // seconds it takes (0 = instant)
}

public class TransformChanger : MonoBehaviour
{
    public TransformChange[] changes;
    public AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("When")]
    public bool triggerOnTouch = true;
    public bool onlyOnce = true;

    bool used;
    readonly Dictionary<Transform, Coroutine> running = new Dictionary<Transform, Coroutine>();

    void OnTriggerEnter(Collider other) { Touched(other.gameObject); }
    void OnCollisionEnter(Collision collision) { Touched(collision.gameObject); }

    void Touched(GameObject other)
    {
        if (!triggerOnTouch) return;
        if (!other.CompareTag("Player")) return;
        Go();
    }

    // Can also be called from a button's On Click list or another script
    [ContextMenu("Test Change")]
    public void Go()
    {
        if (onlyOnce && used) return;
        used = true;

        foreach (TransformChange c in changes)
        {
            if (c.target == null) continue;

            Coroutine old;
            if (running.TryGetValue(c.target, out old) && old != null) StopCoroutine(old);
            running[c.target] = StartCoroutine(Move(c));
        }
    }

    IEnumerator Move(TransformChange c)
    {
        if (c.delay > 0f) yield return new WaitForSeconds(c.delay);

        Transform t = c.target;
        bool local = c.useLocalSpace;

        Vector3 startPos = local ? t.localPosition : t.position;
        Quaternion startRot = local ? t.localRotation : t.rotation;

        Vector3 endPos = c.relative ? startPos + c.position : c.position;
        Quaternion endRot = Quaternion.Euler(c.rotation);

        if (c.duration <= 0f)
        {
            Set(c, startPos, endPos, startRot, endRot, 1f);
            yield break;
        }

        float time = 0f;
        while (time < 1f)
        {
            time = Mathf.Min(time + Time.deltaTime / c.duration, 1f);
            Set(c, startPos, endPos, startRot, endRot, curve.Evaluate(time));
            yield return null;
        }
    }

    void Set(TransformChange c, Vector3 startPos, Vector3 endPos, Quaternion startRot, Quaternion endRot, float k)
    {
        Transform t = c.target;

        if (c.changePosition)
        {
            Vector3 p = Vector3.LerpUnclamped(startPos, endPos, k);
            if (c.useLocalSpace) t.localPosition = p; else t.position = p;
        }

        if (c.changeRotation)
        {
            // Relative turns spin around the object's own axes and can go past 180 degrees
            Quaternion r = c.relative
                ? startRot * Quaternion.Euler(c.rotation * k)
                : Quaternion.SlerpUnclamped(startRot, endRot, k);
            if (c.useLocalSpace) t.localRotation = r; else t.rotation = r;
        }
    }

#if UNITY_EDITOR
    // Handy for setting up: move an object to where it should end up, then run this
    [ContextMenu("Record Current As Target")]
    void RecordCurrent()
    {
        UnityEditor.Undo.RecordObject(this, "Record current as target");
        foreach (TransformChange c in changes)
        {
            if (c.target == null || c.relative) continue;
            c.position = c.useLocalSpace ? c.target.localPosition : c.target.position;
            c.rotation = (c.useLocalSpace ? c.target.localRotation : c.target.rotation).eulerAngles;
        }
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}