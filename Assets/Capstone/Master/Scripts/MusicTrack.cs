using UnityEngine;

[CreateAssetMenu(menuName = "Music/Music Track")]
public class MusicTrack : ScriptableObject
{
    public AudioClip intro;   // optional: plays once, before the loop
    public AudioClip loop;    // required: the repeating part
    public AudioClip outro;   // optional: plays once when leaving this track
}