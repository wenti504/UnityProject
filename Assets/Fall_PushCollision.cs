using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Fall_PushCollision : MonoBehaviour, IFallController
{
    [Header("Bones")]
    public Transform hips;
    public Transform spine;
    public Transform chest;

    [Header("Pose")]
    public PoseAsset initialPose;

    [Header("Rigidbody")]
    public Rigidbody hipsRB;
    public List<Rigidbody> ragdollBodies = new List<Rigidbody>();

    [Header("Push")]
    public float pushForce = 200f;
    public float pushAngle = 0f;

    [Header("Timing")]
    public float waitBeforePush = 0.4f; // 🔥 静止等待时间
    public float duration = 0.4f;

    bool ragdollEnabled;

    Dictionary<Transform, Vector3> lastPos = new Dictionary<Transform, Vector3>();
    Dictionary<Transform, Quaternion> lastRot = new Dictionary<Transform, Quaternion>();

    void Awake()
    {
        ragdollBodies.Clear();

        foreach (var rb in GetComponentsInChildren<Rigidbody>())
        {
            ragdollBodies.Add(rb);
            if (rb != hipsRB)
                rb.isKinematic = true;
        }

        ApplyPoseAsset(initialPose);
    }

    void FixedUpdate()
    {
        foreach (var rb in ragdollBodies)
        {
            Transform t = rb.transform;
            lastPos[t] = t.position;
            lastRot[t] = t.rotation;
        }
    }

    public void Play()
    {
        StopAllCoroutines();
        ResetRagdoll();
        ApplyPoseAsset(initialPose);
        Physics.SyncTransforms();
        StartCoroutine(FallRoutine());
    }

    IEnumerator FallRoutine()
    {
        // ==============================================
        // 🔥 核心：先完全静止等待 0.4 秒（无任何动作）
        // ==============================================
        yield return new WaitForSeconds(waitBeforePush);

        // 等待结束 → 开始推倒逻辑
        Vector3 flatForward = Vector3.Scale(transform.forward, new Vector3(1, 0, 1)).normalized;
        var (dir, pushForce) = GenerateRandomTarget();

        Vector3 axis = Vector3.Cross(Vector3.up, dir).normalized;
        float timer = 0f;

        Quaternion hipsStart = hips.localRotation;
        Quaternion spineStart = spine.localRotation;
        Quaternion chestStart = chest.localRotation;

        float tiltAngle = Mathf.Lerp(15f, 50f, pushForce / 300f);

        Quaternion hipsTarget = hipsStart * Quaternion.AngleAxis(tiltAngle, axis);
        Quaternion spineTarget = spineStart * Quaternion.AngleAxis(tiltAngle * 0.6f, axis);
        Quaternion chestTarget = chestStart * Quaternion.AngleAxis(tiltAngle * 0.3f, axis);

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float k = timer / duration;

            hips.localRotation = Quaternion.Slerp(hipsStart, hipsTarget, k);
            spine.localRotation = Quaternion.Slerp(spineStart, spineTarget, k);
            chest.localRotation = Quaternion.Slerp(chestStart, chestTarget, k);

            yield return null;
        }

        Physics.SyncTransforms();
        StartCoroutine(EnableRagdollSmooth(dir));
    }

    IEnumerator EnableRagdollSmooth(Vector3 dir)
    {
        if (ragdollEnabled) yield break;
        ragdollEnabled = true;

        Physics.SyncTransforms();

        Dictionary<Rigidbody, Vector3> targetPos = new();
        Dictionary<Rigidbody, Quaternion> targetRot = new();

        foreach (var rb in ragdollBodies)
        {
            if (rb == null) continue;

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
                if (rb == null) continue;

                Vector3 pos = Vector3.Lerp(rb.position, targetPos[rb], alpha);
                Quaternion rot = Quaternion.Slerp(rb.rotation, targetRot[rb], alpha);

                rb.position = pos;
                rb.rotation = rot;

                // ✅ 正确 velocity（关键）
                Vector3 vel = (targetPos[rb] - rb.position) / (steps * Time.fixedDeltaTime);
                vel = Vector3.ClampMagnitude(vel, 8f);

                rb.velocity = Vector3.Lerp(rb.velocity, vel, alpha);

                // ✅ 稳定角速度
                Quaternion delta = rot * Quaternion.Inverse(rb.rotation);
                delta.ToAngleAxis(out float angle, out Vector3 axis);

                Vector3 angularVel = Vector3.zero;

                if (axis != Vector3.zero && !float.IsNaN(axis.x))
                {
                    if (angle > 180f) angle -= 360f;
                    angularVel = axis * angle * Mathf.Deg2Rad / Time.fixedDeltaTime;
                }

                angularVel = Vector3.ClampMagnitude(angularVel, 15f);
                rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, angularVel, alpha);
            }

            yield return new WaitForFixedUpdate();
        }

        foreach (var rb in ragdollBodies)
            if (rb != null)
                rb.isKinematic = false;

        yield return new WaitForFixedUpdate();

        // ✅ 只保留一种驱动：Impulse（推荐）
        Vector3 force = dir * Random.Range(40f, 80f);

        hipsRB.AddForce(force, ForceMode.Impulse);

        // ❌ 删除这些（很重要）
        // hipsRB.velocity += pushVelocity;
        // AddTorque(...)
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
        ragdollEnabled = false;
    }

    public (Vector3 pushDir, float force) GenerateRandomTarget()
    {
        float minForce = 150f;
        float maxForce = 350f;
        float minAngle = -180f;
        float maxAngle = 180f;

        float randomForce = Random.Range(minForce, maxForce);
        float randomAngle = Random.Range(minAngle, maxAngle);

        Vector3 flatForward = Vector3.Scale(transform.forward, new Vector3(1, 0, 1)).normalized;
        Vector3 randomDir = Quaternion.Euler(0, randomAngle, 0) * flatForward;

        return (randomDir, randomForce);
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