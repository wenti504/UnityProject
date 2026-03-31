using UnityEngine;
using System.Collections.Generic;

public class SkeletonVisualizerRuntime : MonoBehaviour
{
    public Material lineMaterial;
    public float lineWidth = 0.01f;

    private List<(Transform parent, Transform child)> bonePairs = new List<(Transform, Transform)>();
    private List<LineRenderer> lines = new List<LineRenderer>();
    private List<GameObject> lineObjects = new List<GameObject>();

    void Start()
    {
        CacheBones(transform);
        CreateLineRenderers();
    }

    void LateUpdate()
    {
        UpdateLines();
    }

    // -------- 缓存骨骼（只缓存有Collider的子物体）--------
    void CacheBones(Transform t)
    {
        foreach (Transform child in t)
        {
            // 只有子物体有 Collider 才连线
            if (child.GetComponent<Collider>() != null)
            {
                bonePairs.Add((t, child));
            }

            // 继续递归
            CacheBones(child);
        }
    }

    // -------- 创建 LineRenderer --------
    void CreateLineRenderers()
    {
        foreach (var pair in bonePairs)
        {
            GameObject lineObj = new GameObject("BoneLine");
            lineObj.transform.SetParent(null, false);

            lineObj.tag = "SkeletonVisualizerLine";

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.material = lineMaterial;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.positionCount = 2;
            lr.useWorldSpace = true;

            lines.Add(lr);
            lineObjects.Add(lineObj);
        }
    }

    // -------- 更新骨骼线 --------
    void UpdateLines()
    {
        if (bonePairs.Count == 0 || lines.Count == 0) return;

        if (transform == null || !transform.gameObject.activeInHierarchy)
        {
            CleanupLines();
            return;
        }

        for (int i = 0; i < Mathf.Min(lines.Count, bonePairs.Count); i++)
        {
            if (bonePairs[i].parent != null &&
                bonePairs[i].child != null &&
                lines[i] != null)
            {
                lines[i].SetPosition(0, bonePairs[i].parent.position);
                lines[i].SetPosition(1, bonePairs[i].child.position);
            }
        }
    }

    // -------- 清理线条 --------
    public void CleanupLines()
    {
        foreach (GameObject lineObj in lineObjects)
        {
            if (lineObj != null)
            {
                Destroy(lineObj);
            }
        }

        lines.Clear();
        lineObjects.Clear();
        bonePairs.Clear();
    }

    void OnDestroy()
    {
        CleanupLines();
    }

    void OnDisable()
    {
        CleanupLines();
    }

    public int GetLineCount()
    {
        return lineObjects.Count;
    }

    public void SetLinesVisible(bool visible)
    {
        foreach (LineRenderer line in lines)
        {
            if (line != null)
            {
                line.enabled = visible;
            }
        }
    }
}