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

    float tiltAngle;
    float duration;
    float initialTilt;
    int balanceType;

    bool ragdollEnabled;
    Dictionary<Transform, Vector3> lastPos = new Dictionary<Transform, Vector3>();
    Dictionary<Transform, Quaternion> lastRot = new Dictionary<Transform, Quaternion>();

    Quaternion hipStart, spineStart, leftThighStart, rightThighStart;

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
        initialTilt = Random.Range(-3f, 3f);
        balanceType = Random.Range(0, 5);
    }

    IEnumerator FallRoutine()
    {
        ragdollEnabled = false;
        float timer = 0;
        float currentSpineAngle = 0f;

        hipStart = hips.localRotation;
        spineStart = spine.localRotation;
        leftThighStart = leftThigh.localRotation;
        rightThighStart = rightThigh.localRotation;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float delta = Time.deltaTime;

            // 髋部完全不动
            hips.localRotation = hipStart;

            // ==============================================
            // 先慢倾 → 临界点 → 快速倒下
            // ==============================================
            if (currentSpineAngle < criticalAngle)
                currentSpineAngle += slowTiltSpeed * delta;
            else
                currentSpineAngle += fastFallSpeed * delta;

            spine.localRotation = spineStart * Quaternion.Euler(currentSpineAngle, 0, 0);

            // 腿轻微弯曲
            float legBend = Mathf.InverseLerp(0, 60, currentSpineAngle) * 12f;
            leftThigh.localRotation = leftThighStart * Quaternion.Euler(legBend, 0, 0);
            rightThigh.localRotation = rightThighStart * Quaternion.Euler(legBend, 0, 0);

            if (currentSpineAngle >= 60f)
                break;

            yield return null;
        }

        Physics.SyncTransforms();
        yield return StartCoroutine(EnableRagdollSmooth());
    }

    IEnumerator EnableRagdollSmooth()
    {
        if (ragdollEnabled) yield break;
        ragdollEnabled = true;

        Physics.SyncTransforms();

        // 缓存目标 transform
        Dictionary<Rigidbody, Vector3> targetPos = new Dictionary<Rigidbody, Vector3>();
        Dictionary<Rigidbody, Quaternion> targetRot = new Dictionary<Rigidbody, Quaternion>();

        foreach (var rb in ragdollBodies)
        {
            if (!lastPos.ContainsKey(rb.transform)) continue;

            targetPos[rb] = rb.transform.position;
            targetRot[rb] = rb.transform.rotation;

            rb.isKinematic = true; // 保持 kinematic，先过渡
        }

        int steps = 5; // 分帧平滑
        for (int i = 0; i < steps; i++)
        {
            float alpha = (i + 1f) / steps;

            foreach (var rb in ragdollBodies)
            {
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

        // 解除 kinematic
        foreach (var rb in ragdollBodies)
            rb.isKinematic = false;

        // 施加初始倒下速度
        Vector3 fallDir = Vector3.Scale(spine.forward, new Vector3(1, 0, 1)).normalized;
        hipsRB.velocity += fallDir * Random.Range(0.7f, 1.2f);
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