using UnityEngine;
using System.Collections;
using System.IO;

public class RunAndExportController : MonoBehaviour
{
    public Animator animator;
    public CocoExporter cocoExporter;

    public string animationStateName = "Fall";
    public string outputRootFolder = "Output";

    private int runIndex = 0;
    private bool isRunning = false;

    public void OnRunButtonClicked()
    {
        if (isRunning) return;

        StartCoroutine(RunProcess());
    }

    IEnumerator RunProcess()
    {
        isRunning = true;

        runIndex++;
        string runFolder = CreateRunFolder(runIndex);

        animator.Play(animationStateName, 0, 0f);
        yield return null;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        float clipLength = state.length;

        cocoExporter.BeginRun(runFolder);

        float timer = 0f;

        while (timer < clipLength)
        {
            cocoExporter.CaptureFrame();

            timer += Time.deltaTime;
            yield return null;
        }

        Debug.Log("Run " + runIndex + " finished.");

        isRunning = false;
    }

    string CreateRunFolder(int index)
    {
        string exeFolder = Directory.GetParent(Application.dataPath).FullName;

        string root = Path.Combine(exeFolder, outputRootFolder);

        if (!Directory.Exists(root))
            Directory.CreateDirectory(root);

        string folder = Path.Combine(root, "Run_" + index.ToString("D4"));
        Directory.CreateDirectory(folder);

        return folder;
    }
}