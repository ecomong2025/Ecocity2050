using UnityEngine;

public class BtnSFX : MonoBehaviour
{
    public AudioClip clickSound;
    private AudioSource sfxPlayer;

    void Start()
    {
        sfxPlayer = GameObject.Find("SFXPlayer").GetComponent<AudioSource>();
    }

    public void PlayClickSound()
    {
        if (sfxPlayer != null && clickSound != null)
        {
            sfxPlayer.PlayOneShot(clickSound);
        }
    }
}