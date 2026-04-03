using System.Collections;
using UnityEngine;
using System.IO;

public class BatchFallGenerator : MonoBehaviour
{
    [Header("Reach Controllers")]
    public ReachObjectAnimation leftController;
    public ReachObjectAnimation_Right rightController;

    [Header("Fall Controllers")]
    public Fall_PushCollision fallPushCollision;
    public Fall_SitSupportLoss fallSitSupportLoss;
    public Fall_Slip fallSlip;
    public Fall_StandBalanceLoss fallStandBalanceLoss;
    public Fall_WalkingBalanceLoss fallWalkingBalanceLoss;
    public Fall_TripWalking fallTripWalking;

    [Header("Character")]
    public Transform hips;

    [Header("Camera")]
    public Camera captureCamera;

    [Header("Exporter")]
    public CocoExporter cocoExporter;

    [Header("Batch Settings")]
    public int batchCount = 5;
    public float captureDuration = 3f;
    public bool useRandomCamera = true;

    [Header("Test Settings")]
    public float groundY = 0f;
    public float maxVelocity = 20f;
    public float maxAngularVelocity = 50f;

    Coroutine currentRoutine;
    bool running;

    Rigidbody[] ragdollBodies;

    // ===== 测试统计 =====
    int successCount = 0;
    int failCount = 0;

    ErrorType currentError;

    [System.Flags]
    public enum ErrorType
    {
        None = 0,
        NaN = 1 << 0,
        Penetration = 1 << 1,
        Explosion = 1 << 2,
        Stuck = 1 << 3
    }

    void Start()
    {
        ragdollBodies = hips.GetComponentsInChildren<Rigidbody>();
    }

    public void StartBatch(int count, int fallType)
    {
        if (running) return;

        successCount = 0;
        failCount = 0;

        currentRoutine = StartCoroutine(RunBatch(count, fallType));
    }

    IEnumerator RunBatch(int batchCount, int selectedFall)
    {
        running = true;

        for (int i = 0; i < batchCount; i++)
        {
            if (useRandomCamera)
            {
                RandomizeCamera();
            }

            string folder = CreateRunFolder(i + 1);

            PlayRandomFall(selectedFall);

            cocoExporter.BeginRun(folder);

            float timer = 0f;
            currentError = ErrorType.None;

            while (timer < captureDuration)
            {
                cocoExporter.CaptureFrame();

                // ⭐ 实时检测异常
                currentError |= DetectErrors();
                /*
                // ⭐ 提前终止异常 case
                if (currentError != ErrorType.None)
                {
                    Debug.Log($"Case {i} 提前终止: {currentError}");
                    break;
                }*/

                timer += Time.deltaTime;
                yield return null;

                if (!running) yield break;
            }

            // ⭐ 统计结果
            if (currentError == ErrorType.None)
            {
                successCount++;
            }
            else
            {
                failCount++;
                LogCase(folder, i, currentError);
            }
        }

        running = false;

        // ⭐ 输出统计
        float total = successCount + failCount;
        float stability = total > 0 ? successCount / total : 0f;

        Debug.Log($"===== Batch Result =====");
        Debug.Log($"Total: {total}");
        Debug.Log($"Success: {successCount}");
        Debug.Log($"Fail: {failCount}");
        Debug.Log($"Stability: {stability * 100f}%");
    }

    // =============================
    // 异常检测核心
    // =============================
    bool IsInvalid(Vector3 v)
    {
        return float.IsNaN(v.x) || float.IsInfinity(v.x) ||
               float.IsNaN(v.y) || float.IsInfinity(v.y) ||
               float.IsNaN(v.z) || float.IsInfinity(v.z);
    }

    ErrorType DetectErrors()
    {
        ErrorType errors = ErrorType.None;

        foreach (var rb in ragdollBodies)
        {
            if (IsInvalid(rb.position) || IsInvalid(rb.velocity))
                errors |= ErrorType.NaN;

            if (rb.velocity.magnitude > maxVelocity)
                errors |= ErrorType.Explosion;

            if (rb.angularVelocity.magnitude > maxAngularVelocity)
                errors |= ErrorType.Explosion;
        }

        if (hips.position.y < groundY - 0.1f)
            errors |= ErrorType.Penetration;

        return errors;
    }

    // =============================
    // 日志记录
    // =============================
    void LogCase(string folder, int index, ErrorType error)
    {
        string logPath = Path.Combine(folder, "error_log.txt");

        string log =
            $"Case {index} | Error: {error} | Time: {Time.time}";

        File.AppendAllText(logPath, log + "\n");

        Debug.Log(log);
    }

    // =============================
    // 跌倒选择
    // =============================
    void PlayRandomFall(int selectedFall)
    {
        if (selectedFall == -1)
        {
            selectedFall = Random.Range(1, 8);
        }

        switch (selectedFall)
        {
            case 0:
                leftController.Play();
                break;
            case 1:
                rightController.Play();
                break;
            case 2:
                fallPushCollision.Play();
                break;
            case 3:
                fallSitSupportLoss.Play();
                break;
            case 4:
                fallSlip.Play();
                break;
            case 5:
                fallStandBalanceLoss.Play();
                break;
            case 6:
                fallWalkingBalanceLoss.Play();
                break;
            case 7:
                fallTripWalking.Play();
                break;
        }
    }

    // =============================
    // 摄像机随机
    // =============================
    public void RandomizeCamera()
    {
        Vector3 center = hips.position + Vector3.up * 0.9f;

        float distance = Random.Range(2.5f, 4.5f);
        float height = Random.Range(0.6f, 1.8f);
        float angle = Random.Range(-120f, 120f);

        Vector3 dir =
            Quaternion.Euler(0f, angle, 0f) *
            Vector3.forward;

        Vector3 camPos =
            center +
            dir * distance +
            Vector3.up * height;

        captureCamera.transform.position = camPos;
        captureCamera.transform.LookAt(center);
    }

    // =============================
    // 停止
    // =============================
    public void StopBatch()
    {
        if (!running) return;

        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        running = false;

        Debug.Log("Batch Stopped");
    }

    // =============================
    // 创建输出目录
    // =============================
    string CreateRunFolder(int index)
    {
        string exeFolder =
            Directory.GetParent(Application.dataPath).FullName;

        string root =
            Path.Combine(exeFolder, "Output");

        if (!Directory.Exists(root))
            Directory.CreateDirectory(root);

        string folder =
            Path.Combine(root, "Run_" + index.ToString("D4"));

        Directory.CreateDirectory(folder);

        return folder;
    }
}