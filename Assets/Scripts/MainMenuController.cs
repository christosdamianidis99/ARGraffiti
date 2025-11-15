using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private string arSceneName = "02_ARMain";

    void Awake()
    {
        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);
    }

    void OnStartClicked()
    {
        SceneManager.LoadScene(arSceneName);
    }
}
