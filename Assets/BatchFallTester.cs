using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class BatchFallTester : MonoBehaviour
{
    public BatchFallGenerator generator;

    [Header("Test Config")]
    public int runsPerType = 100;

    public float groundY = 0f;
    public float maxVelocity = 20f;
    public float maxAngularVelocity = 50f;
    public int explosionFramesThreshold = 5;

    int fallTypeCount = 8;

    public enum ErrorType
    {
        None = 0,
        NaN = 1 << 0,
        Penetration = 1 << 1,
        LinearExplosion = 1 << 2,
        AngularExplosion = 1 << 3
    }

    class FallStats
    {
        public int success = 0;
        public int total = 0;

        public Dictionary<ErrorType, int> errorCount = new Dictionary<ErrorType, int>();

        // 分开记录帧级
        public int linearExplosionFrames = 0;
        public int angularExplosionFrames = 0;
    }

    Dictionary<int, FallStats> allStats = new Dictionary<int, FallStats>();
    string reportPath;

    IEnumerator Start()
    {
        string root = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Output");
        reportPath = Path.Combine(root, "FinalReport.txt");

        yield return null;
        yield return null;
        yield return new WaitForSeconds(0.1f);

        StartCoroutine(RunAllTests());
    }

    IEnumerator RunAllTests()
    {
        Debug.Log("===== START FULL TEST =====");

        for (int fallType = 0; fallType < fallTypeCount; fallType++)
        {
            Debug.Log($"--- Testing FallType {fallType} ---");
            FallStats stats = new FallStats();
            allStats[fallType] = stats;

            for (int i = 0; i < runsPerType; i++)
            {
                yield return StartCoroutine(RunSingle(fallType, stats, i));
            }
        }

        WriteFinalReport();
        Debug.Log("===== ALL TEST DONE =====");

        // ⭐ 测试完成后自动退出 Unity 编辑器的播放模式
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#endif
    }

    IEnumerator RunSingle(int fallType, FallStats stats, int index)
    {
        stats.total++;

        generator.StartBatch(1, fallType);

        int linearExplosionFrames = 0;
        int angularExplosionFrames = 0;

        int linearConsecutive = 0;
        int angularConsecutive = 0;
        bool linearError = false;
        bool angularError = false;
        ErrorType caseError = ErrorType.None;

        while (generator.IsRunning())
        {
            var rbList = generator.GetRagdollBodies();
            bool anyLinear = false;
            bool anyAngular = false;

            foreach (var rb in rbList)
            {
                if (IsInvalid(rb.position) || IsInvalid(rb.velocity))
                    caseError |= ErrorType.NaN;

                if (rb.position.y < groundY - 0.1f)
                    caseError |= ErrorType.Penetration;

                if (rb.velocity.magnitude > maxVelocity)
                    anyLinear = true;
                if (rb.angularVelocity.magnitude > maxAngularVelocity)
                    anyAngular = true;
            }

            // 更新连续计数
            linearConsecutive = anyLinear ? linearConsecutive + 1 : 0;
            angularConsecutive = anyAngular ? angularConsecutive + 1 : 0;

            if (linearConsecutive >= explosionFramesThreshold)
                linearError = true;
            if (angularConsecutive >= explosionFramesThreshold)
                angularError = true;

            // 记录总爆炸帧数（用于统计平均值）
            if (anyLinear) linearExplosionFrames++;
            if (anyAngular) angularExplosionFrames++;

            yield return null;
        }

        stats.linearExplosionFrames += linearExplosionFrames;
        stats.angularExplosionFrames += angularExplosionFrames;

        if (linearError) caseError |= ErrorType.LinearExplosion;
        if (angularError) caseError |= ErrorType.AngularExplosion;

        if (caseError == ErrorType.None)
            stats.success++;
        else
        {
            foreach (ErrorType type in System.Enum.GetValues(typeof(ErrorType)))
            {
                if (type == ErrorType.None) continue;
                if (caseError.HasFlag(type))
                {
                    if (!stats.errorCount.ContainsKey(type))
                        stats.errorCount[type] = 0;
                    stats.errorCount[type]++;
                }
            }
        }
    }

    bool IsInvalid(Vector3 v)
    {
        return float.IsNaN(v.x) || float.IsInfinity(v.x) ||
               float.IsNaN(v.y) || float.IsInfinity(v.y) ||
               float.IsNaN(v.z) || float.IsInfinity(v.z);
    }

    void WriteFinalReport()
    {
        List<string> lines = new List<string>();
        lines.Add("===== FINAL REPORT =====");

        foreach (var kv in allStats)
        {
            int type = kv.Key;
            var s = kv.Value;
            float successRate = s.total > 0 ? (float)s.success / s.total : 0f;

            lines.Add($"\n--- FallType {type} ---");
            lines.Add($"Total: {s.total}");
            lines.Add($"Success: {s.success}");
            lines.Add($"Success Rate: {successRate * 100f}%");
            lines.Add("Errors:");

            foreach (var err in s.errorCount)
                lines.Add($"{err.Key}: {err.Value}");
        }

        File.WriteAllLines(reportPath, lines);
        Debug.Log($"Report saved to: {reportPath}");
    }
}