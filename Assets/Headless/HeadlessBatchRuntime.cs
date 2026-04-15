using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum HeadlessCameraMode
{
    Random,
    Manual
}

[Serializable]
public class HeadlessCliConfig
{
    public string SceneName = "SampleScene";
    public int FallType = -1;
    public int Count = 1;
    public float Duration = 3f;
    public string OutputDir = "Output";
    public int? Seed;
    public HeadlessCameraMode CameraMode = HeadlessCameraMode.Random;
    public bool HasCameraPosition;
    public bool HasCameraRotation;
    public Vector3 CameraPosition;
    public Vector3 CameraRotation;
    public bool HeadlessRequested;
}

public static class HeadlessCliState
{
    static HeadlessCliConfig pendingConfig;
    static bool hasPendingConfig;
    static int exitCode;
    static bool hasExitCode;
    static bool hasRuntimeRunner;

    public static void SetPendingConfig(HeadlessCliConfig config)
    {
        pendingConfig = config;
        hasPendingConfig = true;
    }

    public static bool TryConsumePendingConfig(out HeadlessCliConfig config)
    {
        config = pendingConfig;
        pendingConfig = null;

        bool hadConfig = hasPendingConfig;
        hasPendingConfig = false;

        return hadConfig;
    }

    public static void SetExitCode(int code)
    {
        exitCode = code;
        hasExitCode = true;
    }

    public static int ConsumeExitCodeOrDefault(int fallback)
    {
        int result = hasExitCode ? exitCode : fallback;
        hasExitCode = false;
        return result;
    }

    public static bool TryAcquireRuntimeRunner()
    {
        if (hasRuntimeRunner)
            return false;

        hasRuntimeRunner = true;
        return true;
    }

    public static void ReleaseRuntimeRunner()
    {
        hasRuntimeRunner = false;
    }
}

