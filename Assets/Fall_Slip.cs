using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Fall_Slip : MonoBehaviour, IFallController
{
    [Header("Body")]
    public Transform hips;
    public Transform spine;
    public Transform chest;

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

    List<Rigidbody> ragdollBodies = new List<Rigidbody>();

    [Header("走路滑倒设置")]
    public float walkMoveSpeed = 0.5f;
    public float walkStepAngle = 25f;
    public float walkArmSwing = 30f;
    public float walkDuration = 0.8f;

    // 随机参数
    float forwardAngle;
    float sideAngle;
    float legAngle;
    float armAngle;
    float duration;

    float spineFactor;
    float chestFactor;

    float slipSpeed;
    Vector3 slipDir;

    float initialTilt;
    bool slipLeftLeg; // 只保留：左脚滑 / 右脚滑

    bool ragdollEnabled;

    // 防突变缓存
    Dictionary<Transform, Vector3> lastPos = new Dictionary<Transform, Vector3>();
    Dictionary<Transform, Quaternion> lastRot = new Dictionary<Transform, Quaternion>();

    // 初始旋转
    Quaternion leftThighStart;
    Quaternion rightThighStart;
    Quaternion leftArmStart;
    Quaternion rightArmStart;
    Quaternion hipStart, spineStart, chestStart;
    Vector3 initialHipsLocalPos;

    void Awake()
    {
        ragdollBodies.Clear();
        Rigidbody[] bodies = GetComponentsInChildren<Rigidbody>();
        foreach (var rb in bodies)
        {
            ragdollBodies.Add(rb);
            if (rb != hipsRB)
                rb.isKinematic = true;
        }

        ApplyPoseAsset(initialPose);
        initialHipsLocalPos = hips.localPosition;
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
        StartCoroutine(WalkThenSlip());
    }

    void Randomize()
    {
        forwardAngle = Random.Range(25f, 50f);
        sideAngle = Random.Range(-25f, 25f);
        legAngle = Random.Range(40f, 70f);
        armAngle = Random.Range(50f, 80f);
        duration = Random.Range(0.25f, 0.45f);
        spineFactor = Random.Range(0.3f, 0.6f);
        chestFactor = Random.Range(0.15f, 0.4f);
        slipSpeed = Random.Range(0.15f, 0.35f);

        Vector3 flatFwd = Vector3.Scale(transform.forward, new Vector3(1, 0, 1)).normalized;
        slipDir = Quaternion.Euler(0, Random.Range(-30f, 30f), 0) * flatFwd;

        initialTilt = Random.Range(-3f, 3f);
        slipLeftLeg = Random.value > 0.5f; // 左脚 / 右脚 滑倒
    }

    IEnumerator WalkThenSlip()
    {
        ragdollEnabled = false;
        float t = 0;
        float phase = 0;

        CacheStartRotations();

        // 走路阶段
        while (t < walkDuration)
        {
            t += Time.deltaTime;
            phase += Time.deltaTime * 1.8f;
            float sin = Mathf.Sin(phase * Mathf.PI);

            ApplyWalkingPose(sin);

            hips.position += transform.forward * walkMoveSpeed * Time.deltaTime;
            hips.localPosition = new Vector3(
                hips.localPosition.x,
                initialHipsLocalPos.y - Mathf.Abs(sin) * 0.1f,
                hips.localPosition.z
            );

            yield return null;
        }

        // 滑倒阶段
        yield return StartCoroutine(SlipRoutine());
    }

    void ApplyWalkingPose(float sin)
    {
        float leftLeg = walkStepAngle * sin;
        float rightLeg = -walkStepAngle * sin;
        float leftArm = -walkArmSwing * sin;
        float rightArm = walkArmSwing * sin;

        leftThigh.localRotation = leftThighStart * Quaternion.Euler(leftLeg, 0, 0);
        rightThigh.localRotation = rightThighStart * Quaternion.Euler(rightLeg, 0, 0);
        leftShin.localRotation = GetBaseRot(leftShin) * Quaternion.Euler(-leftLeg * 0.5f, 0, 0);
        rightShin.localRotation = GetBaseRot(rightShin) * Quaternion.Euler(-rightLeg * 0.5f, 0, 0);
        leftFoot.localRotation = GetBaseRot(leftFoot) * Quaternion.Euler(leftLeg * 0.3f, 0, 0);
        rightFoot.localRotation = GetBaseRot(rightFoot) * Quaternion.Euler(rightLeg * 0.3f, 0, 0);

        leftUpperArm.localRotation = leftArmStart * Quaternion.Euler(leftArm, 0, 0);
        rightUpperArm.localRotation = rightArmStart * Quaternion.Euler(rightArm, 0, 0);
        leftForeArm.localRotation = GetBaseRot(leftForeArm) * Quaternion.Euler(-leftArm * 0.3f, 0, 0);
        rightForeArm.localRotation = GetBaseRot(rightForeArm) * Quaternion.Euler(-rightArm * 0.3f, 0, 0);

        hips.localRotation = hipStart * Quaternion.Euler(2f * sin, 0, 0);
        spine.localRotation = spineStart;
        chest.localRotation = chestStart;
    }

    IEnumerator SlipRoutine()
    {
        float timer = 0;
        hips.localRotation *= Quaternion.Euler(initialTilt, 0, 0);

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float k = timer / duration;
            float kk = k * k;

            // 身体倾斜
            hips.localRotation = hipStart * Quaternion.Euler(forwardAngle * kk, 0, sideAngle * kk);
            spine.localRotation = spineStart * Quaternion.Euler(forwardAngle * spineFactor * kk, 0, sideAngle * 0.5f * kk);
            chest.localRotation = chestStart * Quaternion.Euler(forwardAngle * chestFactor * kk, 0, sideAngle * 0.3f * kk);

            // ==============================================
            // 只保留：左脚滑倒 或 右脚滑倒
            // ==============================================
            if (slipLeftLeg)
            {
                // 左脚向后滑出去
                leftThigh.localRotation = leftThighStart * Quaternion.Euler(-legAngle * kk, 0, 0);
                rightThigh.localRotation = rightThighStart;
            }
            else
            {
                // 右脚向后滑出去
                rightThigh.localRotation = rightThighStart * Quaternion.Euler(-legAngle * kk, 0, 0);
                leftThigh.localRotation = leftThighStart;
            }

            // 手臂张开
            leftUpperArm.localRotation = leftArmStart * Quaternion.Euler(-armAngle * kk, 0, 0);
            rightUpperArm.localRotation = rightArmStart * Quaternion.Euler(-armAngle * kk, 0, 0);

            hips.position += slipDir * slipSpeed * Time.deltaTime;

            yield return null;
        }

        Physics.SyncTransforms();
        yield return StartCoroutine(EnableRagdollSmooth());
    }

    void CacheStartRotations()
    {
        hipStart = hips.localRotation;
        spineStart = spine.localRotation;
        chestStart = chest.localRotation;

        leftThighStart = leftThigh.localRotation;
        rightThighStart = rightThigh.localRotation;
        leftArmStart = leftUpperArm.localRotation;
        rightArmStart = rightUpperArm.localRotation;
    }

    Quaternion GetBaseRot(Transform t)
    {
        if (initialPose == null) return t.localRotation;
        foreach (var b in initialPose.bones)
        {
            if (b.boneName == t.name)
                return b.localRotation;
        }
        return t.localRotation;
    }

    IEnumerator EnableRagdollSmooth()
    {
        if (ragdollEnabled) yield break;
        ragdollEnabled = true;

        // 同步 transforms
        Physics.SyncTransforms();

        // 缓存目标 transform
        Dictionary<Rigidbody, Vector3> targetPos = new Dictionary<Rigidbody, Vector3>();
        Dictionary<Rigidbody, Quaternion> targetRot = new Dictionary<Rigidbody, Quaternion>();

        foreach (var rb in ragdollBodies)
        {
            if (!lastPos.ContainsKey(rb.transform)) continue;

            targetPos[rb] = rb.transform.position;
            targetRot[rb] = rb.transform.rotation;

            rb.isKinematic = true; // 先保持 kinematic
        }

        int steps = 5; // 分帧平滑过渡
        for (int i = 0; i < steps; i++)
        {
            float alpha = (i + 1f) / steps;

            foreach (var rb in ragdollBodies)
            {
                Vector3 pos = Vector3.Lerp(rb.position, targetPos[rb], alpha);
                Quaternion rot = Quaternion.Slerp(rb.rotation, targetRot[rb], alpha);

                rb.position = pos;
                rb.rotation = rot;

                // 平滑线速度
                Vector3 vel = (pos - lastPos[rb.transform]) / Time.fixedDeltaTime;
                rb.velocity = Vector3.Lerp(rb.velocity, vel, alpha);

                // 平滑角速度
                Quaternion delta = rot * Quaternion.Inverse(lastRot[rb.transform]);
                delta.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f) angle -= 360f;
                Vector3 angularVel = axis * angle * Mathf.Deg2Rad / Time.fixedDeltaTime;
                rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, angularVel, alpha);

                rb.maxAngularVelocity = 20f;
            }

            yield return new WaitForFixedUpdate(); // 保证物理平滑
        }

        // 最后交给物理
        foreach (var rb in ragdollBodies)
            rb.isKinematic = false;

        // 给身体施加滑倒力
        Vector3 force = slipDir * Random.Range(80, 140) + Vector3.down * 30;
        GetForceTarget().AddForce(force, ForceMode.Impulse);
    }

    Rigidbody GetForceTarget()
    {
        int r = Random.Range(0, 3);
        if (r == 0) return hipsRB;
        if (r == 1 && spine != null) return spine.GetComponent<Rigidbody>();
        if (r == 2 && chest != null) return chest.GetComponent<Rigidbody>();
        return hipsRB;
    }

    void ResetPose()
    {
        ApplyPoseAsset(initialPose);
        hips.localPosition = initialHipsLocalPos;

        foreach (var rb in ragdollBodies)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            if (rb != hipsRB)
                rb.isKinematic = true;
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