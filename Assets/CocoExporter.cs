using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class CocoExporter : MonoBehaviour
{
    public Camera cam;
    public Transform[] keypoints;

    private string folder;
    private int frameIndex;

    public void BeginRun(string folderPath)
    {
        folder = folderPath;
        frameIndex = 0;
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);
    }

    public void CaptureFrame()
    {
        if (string.IsNullOrEmpty(folder) || cam == null || keypoints == null || keypoints.Length == 0)
        {
            Debug.LogError("请设置 folder、cam 和 keypoints");
            return;
        }

        frameIndex++;
        List<float> kpList = new List<float>();

        foreach (var t in keypoints)
        {
            if (t == null)
            {
                kpList.AddRange(new float[] { 0, 0, 0 });
                continue;
            }

            Vector3 screenPos = cam.WorldToScreenPoint(t.position);
            float x = screenPos.x;
            float y = screenPos.y;
            int v;

            // 在相机后方
            if (screenPos.z < 0)
            {
                v = 0;
            }
            // 超出屏幕边界
            else if (x < 0 || x > Screen.width || y < 0 || y > Screen.height)
            {
                v = 0;
            }
            else
            {
                v = 2;

                // 遮挡检测：从关键点射向相机，中间有物体则 v=1
                // 注意：遮挡物需要有 Collider，否则射线会穿透
                Vector3 toCamera = cam.transform.position - t.position;
                float distToCam = toCamera.magnitude;

                if (Physics.Raycast(t.position, toCamera.normalized, out RaycastHit hit, distToCam))
                {
                    if (hit.transform != cam.transform)
                    {
                        v = 1;
                    }
                }
            }

            kpList.Add(x);
            kpList.Add(y);
            kpList.Add(v);
        }

        FrameData data = new FrameData { keypoints = kpList.ToArray() };
        string json = JsonUtility.ToJson(data, true);
        string path = Path.Combine(folder, $"frame_{frameIndex:D4}.json");
        File.WriteAllText(path, json);
    }

    [System.Serializable]
    public class FrameData
    {
        public float[] keypoints;
    }
}