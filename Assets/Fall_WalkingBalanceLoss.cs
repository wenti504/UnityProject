using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Fall_WalkingBalanceLoss : MonoBehaviour, IFallController
{
    [Header("Body")]
    public Transform hips;
    public Transform spine;

    [Header("Legs")]
    public Transform leftThigh;
    public Transform rightThigh;
    public Transform leftShin;
    public Transform rightShin;
    public Transform leftFoot;
    public Transform rightFoot;

    [Header("Arms")]
    public Transform leftUpperArm;
    public Transform rightUpperArm;
    public Transform leftForeArm;
    public Transform rightForeArm;

    [Header("Pose")]
    public PoseAsset initialPose;

    [Header("Ragdoll")]
    public Rigidbody hipsRB;
    public List<Rigidbody> ragdollBodies = new List<Rigidbody>();

    [Header("走路移动")]
    public float moveSpeed = 0.6f;
    public float hipsHeightOffset = 0.1f;

    [Header("失衡切换物理")]
    public float maxTiltBeforePhysics = 60f;
    public float tiltSpeed = 60f;

    // 行走参数
    float walkSpeed;
    float stepAngle;
    float armSwingAngle;
    float duration;

    // 跌倒参数
    float fallYaw;
    float failTime;

    Vector3 initialHipsPos;

    class Snapshot
    {
        public Transform t;
        public Vector3 pos;
        public UnityEngine.Quaternion rot;
    }

    List<Snapshot> origin = new List<Snapshot>();

    Dictionary<Transform, Vector3> lastPos = new Dictionary<Transform, Vector3>();
    Dictionary<Transform, UnityEngine.Quaternion> lastRot = new Dictionary<Transform, UnityEngine.Quaternion>();
    bool ragdollEnabled;

    void Awake()
    {
        ragdollBodies.Clear();
        foreach (var rb in GetComponentsInChildren<Rigidbody>())
        {
            ragdollBodies.Add(rb);
            if (rb != hipsRB) rb.isKinematic = true;
        }

        ApplyPoseAsset(initialPose);
        CachePose();
        initialHipsPos = hips.localPosition;
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
        walkSpeed = Random.Range(0.8f, 1.2f);
        stepAngle = Random.Range(20f, 30f);
        armSwingAngle = Random.Range(25f, 40f);
        duration = 2f;
        fallYaw = Random.Range(-25f, 25f);
        failTime = Random.Range(0.4f, 0.6f);
    }

    IEnumerator FallRoutine()
    {
        ragdollEnabled = false;
        float t = 0f;
        float phase = 0f;
        float currentTilt = 0f;

        Vector3 moveDir = Quaternion.Euler(0, fallYaw, 0) * transform.forward;
        moveDir = Vector3.Scale(moveDir, new Vector3(1, 0, 1)).normalized;

        Quaternion initSpineRot = GetRot(spine);
        Quaternion initHipsRot = GetRot(hips);

        Vector3 hipsStartPos = hips.position;

        while (t < duration)
        {
            t += Time.deltaTime;
            phase += Time.deltaTime * walkSpeed;
            phase %= 1f; // 限制 phase 在 0~1 避免腿反向

            float sin = Mathf.Sin(phase * Mathf.PI * 2f); // 完整周期摆腿

            ApplyWalkPose(sin);

            // 平滑移动
            Vector3 targetHipsPos = hipsStartPos + moveDir * moveSpeed * t;
            hips.position = Vector3.Lerp(hips.position, targetHipsPos, 0.5f);

            // 平滑 Y 调整
            float yOffset = Mathf.Lerp(hips.localPosition.y, initialHipsPos.y - Mathf.Abs(sin) * hipsHeightOffset, 0.5f);
            hips.localPosition = new Vector3(hips.localPosition.x, yOffset, hips.localPosition.z);

            hips.localRotation = Quaternion.Slerp(hips.localRotation, initHipsRot * Quaternion.Euler(2f * sin, fallYaw * 0.1f, 0), 0.5f);

            if (t > failTime && currentTilt < maxTiltBeforePhysics)
            {
                currentTilt += tiltSpeed * Time.deltaTime;
                spine.localRotation = initSpineRot * Quaternion.Euler(currentTilt, 0, 0);
            }

            if (currentTilt >= maxTiltBeforePhysics)
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

        int steps = 5;
        for (int i = 0; i < steps; i++)
        {
            float alpha = (i + 1f) / steps;
            foreach (var rb in ragdollBodies)
            {
                if (!lastPos.ContainsKey(rb.transform)) continue;

                rb.isKinematic = true; // 先保持 kinematic
                rb.position = Vector3.Lerp(rb.position, rb.transform.position, alpha);
                rb.rotation = Quaternion.Slerp(rb.rotation, rb.transform.rotation, alpha);
                rb.velocity = Vector3.Lerp(rb.velocity, (rb.transform.position - lastPos[rb.transform]) / Time.fixedDeltaTime, alpha);

                Quaternion delta = rb.transform.rotation * Quaternion.Inverse(lastRot[rb.transform]);
                delta.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f) angle -= 360f;
                Vector3 angVel = axis * angle * Mathf.Deg2Rad / Time.fixedDeltaTime;
                rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, angVel, alpha);
            }
            yield return new WaitForFixedUpdate();
        }

        foreach (var rb in ragdollBodies)
            rb.isKinematic = false;
    }

    void ApplyWalkPose(float sin)
    {
        float leftLeg = stepAngle * sin;
        float rightLeg = -stepAngle * sin;
        float leftArm = -armSwingAngle * sin;
        float rightArm = armSwingAngle * sin;

        leftThigh.localRotation = GetRot(leftThigh) * UnityEngine.Quaternion.Euler(leftLeg, 0, 0);
        rightThigh.localRotation = GetRot(rightThigh) * UnityEngine.Quaternion.Euler(rightLeg, 0, 0);
        leftShin.localRotation = GetRot(leftShin) * UnityEngine.Quaternion.Euler(-leftLeg * 0.5f, 0, 0);
        rightShin.localRotation = GetRot(rightShin) * UnityEngine.Quaternion.Euler(-rightLeg * 0.5f, 0, 0);
        leftFoot.localRotation = GetRot(leftFoot) * UnityEngine.Quaternion.Euler(leftLeg * 0.3f, 0, 0);
        rightFoot.localRotation = GetRot(rightFoot) * UnityEngine.Quaternion.Euler(rightLeg * 0.3f, 0, 0);

        leftUpperArm.localRotation = GetRot(leftUpperArm) * UnityEngine.Quaternion.Euler(leftArm, 0, 0);
        rightUpperArm.localRotation = GetRot(rightUpperArm) * UnityEngine.Quaternion.Euler(rightArm, 0, 0);
        leftForeArm.localRotation = GetRot(leftForeArm) * UnityEngine.Quaternion.Euler(-leftArm * 0.3f, 0, 0);
        rightForeArm.localRotation = GetRot(rightForeArm) * UnityEngine.Quaternion.Euler(-rightArm * 0.3f, 0, 0);
    }

    
    void CachePose()
    {
        origin.Clear();
        foreach (var t in hips.GetComponentsInChildren<Transform>())
        {
            origin.Add(new Snapshot { t = t, pos = t.localPosition, rot = t.localRotation });
        }
    }

    UnityEngine.Quaternion GetRot(Transform t)
    {
        foreach (var s in origin)
            if (s.t == t) return s.rot;
        return t.localRotation;
    }

    void ResetPose()
    {
        foreach (var s in origin)
        {
            s.t.localPosition = s.pos;
            s.t.localRotation = s.rot;
        }
        foreach (var rb in ragdollBodies)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            if (rb != hipsRB)
                rb.isKinematic = true;
        }
        hips.localPosition = initialHipsPos;
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