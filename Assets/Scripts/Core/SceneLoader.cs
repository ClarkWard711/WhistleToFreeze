using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.AsyncOperations;

public class SceneLoader : MonoBehaviour
{
    static SceneLoader Instance;
    static SceneInstance loadedSceneInstance;

    public const string OuterSceneKey = "OuterScene";
    public const string DebugRoomSceneKey = "DebugRoomScene";
    public const string BattleSceneKey = "BattleScene";
    public const string MainMenuSceneKey = "MainMenuScene";

    public static event System.Action LoadingStarted;
    public static event System.Action<float> IsLoading;
    public static event System.Action LoadingSucceeded;
    public static event System.Action LoadingCompleted;
    public static bool ShowLoadingScreen { get; private set; }
    public static bool IsSceneLoaded { get; private set; }


    void Awake()
    {
        Instance = this;
    }

    static IEnumerator LoadAddressableSceneCoroutine(object sceneKey, bool showLoadingScreen, bool loadSceneAdditively, bool activateOnload)
    {
        LoadSceneMode loadSceneMode = loadSceneAdditively ? LoadSceneMode.Additive : LoadSceneMode.Single;
        var asyncOperationHandle = Addressables.LoadSceneAsync(sceneKey, loadSceneMode, activateOnload);

        LoadingStarted?.Invoke();
        ShowLoadingScreen = showLoadingScreen;

        while (asyncOperationHandle.Status != AsyncOperationStatus.Succeeded)
        {
            IsLoading?.Invoke(asyncOperationHandle.PercentComplete);

            yield return null;
        }

        if (activateOnload)
        {
            LoadingCompleted?.Invoke();

            yield break;
        }

        LoadingSucceeded?.Invoke();
        IsSceneLoaded = true;
        loadedSceneInstance = asyncOperationHandle.Result;
    }

    public static void ActivateLoadedScene()
    {
        loadedSceneInstance.ActivateAsync().completed += _ =>
        {
            IsSceneLoaded = false;
            loadedSceneInstance = default;
            LoadingCompleted?.Invoke();
        };
    }


    public static void LoadAddressableScene(object sceneKey,
        bool showLoadingScreen = false,
        bool loadSceneAdditively = false,
        bool activateOnload = false)
    {
        Instance.StartCoroutine(routine: LoadAddressableSceneCoroutine(sceneKey, showLoadingScreen, loadSceneAdditively, activateOnload));

    }

}
