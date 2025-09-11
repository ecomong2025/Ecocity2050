using UnityEngine;

public class SFXPlayer : MonoBehaviour
{
    public static SFXPlayer Instance;

    [Header("Audio Clips")]
    public AudioClip clickClip;       // 버튼 클릭
    public AudioClip correctClip;     // 정답
    public AudioClip incorrectClip;   // 오답
    // 필요하면 추가 클립 더 등록 가능

    private AudioSource audioSource;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        audioSource = GetComponent<AudioSource>();
        DontDestroyOnLoad(gameObject); // 씬 전환에도 유지
    }

    public void PlayClick() => audioSource.PlayOneShot(clickClip);
    public void PlayCorrect() => audioSource.PlayOneShot(correctClip);
    public void PlayIncorrect() => audioSource.PlayOneShot(incorrectClip);

    // 일반적인 재생용
    public void PlaySFX(AudioClip clip)
    {
        if (clip != null)
            audioSource.PlayOneShot(clip);
    }

    //볼륨조절용
    public void SetVolume(float volume)
    {
        if (audioSource != null)
            audioSource.volume = volume;
    }
}

