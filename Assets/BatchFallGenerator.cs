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
    Coroutine currentRoutine;
    bool running;
    public void StartBatch(int count, int fallType)
    {
        if (running) return;

        currentRoutine = StartCoroutine(RunBatch(count, fallType));
    }
    void Update()
    {
        // 批量生成
        
    }

    IEnumerator RunBatch(int batchCount, int selectedFall)
    {
        running = true;

        for (int i = 0; i < batchCount; i++)
        {
            RandomizeCamera();

            string folder = CreateRunFolder(i + 1);

            PlayRandomFall(selectedFall);

            cocoExporter.BeginRun(folder);

            float timer = 0f;

            while (timer < captureDuration)
            {
                cocoExporter.CaptureFrame();

                timer += Time.deltaTime;
                yield return null;
                if (!running) yield break; // ⭐ 支持中断
            }
        }

        running = false;
    }

    void PlayRandomFall(int selectedFall)
    {
        // 如果传入 -1，随机生成 1 ~ 7 的数字
        if (selectedFall == -1)
        {
            selectedFall = Random.Range(1, 8); // 🟢 注意：Unity 整数是 [min,inclusive) → 1~7
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

    void RandomizeCamera()
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
    public void StopBatch()
    {
        if (!running) return;

        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        running = false;

        Debug.Log("Batch Stopped");
    }
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