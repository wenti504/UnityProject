using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;

public class CocoExporter : MonoBehaviour
{
    public Camera cam;
    public Transform[] keypoints;

    [Header("Auto Resolve")]
    public bool autoResolveIfEmpty = true;
    public Transform keypointRoot;

    string folder;
    int frameIndex;
    bool loggedMissingBindings;
    static readonly string[] PreferredBoneNames =
    {
        "Hips", "Spine", "Chest", "Neck", "Head",
        "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand",
        "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand",
        "LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
        "RightUpperLeg", "RightLowerLeg", "RightFoot"
    };

    public void BeginRun(string folderPath)
    {
        folder = folderPath;
        frameIndex = 0;
        EnsureBindings();
    }

    public void CaptureFrame()
    {
        if (folder == null) return;

        EnsureBindings();
        if (cam == null || keypoints == null || keypoints.Length == 0)
            return;

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

    void EnsureBindings()
    {
        if (cam == null)
            cam = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();

        if ((keypoints == null || keypoints.Length == 0) && autoResolveIfEmpty)
            keypoints = ResolveKeypoints();

        if (!loggedMissingBindings && (cam == null || keypoints == null || keypoints.Length == 0))
        {
            loggedMissingBindings = true;
            Debug.LogError("CocoExporter bindings are incomplete. Ensure camera and keypoints are available.");
        }
    }

    Transform[] ResolveKeypoints()
    {
        Transform root = keypointRoot != null ? keypointRoot : transform;
        if (root == null) return Array.Empty<Transform>();

        List<Transform> result = new List<Transform>();
        HashSet<Transform> seen = new HashSet<Transform>();

        Dictionary<string, Transform> byName = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!byName.ContainsKey(t.name))
                byName[t.name] = t;
        }

        foreach (string name in PreferredBoneNames)
        {
            if (byName.TryGetValue(name, out Transform bone))
                AddUnique(result, seen, bone);
        }

        if (result.Count == 0)
        {
            foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
                AddUnique(result, seen, c.transform);
        }

        if (result.Count == 0)
        {
            foreach (Rigidbody rb in root.GetComponentsInChildren<Rigidbody>(true))
                AddUnique(result, seen, rb.transform);
        }

        if (result.Count == 0)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != root)
                    AddUnique(result, seen, t);
            }
        }

        if (result.Count > 0)
            Debug.Log($"CocoExporter auto-resolved keypoints count: {result.Count}");

        return result.ToArray();
    }

    static void AddUnique(List<Transform> list, HashSet<Transform> seen, Transform t)
    {
        if (t == null || seen.Contains(t)) return;
        seen.Add(t);
        list.Add(t);
    }

    [System.Serializable]
    class FrameData
    {
        public float[] keypoints;
    }
}