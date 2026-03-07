using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class TransitionScreen : MonoBehaviour
{
    const string UssFade = "fade";
    VisualElement transitionImage;
    WaitUntil waitUntilSceneHasLoaded;

    public static event System.Action ShowLoadingScreen;

    void Awake()
    {
        transitionImage = GetComponent<UIDocument>().rootVisualElement.Q(name: "TransitionImage");
        waitUntilSceneHasLoaded = new WaitUntil(() => SceneLoader.IsSceneLoaded);
        SceneLoader.LoadingStarted += FadeOut;
        SceneLoader.LoadingCompleted += FadeIn;
    }

    IEnumerator ActivateLoadedSceneCoroutine()
    {
        yield return waitUntilSceneHasLoaded;

        SceneLoader.ActivateLoadedScene();
    }

    void FadeOut()
    {
        transitionImage.AddToClassList(UssFade);
        transitionImage.RegisterCallback<TransitionEndEvent>(OnFadedOutEnded);
    }

    void OnFadedOutEnded(TransitionEndEvent evt)
    {
        transitionImage.UnregisterCallback<TransitionEndEvent>(OnFadedOutEnded);

        if (SceneLoader.ShowLoadingScreen)
        {
            ShowLoadingScreen?.Invoke();
        }
        else
        {
            StartCoroutine(routine: ActivateLoadedSceneCoroutine());
        }
    }

    void FadeIn()
    {
        transitionImage.RemoveFromClassList(UssFade);
    }
}
