using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;

public class MusicPicker : MonoBehaviour
{
    public AudioSource audioSource;

    private List<AudioClip> musicClips;

    // Start is called before the first frame update

    void Awake()
    {
        // Load all audio clips from Resources/Music
        AudioClip[] clips = Resources.LoadAll<AudioClip>("Music");
        musicClips = new List<AudioClip>(clips);
    }

    void Start()
    {
        PlayRandomSong();
    }

    void OnEnable()
    {
        if (musicClips.Count > 0)
        {
            PlayRandomSong();
        }
    }

    void PlayRandomSong()
    {
        int index = Random.Range(0, musicClips.Count);
        audioSource.clip = musicClips[index];
        audioSource.Play();
    }
}
