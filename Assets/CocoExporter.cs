using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class CocoExporter : MonoBehaviour
{
    public Camera cam;
    public Transform[] keypoints;

    string folder;
    int frameIndex;

    public void BeginRun(string folderPath)
    {
        folder = folderPath;
        frameIndex = 0;
    }

    public void CaptureFrame()
    {
        if (folder == null) return;

        frameIndex++;

        List<float> kp = new List<float>();

        foreach (var t in keypoints)
        {
            if (t == null)
            {
                kp.AddRange(new float[] { 0, 0, 0 });
                continue;
            }

            Vector3 s =
                cam.WorldToScreenPoint(t.position);

            kp.Add(s.x);
            kp.Add(s.y);
            kp.Add(2);
        }

        string json =
            JsonUtility.ToJson(
                new FrameData { keypoints = kp.ToArray() },
                true
            );

        string file =
            Path.Combine(
                folder,
                "frame_" + frameIndex.ToString("D4") + ".json"
            );

        File.WriteAllText(file, json);
    }

    [System.Serializable]
    class FrameData
    {
        public float[] keypoints;
    }
}