public static class HeadlessCliArgumentParser
{
    static readonly HashSet<string> KnownKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "scene",
        "falltype",
        "count",
        "duration",
        "outputdir",
        "seed",
        "cameramode",
        "randomcamera",
        "campos",
        "camrot",
        "camposx",
        "camposy",
        "camposz",
        "camrotx",
        "camroty",
        "camrotz",
        "headless"
    };

    public static bool TryParseArgs(string[] args, out HeadlessCliConfig config, out bool hasOptions, out string error)
    {
        config = new HeadlessCliConfig();
        hasOptions = false;
        error = null;

        Dictionary<string, string> map = ExtractKnownOptions(args, ref hasOptions);

        if (!hasOptions)
            return true;

        if (TryGetValue(map, "scene", out string sceneName))
            config.SceneName = sceneName;

        if (TryGetValue(map, "falltype", out string fallTypeRaw))
        {
            if (!int.TryParse(fallTypeRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int fallType))
            {
                error = "Invalid fallType. Expected integer in range -1..7.";
                return false;
            }
            config.FallType = fallType;
        }

        if (TryGetValue(map, "count", out string countRaw))
        {
            if (!int.TryParse(countRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
            {
                error = "Invalid count. Expected integer greater than 0.";
                return false;
            }
            config.Count = count;
        }

        if (TryGetValue(map, "duration", out string durationRaw))
        {
            if (!float.TryParse(durationRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out float duration))
            {
                error = "Invalid duration. Expected float greater than 0.";
                return false;
            }
            config.Duration = duration;
        }

        if (TryGetValue(map, "outputdir", out string outputDir))
            config.OutputDir = outputDir;

        if (TryGetValue(map, "seed", out string seedRaw))
        {
            if (!int.TryParse(seedRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed))
            {
                error = "Invalid seed. Expected integer.";
                return false;
            }
            config.Seed = seed;
        }

        if (TryGetValue(map, "randomcamera", out string randomCameraRaw))
        {
            if (!TryParseBool(randomCameraRaw, out bool randomCamera))
            {
                error = "Invalid randomCamera flag. Expected true/false.";
                return false;
            }

            config.CameraMode = randomCamera ? HeadlessCameraMode.Random : HeadlessCameraMode.Manual;
        }

        if (TryGetValue(map, "cameramode", out string cameraModeRaw))
        {
            if (cameraModeRaw.Equals("random", StringComparison.OrdinalIgnoreCase))
                config.CameraMode = HeadlessCameraMode.Random;
            else if (cameraModeRaw.Equals("manual", StringComparison.OrdinalIgnoreCase))
                config.CameraMode = HeadlessCameraMode.Manual;
            else
            {
                error = "Invalid cameraMode. Expected random or manual.";
                return false;
            }
        }

        if (TryGetValue(map, "campos", out string camPosRaw))
        {
            if (!TryParseVector3(camPosRaw, out Vector3 camPos))
            {
                error = "Invalid camPos. Expected x,y,z.";
                return false;
            }
            config.HasCameraPosition = true;
            config.CameraPosition = camPos;
        }

        if (TryGetValue(map, "camrot", out string camRotRaw))
        {
            if (!TryParseVector3(camRotRaw, out Vector3 camRot))
            {
                error = "Invalid camRot. Expected x,y,z.";
                return false;
            }
            config.HasCameraRotation = true;
            config.CameraRotation = camRot;
        }

        string camPosXRaw = null;
        string camPosYRaw = null;
        string camPosZRaw = null;
        bool hasCamPosX = TryGetValue(map, "camposx", out camPosXRaw);
        bool hasCamPosY = TryGetValue(map, "camposy", out camPosYRaw);
        bool hasCamPosZ = TryGetValue(map, "camposz", out camPosZRaw);

        if (hasCamPosX || hasCamPosY || hasCamPosZ)
        {
            if (!TryParseFloatOrDefault(camPosXRaw, config.CameraPosition.x, out float posX) ||
                !TryParseFloatOrDefault(camPosYRaw, config.CameraPosition.y, out float posY) ||
                !TryParseFloatOrDefault(camPosZRaw, config.CameraPosition.z, out float posZ))
            {
                error = "Invalid camPosX/camPosY/camPosZ values.";
                return false;
            }

            config.HasCameraPosition = true;
            config.CameraPosition = new Vector3(posX, posY, posZ);
        }

        string camRotXRaw = null;
        string camRotYRaw = null;
        string camRotZRaw = null;
        bool hasCamRotX = TryGetValue(map, "camrotx", out camRotXRaw);
        bool hasCamRotY = TryGetValue(map, "camroty", out camRotYRaw);
        bool hasCamRotZ = TryGetValue(map, "camrotz", out camRotZRaw);

        if (hasCamRotX || hasCamRotY || hasCamRotZ)
        {
            if (!TryParseFloatOrDefault(camRotXRaw, config.CameraRotation.x, out float rotX) ||
                !TryParseFloatOrDefault(camRotYRaw, config.CameraRotation.y, out float rotY) ||
                !TryParseFloatOrDefault(camRotZRaw, config.CameraRotation.z, out float rotZ))
            {
                error = "Invalid camRotX/camRotY/camRotZ values.";
                return false;
            }

            config.HasCameraRotation = true;
            config.CameraRotation = new Vector3(rotX, rotY, rotZ);
        }

        if (TryGetValue(map, "headless", out string headlessRaw))
        {
            if (!TryParseBool(headlessRaw, out bool headless))
            {
                error = "Invalid headless flag. Expected true/false.";
                return false;
            }
            config.HeadlessRequested = headless;
        }
        else
        {
            config.HeadlessRequested = hasOptions;
        }

        if (string.IsNullOrWhiteSpace(config.SceneName))
        {
            error = "scene must not be empty.";
            return false;
        }

        if (config.Count <= 0)
        {
            error = "count must be greater than 0.";
            return false;
        }

        if (config.Duration <= 0f)
        {
            error = "duration must be greater than 0.";
            return false;
        }

        if (config.FallType < -1 || config.FallType > 7)
        {
            error = "fallType must be in range -1..7.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(config.OutputDir))
        {
            error = "outputDir must not be empty.";
            return false;
        }

        return true;
    }

    static Dictionary<string, string> ExtractKnownOptions(string[] args, ref bool hasOptions)
    {
        Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < args.Length; i++)
        {
            string token = args[i];
            if (string.IsNullOrWhiteSpace(token) || token == "--")
                continue;

            string key;
            string value;

            int sep = token.IndexOf('=');
            if (sep >= 0)
            {
                key = CanonicalizeKey(token.Substring(0, sep));
                value = token.Substring(sep + 1);
            }
            else
            {
                key = CanonicalizeKey(token);
                if (key == null)
                    continue;

                bool hasNextValue = i + 1 < args.Length && args[i + 1] != "--" && !LooksLikeFlag(args[i + 1]);
                if (hasNextValue)
                {
                    value = args[i + 1];
                    i++;
                }
                else
                {
                    value = "true";
                }
            }

            if (key == null || !KnownKeys.Contains(key))
                continue;

            hasOptions = true;
            map[key] = TrimQuotes(value);
        }

        return map;
    }

    static bool LooksLikeFlag(string token)
    {
        return token.StartsWith("-") || token.StartsWith("/");
    }

    static string CanonicalizeKey(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        string key = raw.Trim();
        while (key.StartsWith("-") || key.StartsWith("/"))
            key = key.Substring(1);

        key = key.Trim().ToLowerInvariant();

        switch (key)
        {
            case "scene":
            case "scenename":
                return "scene";
            case "falltype":
            case "fall":
                return "falltype";
            case "count":
            case "runs":
            case "batchcount":
                return "count";
            case "duration":
            case "captureduration":
                return "duration";
            case "outputdir":
            case "output":
            case "outputpath":
                return "outputdir";
            case "seed":
                return "seed";
            case "cameramode":
            case "mode":
                return "cameramode";
            case "randomcamera":
                return "randomcamera";
            case "campos":
            case "camerapos":
                return "campos";
            case "camrot":
            case "camerarot":
                return "camrot";
            case "camposx":
                return "camposx";
            case "camposy":
                return "camposy";
            case "camposz":
                return "camposz";
            case "camrotx":
                return "camrotx";
            case "camroty":
                return "camroty";
            case "camrotz":
                return "camrotz";
            case "headless":
                return "headless";
            default:
                return null;
        }
    }

    static bool TryGetValue(Dictionary<string, string> map, string key, out string value)
    {
        if (map.TryGetValue(key, out value))
        {
            value = value.Trim();
            return true;
        }

        value = null;
        return false;
    }

    static bool TryParseVector3(string raw, out Vector3 value)
    {
        value = Vector3.zero;

        if (string.IsNullOrWhiteSpace(raw))
            return false;

        string[] parts = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
            return false;

        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x))
            return false;
        if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
            return false;
        if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            return false;

        value = new Vector3(x, y, z);
        return true;
    }

    static bool TryParseBool(string raw, out bool value)
    {
        string text = raw.Trim().ToLowerInvariant();

        if (text == "1" || text == "true" || text == "yes" || text == "on")
        {
            value = true;
            return true;
        }

        if (text == "0" || text == "false" || text == "no" || text == "off")
        {
            value = false;
            return true;
        }

        value = false;
        return false;
    }

    static bool TryParseFloatOrDefault(string raw, float fallback, out float value)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            value = fallback;
            return true;
        }

        return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    static string TrimQuotes(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        string result = value.Trim();
        if (result.Length >= 2 && result[0] == '"' && result[result.Length - 1] == '"')
            result = result.Substring(1, result.Length - 2);

        return result;
    }
}

