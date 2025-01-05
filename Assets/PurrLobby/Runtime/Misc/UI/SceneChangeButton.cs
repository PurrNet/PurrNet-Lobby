using PurrNet;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PurrLobby
{
    public class SceneChangeButton : MonoBehaviour
    {
        [PurrScene, SerializeField] private string scene;

        public void ChangeScene()
        {
            SceneManager.LoadSceneAsync(scene);
        }
    }
}
