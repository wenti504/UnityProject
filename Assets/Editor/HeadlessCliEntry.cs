#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HeadlessCliEntry
{
    const string LogPrefix = "[HeadlessCLI]";

    static bool callbackRegistered;

    public static void Execute()
    {
        try
        {
            bool hasOptions;
            string parseError;
            bool ok = HeadlessCliArgumentParser.TryParseArgs(
                Environment.GetCommandLineArgs(),
                out HeadlessCliConfig config,
                out hasOptions,
                out parseError
            );

            if (!ok)
            {
                ExitImmediately(1, parseError);
                return;
            }

            if (!hasOptions)
                Debug.LogWarning($"{LogPrefix} no CLI options detected, defaults will be used.");

            if (!TryResolveScenePath(config.SceneName, out string scenePath))
            {
                ExitImmediately(2, $"scene '{config.SceneName}' not found in Build Settings.");
                return;
            }

            RegisterPlayModeCallback();
            HeadlessCliState.SetPendingConfig(config);

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            Debug.Log(
                $"{LogPrefix} prepared scene={config.SceneName} path={scenePath}. Entering PlayMode."
            );

            EditorApplication.isPlaying = true;
        }
        catch (Exception ex)
        {
            ExitImmediately(99, $"Unhandled exception: {ex}");
        }
    }

    static bool TryResolveScenePath(string sceneName, out string scenePath)
    {
        scenePath = null;

        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            string shortName = Path.GetFileNameWithoutExtension(scene.path);
            if (string.Equals(shortName, sceneName, StringComparison.OrdinalIgnoreCase))
            {
                scenePath = scene.path;
                return true;
            }
        }

        return false;
    }

    static void RegisterPlayModeCallback()
    {
        if (callbackRegistered) return;

        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        callbackRegistered = true;
    }

    static void UnregisterPlayModeCallback()
    {
        if (!callbackRegistered) return;

        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        callbackRegistered = false;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        int exitCode = HeadlessCliState.ConsumeExitCodeOrDefault(0);
        UnregisterPlayModeCallback();

        Debug.Log($"{LogPrefix} batch finished with exitCode={exitCode}.");
        EditorApplication.Exit(exitCode);
    }

    static void ExitImmediately(int code, string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            Debug.LogError($"{LogPrefix} {message}");

        UnregisterPlayModeCallback();
        HeadlessCliState.SetExitCode(code);
        EditorApplication.Exit(code);
    }
}
#endif
