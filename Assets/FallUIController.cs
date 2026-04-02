using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class FallUIController : MonoBehaviour
{
    [Header("UI")]
    public TMP_Dropdown fallDropdown;
    public TMP_InputField countInput;
    public TMP_InputField durationInput;

    public Button runButton;
    public Button stopButton;

    [Header("System")]
    public BatchFallGenerator batchGenerator;

    [Header("Camera UI")]
    public TMP_InputField camPosX;
    public TMP_InputField camPosY;
    public TMP_InputField camPosZ;

    public TMP_InputField camRotX;
    public TMP_InputField camRotY;
    public TMP_InputField camRotZ;

    [Header("Camera")]
    public Camera captureCamera;

    [Header("Camera Mode")]
    public Toggle randomCameraToggle; // ✔ 勾选=随机，不勾选=手动

    void Start()
    {
        InitDropdown();
        InitCameraUI();
        runButton.onClick.AddListener(OnRunClicked);
        stopButton.onClick.AddListener(OnStopClicked);
    }
    void InitCameraUI()
    {
        if (captureCamera == null) return;

        Vector3 pos = captureCamera.transform.position;
        Vector3 rot = captureCamera.transform.eulerAngles;

        camPosX.text = pos.x.ToString("0");
        camPosY.text = pos.y.ToString("2");
        camPosZ.text = pos.z.ToString("0");

        camRotX.text = rot.x.ToString("0");
        camRotY.text = rot.y.ToString("270");
        camRotZ.text = rot.z.ToString("0");
    }
    void InitDropdown()
    {
        fallDropdown.ClearOptions();

        List<string> options = new List<string>()
        {
            "Random",
            "LeftReach",
            "RightReach",
            "PushCollision",
            "SitSupportLoss",
            "Slip",
            "StandBalanceLoss",
            "WalkingBalanceLoss",
            "TripWalking"
        };

        fallDropdown.AddOptions(options);
    }

    void OnRunClicked()
    {
        if (batchGenerator == null) return;

        int count = 1;
        float duration = 3f;

        int.TryParse(countInput.text, out count);
        float.TryParse(durationInput.text, out duration);

        int selected = fallDropdown.value - 1;

        batchGenerator.captureDuration = duration;

        // ⭐ 同步摄像机模式到生成器（非常重要）
        batchGenerator.useRandomCamera = (randomCameraToggle != null && randomCameraToggle.isOn);

        // ⭐ 统一摄像机入口
        ApplyCamera();

        batchGenerator.StartBatch(count, selected);
    }

    void ApplyCamera()
    {
        if (randomCameraToggle != null && randomCameraToggle.isOn)
        {
            // 随机模式
            batchGenerator.RandomizeCamera();
        }
        else
        {
            // 手动模式
            ApplyCameraManual();
        }
    }

    void ApplyCameraManual()
    {
        if (captureCamera == null) return;

        Vector3 pos = new Vector3(
            ParseSafe(camPosX.text),
            ParseSafe(camPosY.text),
            ParseSafe(camPosZ.text)
        );

        Vector3 rot = new Vector3(
            Mathf.Repeat(ParseSafe(camRotX.text), 360f),
            Mathf.Repeat(ParseSafe(camRotY.text), 360f),
            Mathf.Repeat(ParseSafe(camRotZ.text), 360f)
        );

        captureCamera.transform.position = pos;
        captureCamera.transform.rotation = Quaternion.Euler(rot);
    }

    float ParseSafe(string s)
    {
        float v;
        if (!float.TryParse(s, out v))
            v = 0f;
        return v;
    }

    void OnStopClicked()
    {
        if (batchGenerator != null)
            batchGenerator.StopBatch();
    }

    public void ExitGame()
    {
        Debug.Log("退出程序");
        Application.Quit();
    }
}