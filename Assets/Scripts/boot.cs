using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class boot : MonoBehaviour
{
    public bool isPressed = false;
    public AssetReference Storm;
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E) && !isPressed)
        {
            isPressed = true;
            SceneLoader.LoadAddressableScene(Storm);
        }
    }
}
