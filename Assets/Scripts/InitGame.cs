using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class InitGame : MonoBehaviour
{
    public AssetReference MainMenu;
    private void Awake()
    {
        Addressables.LoadSceneAsync(MainMenu);
    }
}
