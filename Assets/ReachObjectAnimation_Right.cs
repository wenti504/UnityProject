using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ReachObjectAnimation_Right : MonoBehaviour, IFallController
{
    [Header("Bones")]
    public Transform hips;
    public Transform spine;
    public Transform chest;
    public Transform upperArm;

    [Header("Initial Pose")]
    public PoseAsset initialPose;

    [Header("Timing")]
    public float baseDuration = 1.2f;

    float duration;
    float timer;
    bool playing;
    bool physicsEnabled;
    bool clipX;
    Rigidbody[] bodies;

    class Snapshot
    {
        public Transform t;
        public Vector3 pos;
        public Quaternion rot;
    }

    List<Snapshot> origin = new List<Snapshot>();

    // 用于平滑过渡的位置/旋转缓存
    Dictionary<Transform, Vector3> lastPos = new Dictionary<Transform, Vector3>();
    Dictionary<Transform, Quaternion> lastRot = new Dictionary<Transform, Quaternion>();

    Vector3 hipsPosOffset;
    Quaternion hipsTargetRot;
    Quaternion spineTargetRot;
    Quaternion chestTargetRot;

    Quaternion armStartRot;
    Quaternion armTargetRot;

    float randomLean;
    float randomShift;
    float randomSpeed;

    void Start()
    {
        bodies = hips.GetComponentsInChildren<Rigidbody>();

        if (initialPose != null)
            ApplyPoseAsset(initialPose);

        CachePose();
        SetPhysics(false);
    }

    void FixedUpdate()
    {
        // 记录每帧物理状态，用于平滑过渡
        foreach (var rb in bodies)
        {
            lastPos[rb.transform] = rb.transform.position;
            lastRot[rb.transform] = rb.transform.rotation;
        }
    }

    void Update()
    {
        if (!playing) return;

        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / duration);
        float s = Mathf.SmoothStep(0f, 1f, t);

        ApplyPose(s);

        // ✅ 动画完成后启用5帧平滑物理过渡
        if (t >= 1f && !physicsEnabled)
        {
            physicsEnabled = true;
            playing = false;
            StartCoroutine(EnableRagdollSmooth());
        }
    }

    public void Play()
    {
        ResetPose();

        if (initialPose != null)
            ApplyPoseAsset(initialPose);

        GenerateRandom();
        CalculateTargetPose();

        timer = 0f;
        playing = true;
        physicsEnabled = false;
    }

    // 内部生成随机目标点
    Vector3 GenerateRandomTarget()
    {
        Vector3 hipsPos = hips.position;

        float angle = Random.Range(-90f, 90f);
        float distance = Random.Range(2f, 4f);
        float height = Random.Range(0.5f, 1.5f);
        if (angle < 0f) clipX = false;
        else clipX = true;
        Vector3 dir = Quaternion.Euler(0f, angle, 0f) * hips.forward;

        Vector3 noise = new Vector3(
            Random.Range(-0.03f, 0.03f),
            Random.Range(-0.03f, 0.03f),
            Random.Range(-0.03f, 0.03f)
        );

        return hipsPos + dir * distance + Vector3.up * height + noise;
    }

    void GenerateRandom()
    {
        randomLean = Random.Range(0.9f, 1.4f);
        randomShift = Random.Range(0.8f, 1.2f);
        randomSpeed = Random.Range(0.8f, 1.3f);

        duration = baseDuration / randomSpeed;
    }

    void CalculateTargetPose()
    {
        Vector3 targetWorld = GenerateRandomTarget();

        Vector3 toTarget = targetWorld - hips.position;
        Vector3 horizontal = Vector3.ProjectOnPlane(toTarget, Vector3.up);

        if (horizontal.magnitude < 0.001f) return;

        Vector3 reachDir = horizontal.normalized;

        float totalYaw = Vector3.SignedAngle(hips.forward, reachDir, Vector3.up);

        float hipsYaw = totalYaw * 0.2f;
        float spineYaw = totalYaw * 0.3f;
        float chestYaw = totalYaw * 0.5f;

        float reachRatio = Mathf.Clamp01(horizontal.magnitude / 0.6f);
        float lean = 50f * reachRatio * randomLean;
        float shift = 0.25f * reachRatio * randomShift;

        Vector3 worldOffset = reachDir * shift + Vector3.down * shift * 0.25f;

        hipsPosOffset = hips.parent != null
            ? hips.parent.InverseTransformDirection(worldOffset)
            : worldOffset;

        hipsTargetRot = GetSnapshot(hips).rot * Quaternion.Euler(0f, hipsYaw, 0f);
        spineTargetRot = GetSnapshot(spine).rot * Quaternion.Euler(lean * 0.6f, spineYaw, -totalYaw * 0.1f);
        chestTargetRot = GetSnapshot(chest).rot * Quaternion.Euler(lean * 0.4f, chestYaw, -totalYaw * 0.15f);

        // 手臂计算（右手镜像修正）
        Snapshot snap = GetSnapshot(upperArm);
        if (snap == null) return;

        armStartRot = snap.rot;

        Vector3 armToTargetWorld = (targetWorld - upperArm.position).normalized;
        Vector3 armHoriz = Vector3.ProjectOnPlane(armToTargetWorld, Vector3.up);

        if (armHoriz.sqrMagnitude < 0.0001f)
            armHoriz = reachDir;

        armHoriz.Normalize();

        float maxYawDeltaRad = 80f * Mathf.Deg2Rad;
        Vector3 clampedHoriz = Vector3.RotateTowards(reachDir, armHoriz, maxYawDeltaRad, 0f);

        Vector3 finalDirWorld = new Vector3(clampedHoriz.x, armToTargetWorld.y, clampedHoriz.z).normalized;

        // 右手镜像修正
        Vector3 localDir = upperArm.parent.InverseTransformDirection(finalDirWorld);
        if (clipX) localDir.x = -localDir.x;

        armTargetRot = Quaternion.LookRotation(localDir, upperArm.parent.up);
    }

    void ApplyPose(float t)
    {
        Snapshot hipsSnap = GetSnapshot(hips);

        hips.localPosition = Vector3.Lerp(hipsSnap.pos, hipsSnap.pos + hipsPosOffset, t);
        hips.localRotation = Quaternion.Slerp(hipsSnap.rot, hipsTargetRot, t);

        spine.localRotation = Quaternion.Slerp(GetSnapshot(spine).rot, spineTargetRot, t);
        chest.localRotation = Quaternion.Slerp(GetSnapshot(chest).rot, chestTargetRot, t);

        upperArm.localRotation = Quaternion.Slerp(armStartRot, armTargetRot, t);
    }

    // ✅ 新增：5帧平滑物理过渡
    IEnumerator EnableRagdollSmooth()
    {
        Physics.SyncTransforms();

        Dictionary<Rigidbody, Vector3> targetPos = new Dictionary<Rigidbody, Vector3>();
        Dictionary<Rigidbody, Quaternion> targetRot = new Dictionary<Rigidbody, Quaternion>();

        foreach (var rb in bodies)
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

            foreach (var rb in bodies)
            {
                if (rb == null) continue;

                Vector3 pos = Vector3.Lerp(rb.position, targetPos[rb], alpha);
                Quaternion rot = Quaternion.Slerp(rb.rotation, targetRot[rb], alpha);

                rb.position = pos;
                rb.rotation = rot;

                // 平滑线速度
                Vector3 vel = (targetPos[rb] - rb.position) / (steps * Time.fixedDeltaTime);
                vel = Vector3.ClampMagnitude(vel, 10f);
                rb.velocity = Vector3.Lerp(rb.velocity, vel, alpha);

                // 平滑角速度
                Quaternion delta = rot * Quaternion.Inverse(rb.rotation);
                delta.ToAngleAxis(out float angle, out Vector3 axis);

                Vector3 angularVel = Vector3.zero;
                if (axis != Vector3.zero && !float.IsNaN(axis.x))
                {
                    if (angle > 180f) angle -= 360f;
                    angularVel = axis * angle * Mathf.Deg2Rad / Time.fixedDeltaTime;
                }
                angularVel = Vector3.ClampMagnitude(angularVel, 20f);
                rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, angularVel, alpha);

                rb.maxAngularVelocity = 20f;
            }

            yield return new WaitForFixedUpdate();
        }

        // 解除运动学约束，完全交给物理
        foreach (var rb in bodies)
        {
            if (rb == null) continue;
            rb.isKinematic = false;
        }
    }

    void CachePose()
    {
        origin.Clear();
        foreach (var t in hips.GetComponentsInChildren<Transform>())
        {
            origin.Add(new Snapshot { t = t, pos = t.localPosition, rot = t.localRotation });
        }
    }

    void ApplyPoseAsset(PoseAsset pose)
    {
        if (pose == null || pose.bones == null) return;

        Transform[] bones = hips.GetComponentsInChildren<Transform>();
        for (int i = 0; i < bones.Length && i < pose.bones.Length; i++)
        {
            bones[i].localPosition = pose.bones[i].localPosition;
            bones[i].localRotation = pose.bones[i].localRotation;
        }
    }

    Snapshot GetSnapshot(Transform t)
    {
        return origin.Find(s => s.t == t);
    }

    void ResetPose()
    {
        SetPhysics(false);
        foreach (var s in origin)
        {
            s.t.localPosition = s.pos;
            s.t.localRotation = s.rot;
        }
    }

    void SetPhysics(bool enabled)
    {
        foreach (var rb in bodies)
        {
            rb.isKinematic = !enabled;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}