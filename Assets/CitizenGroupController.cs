using UnityEngine;

public class CitizenGroupController : MonoBehaviour
{
    public GameObject[] citizens; 
    private GameManager gameManager;

    void Start()
    {
        gameManager = FindObjectOfType<GameManager>();
    }

    void Update()
    {
        float satisfaction = gameManager.GetSatisfactionValue();
        int targetCount = Mathf.RoundToInt(satisfaction * citizens.Length);

        for (int i = 0; i < citizens.Length; i++)
        {
            bool shouldBeVisible = i < targetCount;

            // SetActive ¹æ½Ä
            citizens[i].SetActive(shouldBeVisible);

       
        }
    }

    void SetCitizenAlpha(GameObject citizen, float alpha)
    {
        var renderers = citizen.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            foreach (var mat in renderer.materials)
            {
                if (mat.HasProperty("_Color"))
                {
                    Color color = mat.color;
                    color.a = alpha;
                    mat.color = color;
                }
            }
        }
    }
}