public class HeadlessBatchRuntime : MonoBehaviour
{
    const string LogPrefix = "[HeadlessCLI]";

    HeadlessCliConfig config;
    BatchFallGenerator generator;
    bool quitIssued;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (!Application.isBatchMode)
            return;

        if (HeadlessCliState.TryConsumePendingConfig(out HeadlessCliConfig pendingConfig))
        {
            if (!HeadlessCliState.TryAcquireRuntimeRunner())
                return;

            StartRunner(pendingConfig);
            return;
        }

        bool hasOptions;
        string parseError;
        bool ok = HeadlessCliArgumentParser.TryParseArgs(
            Environment.GetCommandLineArgs(),
            out HeadlessCliConfig parsedConfig,
            out hasOptions,
            out parseError
        );

        if (!hasOptions)
            return;

        if (!ok)
        {
            FailImmediately(11, parseError);
            return;
        }

        if (!parsedConfig.HeadlessRequested)
            return;

        if (!HeadlessCliState.TryAcquireRuntimeRunner())
            return;

        StartRunner(parsedConfig);
    }

    static void StartRunner(HeadlessCliConfig config)
    {
        GameObject go = new GameObject("HeadlessBatchRuntime");
        DontDestroyOnLoad(go);

        HeadlessBatchRuntime runner = go.AddComponent<HeadlessBatchRuntime>();
        runner.Initialize(config);
    }

    public void Initialize(HeadlessCliConfig incomingConfig)
    {
        config = incomingConfig;
    }

    void Awake()
    {
        if (config == null)
            config = new HeadlessCliConfig();
    }

    IEnumerator Start()
    {
        if (!string.IsNullOrWhiteSpace(config.SceneName))
        {
            string currentSceneName = SceneManager.GetActiveScene().name;
            if (!string.Equals(currentSceneName, config.SceneName, StringComparison.OrdinalIgnoreCase))
            {
                AsyncOperation op;
                try
                {
                    op = SceneManager.LoadSceneAsync(config.SceneName, LoadSceneMode.Single);
                }
                catch (Exception ex)
                {
                    Fail(24, $"scene '{config.SceneName}' load failed: {ex.Message}");
                    yield break;
                }

                if (op == null)
                {
                    Fail(24, $"scene '{config.SceneName}' could not be loaded in player/runtime.");
                    yield break;
                }

                while (!op.isDone)
                    yield return null;

                yield return null;
            }
        }

        DisableConflictingComponents();

        yield return null;
        BeginRun();
    }

    void DisableConflictingComponents()
    {
        BatchFallTester tester = FindObjectOfType<BatchFallTester>();
        if (tester != null)
            tester.enabled = false;

        FallUIController ui = FindObjectOfType<FallUIController>();
        if (ui != null)
            ui.enabled = false;
    }

    void BeginRun()
    {
        generator = FindObjectOfType<BatchFallGenerator>();
        if (generator == null)
        {
            Fail(21, "BatchFallGenerator not found in active scene.");
            return;
        }

        if (generator.captureCamera == null)
            generator.captureCamera = FindObjectOfType<Camera>();

        if (generator.captureCamera == null)
        {
            Fail(22, "No Camera found for capture.");
            return;
        }

        if (generator.cocoExporter == null)
        {
            Fail(23, "CocoExporter is not assigned on BatchFallGenerator.");
            return;
        }

        if (generator.cocoExporter.cam == null)
            generator.cocoExporter.cam = generator.captureCamera;

        generator.captureDuration = config.Duration;
        generator.useRandomCamera = config.CameraMode == HeadlessCameraMode.Random;
        generator.ConfigureOutputRoot(config.OutputDir);
        generator.ConfigureSeed(config.Seed);

        if (config.CameraMode == HeadlessCameraMode.Manual)
        {
            Vector3 pos = config.HasCameraPosition
                ? config.CameraPosition
                : generator.captureCamera.transform.position;

            Vector3 rot = config.HasCameraRotation
                ? config.CameraRotation
                : generator.captureCamera.transform.eulerAngles;

            generator.ApplyManualCamera(pos, rot);
        }

        generator.OnBatchCompleted += OnBatchCompleted;

        Debug.Log(
            $"{LogPrefix} start scene={config.SceneName} fallType={config.FallType} count={config.Count} duration={config.Duration} outputDir={config.OutputDir} cameraMode={config.CameraMode}"
        );

        generator.StartBatch(config.Count, config.FallType);
    }

    void OnBatchCompleted()
    {
        if (generator != null)
            generator.OnBatchCompleted -= OnBatchCompleted;

        Debug.Log($"{LogPrefix} batch completed successfully.");
        QuitWithCode(0);
    }

    void Fail(int code, string message)
    {
        if (generator != null)
            generator.OnBatchCompleted -= OnBatchCompleted;

        Debug.LogError($"{LogPrefix} {message}");
        QuitWithCode(code);
    }

    static void FailImmediately(int code, string message)
    {
        Debug.LogError($"{LogPrefix} {message}");
        HeadlessCliState.SetExitCode(code);
        HeadlessCliState.ReleaseRuntimeRunner();

#if UNITY_EDITOR
        if (Application.isBatchMode)
        {
            if (UnityEditor.EditorApplication.isPlaying)
                UnityEditor.EditorApplication.isPlaying = false;
            else
                UnityEditor.EditorApplication.Exit(code);
            return;
        }
#endif

        Application.Quit(code);
    }

    void QuitWithCode(int code)
    {
        if (quitIssued) return;
        quitIssued = true;

        HeadlessCliState.SetExitCode(code);
        HeadlessCliState.ReleaseRuntimeRunner();

#if UNITY_EDITOR
        if (Application.isBatchMode)
        {
            UnityEditor.EditorApplication.isPlaying = false;
            return;
        }
#endif

        Application.Quit(code);
    }
}
