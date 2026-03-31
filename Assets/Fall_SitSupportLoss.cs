using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Fall_SitSupportLoss : MonoBehaviour, IFallController
{
    [Header("Body")]
    public Transform hips;
    public Transform spine;
    public Transform chest;

    [Header("Ragdoll")]
    public Rigidbody hipsRB;
    public List<Rigidbody> ragdollBodies = new List<Rigidbody>();

    [Header("Pose")]
    public PoseAsset initialPose;

    [Header("Fall Direction")]
    public Vector3 fallDirection = Vector3.back; // 世界空间主方向
    [Range(0f, 1f)]
    public float directionInfluence = 1f; // 方向控制强度
    [Range(0f, 60f)]
    public float maxDirectionOffsetAngle = 15f; // 每次随机偏移角度（度）
    private Vector3 fallDirectionOffset; // 实际使用的偏移方向

    // ===== 参数 =====
    float duration;
    float maxTiltAngle;

    // ===== 速度缓存（用于无突变切换）=====
    Dictionary<Transform, Vector3> lastPos = new Dictionary<Transform, Vector3>();
    Dictionary<Transform, Quaternion> lastRot = new Dictionary<Transform, Quaternion>();

    void Awake()
    {
        CacheRagdoll();
        ResetRagdoll();
        ApplyPoseAsset(initialPose);
    }

    void LateUpdate()
    {
        foreach (var rb in ragdollBodies)
        {
            Transform t = rb.transform;
            lastPos[t] = t.position;
            lastRot[t] = t.rotation;
        }
    }

    void CacheRagdoll()
    {
        ragdollBodies.Clear();
        foreach (var rb in GetComponentsInChildren<Rigidbody>())
        {
            ragdollBodies.Add(rb);
            if (rb != hipsRB)
                rb.isKinematic = true;
        }
    }

    public void Play()
    {
        StopAllCoroutines();
        ResetRagdoll();
        ApplyPoseAsset(initialPose);
        Randomize();
        StartCoroutine(FallRoutine());
    }

    void Randomize()
    {
        duration = Random.Range(0.5f, 0.8f);
        maxTiltAngle = Random.Range(25f, 45f);

        // 随机方向偏移
        float yaw = Random.Range(-maxDirectionOffsetAngle, maxDirectionOffsetAngle);
        float pitch = Random.Range(-maxDirectionOffsetAngle * 0.5f, maxDirectionOffsetAngle * 0.5f);
        Quaternion offsetRot = Quaternion.Euler(pitch, yaw, 0f);
        fallDirectionOffset = offsetRot * fallDirection.normalized;
    }

    IEnumerator FallRoutine()
    {
        float t = 0f;

        Quaternion hipsStart = hips.localRotation;
        Quaternion spineStart = spine.localRotation;
        Quaternion chestStart = chest.localRotation;

        // ===== 方向处理 =====
        Vector3 dir = fallDirectionOffset.normalized;

        // 转换为世界局部方向
        dir = transform.TransformDirection(dir);

        // 混合随机扰动
        Vector3 randomDir = (dir * directionInfluence + Random.insideUnitSphere * (1f - directionInfluence)).normalized;

        // 防止与 up 平行
        if (Mathf.Abs(Vector3.Dot(randomDir, Vector3.up)) > 0.99f)
            randomDir = transform.right;

        Vector3 finalAxis = Vector3.Cross(Vector3.up, randomDir).normalized;

        Quaternion hipsTarget = hipsStart * Quaternion.AngleAxis(maxTiltAngle, finalAxis);
        Quaternion spineTarget = spineStart * Quaternion.AngleAxis(maxTiltAngle * 0.6f, finalAxis);
        Quaternion chestTarget = chestStart * Quaternion.AngleAxis(maxTiltAngle * 0.3f, finalAxis);

        bool ragdollEnabled = false;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = t / duration;

            hips.localRotation = Quaternion.Slerp(hipsStart, hipsTarget, k);
            spine.localRotation = Quaternion.Slerp(spineStart, spineTarget, k);
            chest.localRotation = Quaternion.Slerp(chestStart, chestTarget, k);

            if (!ragdollEnabled && k > 0.7f)
            {
                StartCoroutine(EnableRagdollSmooth());
                ragdollEnabled = true;
            }

            yield return null;
        }

        if (!ragdollEnabled)
            StartCoroutine(EnableRagdollSmooth());
    }

    IEnumerator EnableRagdollSmooth()
    {
        // 首先同步 transforms
        Physics.SyncTransforms();

        // 先确保所有 Rigidbody 处于 kinematic，记录动画目标
        Dictionary<Rigidbody, Vector3> targetPos = new Dictionary<Rigidbody, Vector3>();
        Dictionary<Rigidbody, Quaternion> targetRot = new Dictionary<Rigidbody, Quaternion>();

        foreach (var rb in ragdollBodies)
        {
            Transform t = rb.transform;
            if (!lastPos.ContainsKey(t)) continue;

            targetPos[rb] = t.position;
            targetRot[rb] = t.rotation;

            rb.isKinematic = true;  // 保持 kinematic，先不让物理影响
        }

        // 平滑过渡的帧数
        int steps = 5;
        for (int i = 0; i < steps; i++)
        {
            float alpha = (i + 1f) / steps;
            foreach (var rb in ragdollBodies)
            {
                Vector3 pos = Vector3.Lerp(rb.position, targetPos[rb], alpha);
                Quaternion rot = Quaternion.Slerp(rb.rotation, targetRot[rb], alpha);

                rb.position = pos;
                rb.rotation = rot;

                // 平滑线速度和角速度
                Vector3 vel = (pos - lastPos[rb.transform]) / Time.deltaTime;
                rb.velocity = Vector3.Lerp(rb.velocity, vel, alpha);

                Quaternion delta = rot * Quaternion.Inverse(lastRot[rb.transform]);
                delta.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f) angle -= 360f;
                Vector3 angularVel = axis * angle * Mathf.Deg2Rad / Time.deltaTime;
                rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, angularVel, alpha);

                rb.maxAngularVelocity = 20f;
            }

            yield return new WaitForFixedUpdate(); // 用 FixedUpdate 保证物理平滑
        }

        // 最后完全交给物理
        foreach (var rb in ragdollBodies)
        {
            rb.isKinematic = false;
        }
    }

    void ResetRagdoll()
    {
        foreach (var rb in ragdollBodies)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            if (rb != hipsRB)
                rb.isKinematic = true;
        }
        hipsRB.isKinematic = true;
    }

    void ApplyPoseAsset(PoseAsset pose)
    {
        if (pose == null) return;

        Transform[] bones = hips.GetComponentsInChildren<Transform>();
        for (int i = 0; i < bones.Length && i < pose.bones.Length; i++)
        {
            bones[i].localPosition = pose.bones[i].localPosition;
            bones[i].localRotation = pose.bones[i].localRotation;
        }
    }
}