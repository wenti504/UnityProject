using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Fall_StandBalanceLoss : MonoBehaviour, IFallController
{
    [Header("Body")]
    public Transform hips;
    public Transform spine;

    [Header("Legs")]
    public Transform leftThigh;
    public Transform rightThigh;

    [Header("Pose")]
    public PoseAsset initialPose;

    [Header("Ragdoll")]
    public Rigidbody hipsRB;
    List<Rigidbody> ragdollBodies = new List<Rigidbody>();

    [Header("失衡控制")]
    public float criticalAngle = 40f;
    public float slowTiltSpeed = 11f;
    public float fastFallSpeed = 70f;

    float duration;
    float initialTilt;
    int balanceType;

    bool ragdollEnabled;
    Dictionary<Transform, Vector3> lastPos = new Dictionary<Transform, Vector3>();
    Dictionary<Transform, Quaternion> lastRot = new Dictionary<Transform, Quaternion>();

    Quaternion hipStart, spineStart, leftThighStart, rightThighStart;
    Vector3 recordedFallDir; // 记录最终跌倒方向，用于物理启用时施加冲量

    void Awake()
    {
        ragdollBodies.Clear();
        foreach (var rb in GetComponentsInChildren<Rigidbody>())
        {
            ragdollBodies.Add(rb);
            if (rb != hipsRB) rb.isKinematic = true;
        }
        ApplyPoseAsset(initialPose);
    }

    void FixedUpdate()
    {
        foreach (var rb in ragdollBodies)
        {
            lastPos[rb.transform] = rb.transform.position;
            lastRot[rb.transform] = rb.transform.rotation;
        }
    }

    public void Play()
    {
        StopAllCoroutines();
        ResetPose();
        Randomize();
        StartCoroutine(FallRoutine());
    }

    void Randomize()
    {
        duration = Random.Range(0.6f, 1.0f);
        initialTilt = Random.Range(-3f, 3f);      // 起始倾斜角度（正：前倾，负：后仰，用于前/后类型）
        balanceType = Random.Range(0, 5);         // 0:前倾 1:后仰 2:左侧倾 3:右侧倾 4:前倾+随机侧偏
    }

    IEnumerator FallRoutine()
    {
        ragdollEnabled = false;
        float timer = 0;

        // ===== 根据 balanceType 确定倾斜轴和方向 =====
        Vector3 axis;
        float sign = 1f;
        switch (balanceType)
        {
            case 0: // 前倾
                axis = Vector3.right;
                sign = 1f;
                break;
            case 1: // 后仰
                axis = Vector3.right;
                sign = -1f;
                break;
            case 2: // 左侧倾
                axis = Vector3.forward;
                sign = 1f;
                break;
            case 3: // 右侧倾
                axis = Vector3.forward;
                sign = -1f;
                break;
            case 4: // 前倾 + 随机侧偏（绕斜轴）
                float randomSide = Random.Range(-0.5f, 0.5f);
                axis = new Vector3(1f, 0f, randomSide).normalized;
                sign = 1f;
                break;
            default:
                axis = Vector3.right;
                sign = 1f;
                break;
        }

        // 当前倾斜角度（标量，正负表示方向）
        float currentTiltAngle = initialTilt;
        float targetMax = 60f; // 最大倾斜角度绝对值

        hipStart = hips.localRotation;
        spineStart = spine.localRotation;
        leftThighStart = leftThigh.localRotation;
        rightThighStart = rightThigh.localRotation;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float delta = Time.deltaTime;

            // 髋部保持初始旋转（不倒）
            hips.localRotation = hipStart;

            // 根据当前倾斜绝对值决定倾斜速度
            float absAngle = Mathf.Abs(currentTiltAngle);
            float tiltSpeed = absAngle < criticalAngle ? slowTiltSpeed : fastFallSpeed;
            float deltaAngle = tiltSpeed * delta;
            currentTiltAngle += sign * deltaAngle;

            // 限制最大角度
            if (sign > 0 && currentTiltAngle > targetMax)
                currentTiltAngle = targetMax;
            else if (sign < 0 && currentTiltAngle < -targetMax)
                currentTiltAngle = -targetMax;

            // 应用脊柱旋转
            spine.localRotation = spineStart * Quaternion.AngleAxis(currentTiltAngle, axis);

            // 腿的弯曲：基于前向倾斜分量（如果是纯侧倾，腿弯曲较小）
            float forwardTilt = Vector3.Dot(axis, Vector3.right) * currentTiltAngle;
            float legBend = Mathf.InverseLerp(0, 60, Mathf.Abs(forwardTilt)) * 12f;
            leftThigh.localRotation = leftThighStart * Quaternion.Euler(legBend, 0, 0);
            rightThigh.localRotation = rightThighStart * Quaternion.Euler(legBend, 0, 0);

            // 达到最大角度则提前结束
            if (Mathf.Abs(currentTiltAngle) >= targetMax)
                break;

            yield return null;
        }

        // 记录跌倒方向（用于物理冲量）
        if (Mathf.Abs(Vector3.Dot(axis, Vector3.right)) > 0.5f)
            recordedFallDir = Vector3.Scale(spine.forward, new Vector3(1, 0, 1)).normalized; // 前/后
        else
            recordedFallDir = Vector3.Scale(spine.right, new Vector3(1, 0, 1)).normalized;   // 左/右

        Physics.SyncTransforms();
        yield return StartCoroutine(EnableRagdollSmooth());
    }

    IEnumerator EnableRagdollSmooth()
    {
        if (ragdollEnabled) yield break;
        ragdollEnabled = true;

        Physics.SyncTransforms();

        Dictionary<Rigidbody, Vector3> targetPos = new Dictionary<Rigidbody, Vector3>();
        Dictionary<Rigidbody, Quaternion> targetRot = new Dictionary<Rigidbody, Quaternion>();

        foreach (var rb in ragdollBodies)
        {
            if (!lastPos.ContainsKey(rb.transform)) continue;

            targetPos[rb] = rb.transform.position;
            targetRot[rb] = rb.transform.rotation;
            rb.isKinematic = true;
        }

        int steps = 5;
        for (int i = 0; i < steps; i++)
        {
            float alpha = (i + 1f) / steps;
            foreach (var rb in ragdollBodies)
            {
                if (!targetPos.ContainsKey(rb)) continue;

                Vector3 pos = Vector3.Lerp(rb.position, targetPos[rb], alpha);
                Quaternion rot = Quaternion.Slerp(rb.rotation, targetRot[rb], alpha);

                rb.position = pos;
                rb.rotation = rot;

                Vector3 vel = (pos - lastPos[rb.transform]) / Time.fixedDeltaTime;
                rb.velocity = Vector3.Lerp(rb.velocity, vel, alpha);

                Quaternion delta = rot * Quaternion.Inverse(lastRot[rb.transform]);
                delta.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f) angle -= 360f;
                Vector3 angularVel = axis * angle * Mathf.Deg2Rad / Time.fixedDeltaTime;
                rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, angularVel, alpha);

                rb.maxAngularVelocity = 20f;
            }
            yield return new WaitForFixedUpdate();
        }

        foreach (var rb in ragdollBodies)
            rb.isKinematic = false;

        // 根据记录的方向施加初始倒下速度
        hipsRB.velocity += recordedFallDir * Random.Range(0.7f, 1.2f);
    }

    void ResetPose()
    {
        ApplyPoseAsset(initialPose);
        foreach (var rb in ragdollBodies)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            if (rb != hipsRB) rb.isKinematic = true;
        }
        ragdollEnabled = false;
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