using UnityEngine;
using UnityEngine.SceneManagement;


public class SceneChange : MonoBehaviour
{
    public void SceneChangeToZero(){
        SceneManager.LoadScene(0);
    }

    public void SceneChangeToOne(){
        SceneManager.LoadScene(1);
    }
}
