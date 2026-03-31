using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FallRunner : MonoBehaviour
{
    [Header("扫描目标（拖 hips 或 Player）")]
    public GameObject targetObject;

    public float interval = 2f;

    List<IFallController> fallControllers = new List<IFallController>();

    public IReadOnlyList<IFallController> Controllers => fallControllers;

    void Awake()
    {
        AutoRegister();
    }

    public void AutoRegister()
    {
        fallControllers.Clear();

        if (targetObject == null)
        {
            Debug.LogError("targetObject 未设置！");
            return;
        }

        // ⭐ 从目标物体上获取所有组件
        var monos = targetObject.GetComponents<MonoBehaviour>();

        foreach (var m in monos)
        {
            if (m is IFallController fc)
            {
                fallControllers.Add(fc);
            }
        }

        Debug.Log($"已从 {targetObject.name} 注册跌倒类型数量: {fallControllers.Count}");
    }

    public void Run(int index, int count)
    {
        if (index < 0 || index >= fallControllers.Count)
        {
            Debug.LogError("index 越界");
            return;
        }

        StopAllCoroutines();
        StartCoroutine(RunRoutine(index, count));
    }

    IEnumerator RunRoutine(int index, int count)
    {
        for (int i = 0; i < count; i++)
        {
            fallControllers[index].Play();
            yield return new WaitForSeconds(interval);
        }
    }
}