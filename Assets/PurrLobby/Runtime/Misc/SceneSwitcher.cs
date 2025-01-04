using PurrNet;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PurrLobby
{
    public class SceneSwitcher : MonoBehaviour
    {
        [PurrScene, SerializeField] private string nextScene;

        public void SwitchScene()
        {
            SceneManager.LoadSceneAsync(nextScene);
        }
    }
}
