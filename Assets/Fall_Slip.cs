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
    public Transform leftThigh, rightThigh, leftShin, rightShin, leftFoot, rightFoot;

    [Header("Arms")]
    public Transform leftUpperArm, rightUpperArm, leftForeArm, rightForeArm;

    [Header("Pose")]
    public PoseAsset initialPose;

    [Header("Ragdoll")]
    public Rigidbody hipsRB;

    List<Rigidbody> ragdollBodies = new List<Rigidbody>();

    [Header("Walk")]
    public float walkMoveSpeed = 0.5f;
    public float walkStepAngle = 25f;
    public float walkArmSwing = 30f;
    public float walkDuration = 0.8f;

    float forwardAngle, sideAngle, legAngle, armAngle, duration;
    float spineFactor, chestFactor;
    float slipSpeed;
    Vector3 slipDir;
    float initialTilt;
    bool slipLeftLeg;

    bool ragdollEnabled;

    Quaternion leftThighStart, rightThighStart, leftArmStart, rightArmStart;
    Quaternion hipStart, spineStart, chestStart;
    Vector3 initialHipsLocalPos;

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
        initialHipsLocalPos = hips.localPosition;
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
        slipLeftLeg = Random.value > 0.5f;
    }

    IEnumerator WalkThenSlip()
    {
        ragdollEnabled = false;

        float t = 0;
        float phase = 0;

        CacheStartRotations();

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

        yield return StartCoroutine(SlipRoutine());
    }

    void ApplyWalkingPose(float sin)
    {
        float l = walkStepAngle * sin;
        float r = -walkStepAngle * sin;

        leftThigh.localRotation = leftThighStart * Quaternion.Euler(l, 0, 0);
        rightThigh.localRotation = rightThighStart * Quaternion.Euler(r, 0, 0);

        leftUpperArm.localRotation = leftArmStart * Quaternion.Euler(-walkArmSwing * sin, 0, 0);
        rightUpperArm.localRotation = rightArmStart * Quaternion.Euler(walkArmSwing * sin, 0, 0);

        hips.localRotation = hipStart * Quaternion.Euler(2f * sin, 0, 0);
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

            hips.localRotation = hipStart * Quaternion.Euler(forwardAngle * kk, 0, sideAngle * kk);
            spine.localRotation = spineStart * Quaternion.Euler(forwardAngle * spineFactor * kk, 0, sideAngle * 0.5f * kk);
            chest.localRotation = chestStart * Quaternion.Euler(forwardAngle * chestFactor * kk, 0, sideAngle * 0.3f * kk);

            if (slipLeftLeg)
                leftThigh.localRotation = leftThighStart * Quaternion.Euler(-legAngle * kk, 0, 0);
            else
                rightThigh.localRotation = rightThighStart * Quaternion.Euler(-legAngle * kk, 0, 0);

            hips.position += slipDir * slipSpeed * Time.deltaTime;

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

                // ✅ 正确 velocity（关键修复）
                Vector3 vel = (targetPos[rb] - rb.position) / (steps * Time.fixedDeltaTime);
                vel = Vector3.ClampMagnitude(vel, 10f);

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

                angularVel = Vector3.ClampMagnitude(angularVel, 20f);
                rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, angularVel, alpha);

                rb.maxAngularVelocity = 20f;
            }

            yield return new WaitForFixedUpdate();
        }

        foreach (var rb in ragdollBodies)
            if (rb != null)
                rb.isKinematic = false;

        // ✅ 降低冲量（关键修复）
        Vector3 force = slipDir * Random.Range(30, 60) + Vector3.down * 20;
        GetForceTarget().AddForce(force, ForceMode.Impulse);
    }

    Rigidbody GetForceTarget()
    {
        int r = Random.Range(0, 3);
        if (r == 0) return hipsRB;

        if (r == 1 && spine != null)
            return spine.GetComponent<Rigidbody>();

        if (r == 2 && chest != null)
            return chest.GetComponent<Rigidbody>();

        return hipsRB;
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

    void ResetPose()
    {
        ApplyPoseAsset(initialPose);
        hips.localPosition = initialHipsLocalPos;

        foreach (var rb in ragdollBodies)
        {
            if (rb == null) continue;

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