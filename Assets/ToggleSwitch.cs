using UnityEngine;
using UnityEngine.UI;

public class ToggleSwitch : MonoBehaviour
{
    [Header("UI References")]
    public Image backgroundImage;
    public Image handleImage;

    [Header("Sprites")]
    public Sprite backgroundOn;
    public Sprite backgroundOff;
    public Sprite handleOn;
    public Sprite handleOff;

    [Header("Initial State")]
    public bool isOn = false;

    [Header("Toggle Type")]              // 🔹 추가
    public bool isBGM = false;           // true면 BGM 토글, false면 SFX 토글

    private void Start()
    {
        UpdateToggleVisuals();

        // 🔹 SettingManager에서 저장된 상태 불러와서 UI 맞추기
        if (isBGM && SettingManager.Instance != null)
            isOn = SettingManager.Instance.IsBGMOn();
        else if (!isBGM && SettingManager.Instance != null)
            isOn = SettingManager.Instance.IsSFXOn();

        UpdateToggleVisuals();
    }

    public void Toggle()
    {
        // 상태 반전
        isOn = !isOn;

        // 시각적 요소 업데이트
        UpdateToggleVisuals();

        // 🔹 SettingManager에 실제 오디오 상태 반영
        if (SettingManager.Instance != null)
        {
            if (isBGM)
                SettingManager.Instance.SetBGM(isOn);
            else
                SettingManager.Instance.SetSFX(isOn);
        }
    }

    private void UpdateToggleVisuals()
    {
        float halfWidth = backgroundImage.rectTransform.rect.width / 2f;
        float handleHalf = handleImage.rectTransform.rect.width / 2f;

        float xPos = isOn ? -(halfWidth - handleHalf) : (halfWidth - handleHalf);

        backgroundImage.sprite = isOn ? backgroundOn : backgroundOff;
        handleImage.sprite = isOn ? handleOn : handleOff;

        handleImage.rectTransform.anchoredPosition = new Vector2(xPos, 0);
    }
}
