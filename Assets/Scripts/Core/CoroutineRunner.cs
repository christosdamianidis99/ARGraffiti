using System.Collections;
using UnityEngine;

/// <summary>
/// Central runner that keeps coroutines alive even if the caller's GameObject
/// is disabled (for example, when XR Origin is hidden while showing the gallery).
/// </summary>
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

    /// <summary>
    /// Start a coroutine on a stable runner so calls succeed even if the
    /// initiating behaviour is inactive.
    /// </summary>
    public static Coroutine Run(IEnumerator routine)
    {
        if (routine == null) return null;
        return Instance.StartCoroutine(routine);
    }

    /// <summary>
    /// Stop a coroutine that was started through <see cref="Run"/>.
    /// </summary>
    public static void Stop(Coroutine routine)
    {
        if (routine == null || _instance == null) return;
        _instance.StopCoroutine(routine);
    }
}
