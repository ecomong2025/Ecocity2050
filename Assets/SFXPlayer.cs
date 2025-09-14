using UnityEngine;

public class SFXPlayer : MonoBehaviour
{
    public static SFXPlayer Instance;

    [Header("Audio Clips")]
    public AudioClip clickClip;       // 버튼 클릭
    public AudioClip correctClip;     // 성공
    public AudioClip incorrectClip;   // 실패
    public AudioClip popupOpenClip;   // 팝업 오픈 사운드 (추가)

    private AudioSource audioSource;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // AudioSource가 없으면 자동 추가
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.loop = false;

        DontDestroyOnLoad(gameObject); // 씬 전환 유지
    }

    public void PlayClick() { if (clickClip != null) audioSource.PlayOneShot(clickClip); }
    public void PlayCorrect() { if (correctClip != null) audioSource.PlayOneShot(correctClip); }
    public void PlayIncorrect() { if (incorrectClip != null) audioSource.PlayOneShot(incorrectClip); }

    // 일반 SFX 재생
    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    // 팝업 오픈 전용 재생 (popupOpenClip 우선)
    public void PlayPopupOpen(float volume = 1f)
    {
        if (audioSource == null) return;
        if (popupOpenClip != null) audioSource.PlayOneShot(popupOpenClip, Mathf.Clamp01(volume));
    }

    // 볼륨 설정
    public void SetVolume(float volume)
    {
        if (audioSource != null)
            audioSource.volume = Mathf.Clamp01(volume);
    }
}

