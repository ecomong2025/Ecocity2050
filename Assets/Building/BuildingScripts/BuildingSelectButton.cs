using UnityEngine;
using UnityEngine.UI;

public class BuildingSelectButton : MonoBehaviour
{
    public TileClickInstaller installer;       // 인스펙터에 드래그
    public GameObject buildingPrefab;          // 설치할 프리팹 드래그
    public bool continuous = true;             // 누르면 연속 설치로

    void Awake()
    {
        var btn = GetComponent<Button>();
        if (btn) btn.onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        if (!installer || !buildingPrefab) return;

        installer.SetSelectedBuilding(buildingPrefab);
        installer.SetContinuousPlacement(continuous); // 연속 배치 켜기/끄기

        // 필요 시: 패널 열기/회전 버튼 활성화 등은 SetSelectedBuilding에서 이미 처리
        // (rotateKey=Space로 회전 가능)
    }
}
