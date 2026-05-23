using UnityEngine;
using UnityEngine.SceneManagement;

public class Bootstrapper : MonoBehaviour
{
    private void Start()
    {
        // Загружаем главную игровую сцену
        SceneManager.LoadScene("MainScene");
    }
}