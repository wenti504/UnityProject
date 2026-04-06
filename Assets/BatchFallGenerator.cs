using System.Collections;
using System.IO;
using UnityEngine;

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

    Coroutine currentRoutine;
    bool running;

    public Rigidbody[] GetRagdollBodies() => hips.GetComponentsInChildren<Rigidbody>();

    public bool IsRunning() => running;

    public void StartBatch(int count, int selectedFall = -1)
    {
        if (running) return;
        currentRoutine = StartCoroutine(RunBatch(count, selectedFall));
    }

    IEnumerator RunBatch(int count, int selectedFall)
    {
        running = true;

        for (int i = 0; i < count; i++)
        {
            if (useRandomCamera) RandomizeCamera();
            string folder = CreateRunFolder(selectedFall, i + 1);

            PlayFall(selectedFall);
            cocoExporter.BeginRun(folder);

            float timer = 0f;
            while (timer < captureDuration)
            {
                cocoExporter.CaptureFrame();
                timer += Time.deltaTime;
                yield return null;
            }
        }

        running = false;
    }

    void PlayFall(int fallType)
    {
        if (fallType == -1) fallType = Random.Range(0, 8);

        switch (fallType)
        {
            case 0: leftController.Play(); break;
            case 1: rightController.Play(); break;
            case 2: fallPushCollision.Play(); break;
            case 3: fallSitSupportLoss.Play(); break;
            case 4: fallSlip.Play(); break;
            case 5: fallStandBalanceLoss.Play(); break;
            case 6: fallWalkingBalanceLoss.Play(); break;
            case 7: fallTripWalking.Play(); break;
        }
    }

    public void RandomizeCamera()
    {
        Vector3 center = hips.position + Vector3.up * 0.9f;
        float distance = Random.Range(2.5f, 4.5f);
        float height = Random.Range(0.6f, 1.8f);
        float angle = Random.Range(-120f, 120f);

        Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
        Vector3 camPos = center + dir * distance + Vector3.up * height;

        captureCamera.transform.position = camPos;
        captureCamera.transform.LookAt(center);
    }

    string CreateRunFolder(int fallType, int index)
    {
        string exeFolder = Directory.GetParent(Application.dataPath).FullName;
        string root = Path.Combine(exeFolder, "Output");
        if (!Directory.Exists(root)) Directory.CreateDirectory(root);

        string folder = Path.Combine(root, $"Run_Fall{fallType}_{index:D4}");
        Directory.CreateDirectory(folder);
        return folder;
    }

    public void StopBatch()
    {
        if (!running) return;
        if (currentRoutine != null) StopCoroutine(currentRoutine);
        running = false;
        Debug.Log("Batch generation stopped.");
    }
}