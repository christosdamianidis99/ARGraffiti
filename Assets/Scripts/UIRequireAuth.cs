using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UIRequireAuth : MonoBehaviour
{
    [Tooltip("If empty, will try to find a Selectable on the same GameObject.")]
    public Selectable[] controls;

    internal Selectable[] Resolve()
    {
        if (controls != null && controls.Length > 0)
            return controls.Where(c => c != null).ToArray();

        var s = GetComponent<Selectable>();
        return s != null ? new[] { s } : new Selectable[0];
    }
}
