using UnityEngine;
using UnityEngine.UI;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;

    // 드래그 안 했어도 자동으로 찾아줍니다.
    public GameObject check1;
    public GameObject check2;
    public GameObject check3;
    public GameObject check4;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // 자동 바인딩 (Hierarchy 이름 기준)
        if (!check1) check1 = GameObject.Find("Check1");
        if (!check2) check2 = GameObject.Find("Check2");
        if (!check3) check3 = GameObject.Find("Check3");
        if (!check4) check4 = GameObject.Find("Check4");
    }

    public void SetCheck(int idx, bool on)
    {
        var go = idx switch { 1 => check1, 2 => check2, 3 => check3, 4 => check4, _ => null };
        if (!go) { Debug.LogWarning($"[Quest] Check{idx} 오브젝트를 찾지 못했습니다."); return; }

        // 1) 자기 자신에 Image가 있으면 켜기
        var img = go.GetComponent<Image>();
        if (img) { img.enabled = on; return; }

        // 2) 자식 중에 체크 모양 이미지를 찾아 켜기
        var childImg = go.GetComponentInChildren<Image>(true);
        if (childImg) { childImg.enabled = on; go.SetActive(true); return; }

        // 3) 마지막으로 GameObject 자체 활성화
        go.SetActive(on);
    }

    public void OnBuildingInstalled(GameObject prefab, BuildingData data)
    {
        if (!prefab || data == null)
        {
            Debug.LogWarning("[Quest] 잘못된 파라미터(prefab 혹은 data null)");
            return;
        }

        bool isFactory = IsFactory(prefab, data);
        bool zeroEmission = IsZeroEmission(data);

        Debug.Log($"[Quest] 설치됨: {prefab.name}, isFactory={isFactory}, zero={zeroEmission}");

        if (isFactory) SetCheck(1, true); // 예: 공장 설치 → Check1
        if (zeroEmission) SetCheck(2, true); // 예: 무배출 건물 → Check2
    }

    bool IsFactory(GameObject prefab, BuildingData data)
    {
        // ✅ 가장 확실한 방법: BuildingData에 플래그가 있으면 그걸 사용
        // return data.isFactory;

        // 없으면 Tag/이름으로 판정 (프리팹에 Tag "Factory" 권장)
        if (prefab.CompareTag("Factory")) return true;
        string n = prefab.name.ToLower();
        return n.Contains("factory") || n.Contains("plant"); // 필요하면 키워드 추가
    }

    bool IsZeroEmission(BuildingData data)
    {
        // 프로젝트 규칙에 맞게 조정. 여기선 “배출 없음”을 다음과 같이 간주
        // (양수 배출이 없고, 순간/최대도 증가시키지 않음)
        return data.co2PerSecond <= 0f
            && data.instantCO2Change <= 0f
            && data.maxCO2Change <= 0f;
    }
}
