using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class GameManager : MonoBehaviour
{
    const string GameManagerKey = "GameManager";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]

    static void InitiateGameManager()
    {
        Addressables.InstantiateAsync(GameManagerKey).Completed += OnInitiated;
    }

    static void OnInitiated(AsyncOperationHandle<GameObject> operationHandle)
    {
        DontDestroyOnLoad(operationHandle.Result); 
    }
}
