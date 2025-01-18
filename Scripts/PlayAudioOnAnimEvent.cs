using UnityEngine;

public class PlayAudioOnAnimEvent : MonoBehaviour
{
    public AudioSource audioSource = null!;
    public AudioClip[] audioClips = null!;
    public void PlayAudio(int index)
    {
        if (audioClips.Length - 1 < index)
        {
            Debug.LogWarning($"Audio clip index out of range: {index}");
            return;
        }
        audioSource.PlayOneShot(audioClips[index], audioSource.volume);
    }
}