using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public class RequireAuth : MonoBehaviour
{
    [Tooltip("If empty, all behaviours on this GameObject (except this marker) will be gated automatically.")]
    public Behaviour[] specificBehaviours;

    // Resolved at runtime by the bootstrapper:
    internal IEnumerable<Behaviour> ResolveTargets()
    {
        if (specificBehaviours != null && specificBehaviours.Length > 0)
            return specificBehaviours.Where(b => b != null);

        // Auto-pick every enabled Behaviour on this GO except the marker itself
        return GetComponents<Behaviour>()
            .Where(b => b != null && b != this);
    }
}
