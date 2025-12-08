using System.Collections;
using UnityEngine;

public sealed class CoroutineRunner : MonoBehaviour
{
    static CoroutineRunner _instance;

    public static CoroutineRunner Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("CoroutineRunner");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<CoroutineRunner>();
            }

            return _instance;
        }
    }


    public static Coroutine Run(IEnumerator routine)
    {
        if (routine == null) return null;
        return Instance.StartCoroutine(routine);
    }


    public static void Stop(Coroutine routine)
    {
        if (routine == null || _instance == null) return;
        _instance.StopCoroutine(routine);
    }
}
