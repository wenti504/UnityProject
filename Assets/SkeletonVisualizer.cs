using UnityEngine;

public class SkeletonVisualizer : MonoBehaviour
{
    public Color lineColor = Color.green;

    void OnDrawGizmos()
    {
        Gizmos.color = lineColor;

        if (transform == null) return;

        DrawRecursive(transform);
    }

    void DrawRecursive(Transform parent)
    {
        foreach (Transform child in parent)
        {
            // 只有当子物体有 Collider 才连线
            if (child.GetComponent<Collider>() != null)
            {
                Gizmos.DrawLine(parent.position, child.position);
            }

            // 继续递归
            DrawRecursive(child);
        }
    }
}