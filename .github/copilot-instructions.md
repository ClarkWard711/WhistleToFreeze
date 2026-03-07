# Copilot Instructions for "Whistle to Freeze" Unity Project

## Architecture Overview
This is a 2D Unity game using Addressables for asset and scene management. Key components:
- **GameManager**: Singleton instantiated pre-scene load via `[RuntimeInitializeOnLoadMethod]`, handles DontDestroyOnLoad.
- **SceneLoader**: Central scene management with Addressables, supports additive loading, loading screens, and progress events.
- **PlayerController**: Singleton player movement controller using Rigidbody2D and Animator.
- **TransitionScreen**: UI Toolkit-based loading transitions tied to SceneLoader events.

## Key Patterns
- **Addressables Usage**: Scenes and assets loaded by string keys (e.g., "MainMenu", "BattleScene"). Use `Addressables.LoadSceneAsync()` for scenes, `Addressables.InstantiateAsync()` for prefabs.
- **Singleton Pattern**: Implemented manually with static Instance checks in Awake(), e.g., in PlayerController and GameManager.
- **Event-Driven Loading**: SceneLoader fires events (LoadingStarted, IsLoading, LoadingCompleted) for UI feedback.
- **UI Toolkit**: Use VisualElement queries and USS classes for transitions, e.g., `AddToClassList("fade")` in TransitionScreen.
- **Scene Constants**: Define scene keys as const strings in SceneLoader (e.g., `public const string MainMenuSceneKey = "MainMenu";`).

## Workflows
- **Scene Loading**: Call `SceneLoader.LoadAddressableScene(sceneKey, showLoadingScreen: true)` for transitions with UI.
- **Player Setup**: Attach PlayerController to player GameObject; it auto-finds Camera in children and sets up Rigidbody2D/Animator.
- **Build**: Standard Unity build process; ensure Addressable groups are built for asset bundles.

## Conventions
- Scripts in `Assets/Scripts/` with subfolders (Core/, UI/).
- Use Unity's Input system for movement (GetAxis, GetButton).
- Animator parameters: "running" bool for movement state.
- Flip sprite via `transform.localScale = new Vector3(direction, 1, 1);` based on horizontal input.

## Dependencies
- Addressables (1.19.19) for dynamic loading.
- UI Toolkit for modern UI.
- Cinemachine, TextMeshPro, etc., for enhanced features.

Reference: [SceneLoader.cs](Assets/Scripts/Core/SceneLoader.cs), [PlayerController.cs](Assets/Scripts/PlayerController.cs), [TransitionScreen.cs](Assets/Scripts/UI/TransitionScreen.cs)