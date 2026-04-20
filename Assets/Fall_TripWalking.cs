using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Fall_TripWalking : MonoBehaviour, IFallController
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

    [Header("Initial Pose")]
    public PoseAsset initialPose;

    [Header("Ragdoll")]
    public Rigidbody hipsRB;
    public List<Rigidbody> ragdollBodies = new List<Rigidbody>();

    [Header("Walk")]
    public float moveSpeed = 0.5f;
    public float stepAngle = 28f;
    public float armSwing = 32f;
    public float walkPhaseSpeed = 2.2f;

    [Header("Trip — 随机绊倒设置")]
    // 时长随机
    public float tripTimeMin = 0.15f;
    public float tripTimeMax = 0.25f;

    // 前倾角度随机
    public float forwardMin = 15f;
    public float forwardMax = 30f;

    // 侧倾角度随机
    public float sideMin = 3f;
    public float sideMax = 10f;

    // 物理力度随机
    public float physicsPowerMin = 0.08f;
    public float physicsPowerMax = 0.12f;

    Vector3 initialHipsPos;
    bool ragdollEnabled;

    // 运行时随机值
    private float randomTripTime;
    private float randomForward;
    private float randomSide;
    private float randomPhysicsPower;

    class Snapshot
    {
        public Transform t;
        public Vector3 pos;
        public Quaternion rot;
    }

    List<Snapshot> origin = new List<Snapshot>();
    Dictionary<Transform, Vector3> lastPos = new Dictionary<Transform, Vector3>();
    Dictionary<Transform, Quaternion> lastRot = new Dictionary<Transform, Quaternion>();

    void Awake()
    {
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
            lastRot[rb.transform] = rb.rotation;
        }
    }

    public void Play()
    {
        StopAllCoroutines();
        ResetPose();

        // 🔥 每次播放都重新随机一次！
        RandomizeAll();

        StartCoroutine(DoTrip());
    }

    // 所有随机化
    void RandomizeAll()
    {
        randomTripTime = Random.Range(tripTimeMin, tripTimeMax);
        randomForward = Random.Range(forwardMin, forwardMax);
        randomSide = Random.Range(sideMin, sideMax);
        randomPhysicsPower = Random.Range(physicsPowerMin, physicsPowerMax);
    }

    IEnumerator DoTrip()
    {
        float t = 0;

        // 走路 1 秒
        while (t < 1f)
        {
            t += Time.deltaTime;
            float phase = Mathf.Sin(t * walkPhaseSpeed * Mathf.PI);
            ApplyWalk(phase);
            hips.position += transform.forward * moveSpeed * Time.deltaTime;
            yield return null;
        }

        // ==========================================
        // 随机：左脚绊 或 右脚绊
        // ==========================================
        bool leftTrip = Random.value > 0.5f;
        Transform pivotFoot = leftTrip ? leftFoot : rightFoot;
        Vector3 pivotWorldPos = pivotFoot.position;

        Transform stuckThigh = leftTrip ? leftThigh : rightThigh;
        Transform stuckShin = leftTrip ? leftShin : rightShin;
        Quaternion origThighRot = stuckThigh.localRotation;
        Quaternion origShinRot = stuckShin.localRotation;
        Quaternion origFootRot = pivotFoot.rotation;

        Vector3 startHipsPos = hips.position;
        Quaternion startHipsRot = hips.rotation;
        Quaternion startSpineRot = spine.rotation;

        float timer = 0;

        // 使用随机时长
        while (timer < randomTripTime)
        {
            timer += Time.deltaTime;
            float k = Mathf.Clamp01(timer / randomTripTime);

            // 脚钉死
            pivotFoot.position = pivotWorldPos;
            pivotFoot.rotation = origFootRot;
            stuckThigh.localRotation = origThighRot;
            stuckShin.localRotation = origShinRot;

            // 随机前倾 + 侧倾
            float forward = Mathf.Lerp(0, randomForward, k);
            float side = Mathf.Lerp(0, randomSide, k) * (leftTrip ? -1 : 1);

            Quaternion bodyRot = Quaternion.Euler(forward, 0, side);
            Vector3 offset = startHipsPos - pivotWorldPos;
            hips.position = pivotWorldPos + bodyRot * offset;
            hips.rotation = startHipsRot * bodyRot;

            yield return null;
        }

        // 物理接管 —— 使用5帧平滑过渡
        Physics.SyncTransforms();
        yield return StartCoroutine(EnableRagdollSmooth());
    }

    void ApplyWalk(float phase)
    {
        float s = phase;

        float leftLeg = stepAngle * s;
        float rightLeg = -stepAngle * s;

        leftThigh.localRotation = GetRot(leftThigh) * Quaternion.Euler(leftLeg, 0, 0);
        rightThigh.localRotation = GetRot(rightThigh) * Quaternion.Euler(rightLeg, 0, 0);
        leftShin.localRotation = GetRot(leftShin) * Quaternion.Euler(-leftLeg * 0.5f, 0, 0);
        rightShin.localRotation = GetRot(rightShin) * Quaternion.Euler(-rightLeg * 0.5f, 0, 0);

        float la = -armSwing * s;
        float ra = armSwing * s;

        leftUpperArm.localRotation = GetRot(leftUpperArm) * Quaternion.Euler(la, 0, 0);
        rightUpperArm.localRotation = GetRot(rightUpperArm) * Quaternion.Euler(ra, 0, 0);
        leftForeArm.localRotation = GetRot(leftForeArm) * Quaternion.Euler(-la * 0.3f, 0, 0);
        rightForeArm.localRotation = GetRot(rightForeArm) * Quaternion.Euler(-ra * 0.3f, 0, 0);

        hips.localRotation = GetRot(hips) * Quaternion.Euler(2 * s, 0, 0);
        spine.localRotation = GetRot(spine) * Quaternion.Euler(1 * s, 0, 0);

        hips.localPosition = new Vector3(
            hips.localPosition.x,
            initialHipsPos.y - Mathf.Abs(s) * 0.1f,
            hips.localPosition.z
        );
    }

    // ✅ 替换原来的 EnableRagdoll() 为 EnableRagdollSmooth()
    IEnumerator EnableRagdollSmooth()
    {
        if (ragdollEnabled) yield break;
        ragdollEnabled = true;

        Physics.SyncTransforms();

        Dictionary<Rigidbody, Vector3> targetPos = new Dictionary<Rigidbody, Vector3>();
        Dictionary<Rigidbody, Quaternion> targetRot = new Dictionary<Rigidbody, Quaternion>();

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

                // 平滑线速度（融入 randomPhysicsPower）
                Vector3 vel = (targetPos[rb] - rb.position) / (steps * Time.fixedDeltaTime);
                vel = Vector3.ClampMagnitude(vel, 10f);
                rb.velocity = Vector3.Lerp(rb.velocity, vel, alpha) * randomPhysicsPower;

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

                rb.drag = 2.5f;
                rb.angularDrag = 10f;
                rb.maxAngularVelocity = 20f;
            }

            yield return new WaitForFixedUpdate();
        }

        foreach (var rb in ragdollBodies)
        {
            if (rb == null) continue;
            rb.isKinematic = false;
        }
    }

    void CachePose()
    {
        origin.Clear();
        foreach (var t in hips.GetComponentsInChildren<Transform>())
            origin.Add(new Snapshot { t = t, pos = t.localPosition, rot = t.localRotation });
    }

    Quaternion GetRot(Transform t)
    {
        foreach (var s in origin) if (s.t == t) return s.rot;
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
            if (rb != hipsRB) rb.isKinematic = true;
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