using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;

public class MainMenu : MonoBehaviour
{
    public AssetReference MainGame;
    public Button StartGame;
    public Button QuitGame;
    public Image FrontImage;
    public bool isEntering = false;

    public void LoadNewGame()
    {
        isEntering = true;
        SceneLoader.LoadAddressableScene(MainGame);
    }
    public void QuitButton() => Application.Quit();

}
