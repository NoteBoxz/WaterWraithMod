using UnityEngine;

public class PlayAudioOnAnimEvent : MonoBehaviour
{
    public AudioSource audioSource = null!;
    public AudioClip[] audioClips = null!;
    public void PlayAudio(int index)
    {
        audioSource.PlayOneShot(audioClips[index], audioSource.volume);
    }
}