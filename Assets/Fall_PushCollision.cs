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

        foreach (var rb in ragdollBodies)
        {
            Transform t = rb.transform;
            if (!lastPos.ContainsKey(t)) continue;

            rb.position = t.position;
            rb.rotation = t.rotation;

            Vector3 vel = (t.position - lastPos[t]) / Time.fixedDeltaTime;
            rb.velocity = vel;

            Quaternion delta = t.rotation * Quaternion.Inverse(lastRot[t]);
            delta.ToAngleAxis(out float angle, out Vector3 axis);

            if (angle > 180f) angle -= 360f;
            rb.angularVelocity = axis * angle * Mathf.Deg2Rad / Time.fixedDeltaTime;
        }

        yield return new WaitForFixedUpdate();

        foreach (var rb in ragdollBodies)
            rb.isKinematic = false;

        yield return new WaitForFixedUpdate();

        Vector3 pushVelocity = dir * (pushForce / hipsRB.mass);
        hipsRB.velocity += pushVelocity;

        hipsRB.AddTorque(Vector3.Cross(Vector3.up, dir) * pushForce * 0.2f, ForceMode.Impulse);
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