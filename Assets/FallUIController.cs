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

    void Start()
    {
        InitDropdown();

        runButton.onClick.AddListener(OnRunClicked);
        stopButton.onClick.AddListener(OnStopClicked);
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

        int selected = fallDropdown.value - 1; // 0=Random → -1

        batchGenerator.captureDuration = duration;

        // ⭐ 统一入口（不再区分单次/批量）
        batchGenerator.StartBatch(count, selected);
    }

    void OnStopClicked()
    {
        if (batchGenerator != null)
            batchGenerator.StopBatch();
    }
    public void ExitGame()
    {
        Debug.Log("退出程序"); // 在编辑器里也能看到提示
        Application.Quit();
    }
}