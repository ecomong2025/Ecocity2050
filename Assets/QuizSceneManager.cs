using UnityEngine;
using UnityEngine.SceneManagement;

public class QuizSceneManager : MonoBehaviour
{
    public void LoadQuizScene()
    {
        SceneManager.LoadScene("QuizScene");
    }
}
