using UnityEngine;

public class PlayRandomAudioOnAnimEvent : MonoBehaviour
{
    public AudioSource audioSource = null!;
    public AudioClip[] audioClips = null!;
    public void PlayRandom()
    {
        audioSource.PlayOneShot(audioClips[UnityEngine.Random.Range(0, audioClips.Length - 1)], audioSource.volume);
    }
}