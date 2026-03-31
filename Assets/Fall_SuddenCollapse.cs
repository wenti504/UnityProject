using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Fall_SuddenCollapse : MonoBehaviour, IFallController
{
    [Header("Body")]
    public Transform hips;
    public Transform spine;
    public Transform chest;

    [Header("Pose")]
    public PoseAsset initialPose;

    [Header("Ragdoll")]
    public Rigidbody hipsRB;
    public List<Rigidbody> ragdollBodies = new List<Rigidbody>();

    // 让腿不回弹的关键参数
    [Header("腿软不回弹设置")]
    public float legDrag = 8f;
    public float legAngularDrag = 10f;
    public float bodyAngularDrag = 4f;

    // ===== 随机参数 =====
    float duration;
    float fallYaw;
    int collapseType;

    float spineFactor;
    float chestFactor;

    float initialTiltX;
    float initialTiltZ;

    float dropOffset;
    bool kneeCollapse;

    bool ragdollEnabled;

    // 防突变：速度缓存
    Dictionary<Transform, Vector3> lastPos = new Dictionary<Transform, Vector3>();
    Dictionary<Transform, Quaternion> lastRot = new Dictionary<Transform, Quaternion>();

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
        ResetPose();
        Randomize();
        StartCoroutine(CollapseRoutine());
    }

    void Randomize()
    {
        // 极快失控：符合“突然脱力”
        duration = Random.Range(0.12f, 0.25f);

        // 小幅偏摆，不自转
        fallYaw = Random.Range(-30f, 30f);

        // 0=垂直瘫软 1=前软倒 2=后软倒 3=侧软倒
        collapseType = Random.Range(0, 4);

        // 上半身完全放松
        spineFactor = Random.Range(0.15f, 0.3f);
        chestFactor = Random.Range(0.05f, 0.2f);

        // 微小初始晃动，更自然
        initialTiltX = Random.Range(-2f, 2f);
        initialTiltZ = Random.Range(-2f, 2f);

        // 下沉幅度（腿一软，直接沉）
        dropOffset = Random.Range(0.12f, 0.25f);

        // 大部分情况膝盖都会软
        kneeCollapse = Random.value > 0.2f;
    }

    IEnumerator CollapseRoutine()
    {
        ragdollEnabled = false;
        float timer = 0f;

        Quaternion hipStart = hips.localRotation;
        Quaternion spineStart = spine.localRotation;
        Quaternion chestStart = chest.localRotation;
        Vector3 hipStartPos = hips.localPosition;

        hips.localRotation *= Quaternion.Euler(initialTiltX, 0, initialTiltZ);

        // 【脱力核心】只小角度软倒，不大力前倾
        float forwardAngle = 0f;
        float sideAngle = 0f;

        switch (collapseType)
        {
            case 0: // 垂直瘫倒（最像脱力）
                forwardAngle = Random.Range(-5f, 5f);
                sideAngle = Random.Range(-5f, 5f);
                break;
            case 1: // 向前软倒
                forwardAngle = Random.Range(10f, 25f);
                break;
            case 2: // 向后软倒
                forwardAngle = Random.Range(-25f, -10f);
                break;
            case 3: // 侧向软倒
                sideAngle = Random.Range(-18f, 18f);
                break;
        }

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float k = timer / duration;
            float kk = k * k * k; // 更快塌陷，更软

            // 身体只轻微倾斜，重点是【下沉】
            hips.localRotation = hipStart * Quaternion.Euler(
                -forwardAngle * kk,
                fallYaw * 0.08f * kk,
                sideAngle * kk
            );

            // 腿一软，直接快速往下掉
            float drop = dropOffset * kk;
            if (kneeCollapse)
                hips.localPosition = hipStartPos + new Vector3(0, -drop * 2.2f, 0);
            else
                hips.localPosition = hipStartPos + new Vector3(0, -drop * 1f, 0);

            // 上半身完全放松，跟着塌
            spine.localRotation = spineStart * Quaternion.Euler(
                -forwardAngle * spineFactor * 0.25f * kk,
                0,
                sideAngle * 0.2f * kk
            );

            chest.localRotation = chestStart * Quaternion.Euler(
                -forwardAngle * chestFactor * 0.15f * kk,
                0,
                sideAngle * 0.12f * kk
            );

            yield return null;
        }

        Physics.SyncTransforms();
        StartCoroutine(EnableRagdollSmooth());
    }

    // 平滑开启布娃娃，完全不跳变 + 腿不回弹
    IEnumerator EnableRagdollSmooth()
    {
        if (ragdollEnabled) yield break;
        ragdollEnabled = true;

        // 同步位置、旋转、速度
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
            if (angle > 180) angle -= 360;
            rb.angularVelocity = axis * angle * Mathf.Deg2Rad / Time.fixedDeltaTime;

            // ========== 腿不回弹的核心 ==========
            if (rb.name.ToLower().Contains("leg") || rb.name.ToLower().Contains("knee") || rb.name.ToLower().Contains("foot"))
            {
                rb.drag = legDrag;
                rb.angularDrag = legAngularDrag;
            }
            else
            {
                rb.drag = 0;
                rb.angularDrag = bodyAngularDrag;
            }
        }

        yield return new WaitForFixedUpdate();

        // 统一开启物理
        foreach (var rb in ragdollBodies)
            rb.isKinematic = false;

        yield return new WaitForFixedUpdate();

        // 完全不给外力，只靠重力瘫
        // 去掉所有多余冲击
    }

    void ResetPose()
    {
        ApplyPoseAsset(initialPose);

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