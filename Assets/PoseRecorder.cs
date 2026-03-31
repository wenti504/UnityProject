using System.Collections.Generic;
using UnityEngine;

public class PoseRecorder : MonoBehaviour
{
    [Header("Pose Assets")]
    public PoseAsset sitPose;
    public PoseAsset standPose;

    Transform[] bones;                         // 当前模型所有骨骼
    Dictionary<string, Transform> boneMap;     // 骨骼名称 → Transform 映射

    void Awake()
    {
        bones = GetComponentsInChildren<Transform>();

        // ⭐ 构建骨骼名称映射
        boneMap = new Dictionary<string, Transform>();
        foreach (var t in bones)
        {
            if (!boneMap.ContainsKey(t.name))
                boneMap.Add(t.name, t);
        }
    }

    void Update()
    {
        // 保存坐姿
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SavePose(sitPose);
            Debug.Log("Sit Pose Saved");
        }

        // 保存站姿
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SavePose(standPose);
            Debug.Log("Stand Pose Saved");
        }

        // 应用坐姿
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            ApplyPose(sitPose);
            Debug.Log("Sit Pose Applied");
        }

        // 应用站姿
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            ApplyPose(standPose);
            Debug.Log("Stand Pose Applied");
        }
    }

    // =========================
    // 保存姿态
    // =========================
    void SavePose(PoseAsset pose)
    {
        if (pose == null) return;

        pose.bones = new BonePose[bones.Length];

        for (int i = 0; i < bones.Length; i++)
        {
            pose.bones[i] = new BonePose
            {
                boneName = bones[i].name,
                localPosition = bones[i].localPosition,
                localRotation = bones[i].localRotation
            };
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(pose);
#endif
    }

    // =========================
    // 应用姿态
    // =========================
    public void ApplyPose(PoseAsset pose)
    {
        if (pose == null || pose.bones == null) return;

        foreach (var bonePose in pose.bones)
        {
            if (!boneMap.TryGetValue(bonePose.boneName, out Transform t))
                continue;

            t.localPosition = bonePose.localPosition;
            t.localRotation = bonePose.localRotation;
        }
    }
}