using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class DrawAuthGuard : MonoBehaviour
{
    [Header("Behaviours gated by auth")]
    [Tooltip("Scripts that must be enabled only for authenticated users (e.g., DrawManager, BrushController).")]
    public Behaviour[] drawingBehaviours;

    [Header("UI Groups to show/hide")]
    [Tooltip("Root GameObjects of toolbars or panels to show when signed in, hide otherwise.")]
    public GameObject[] toolbarsToToggle;

    [Header("Individual controls to disable")]
    [Tooltip("Buttons/sliders/etc. that should be interactable only when signed in.")]
    public Selectable[] buttonsToDisable;

    [Header("Optional: Overlay that blocks touches when signed out")]
    [Tooltip("CanvasGroup with a Panel + Text: set alpha=1, blocks Raycasts. Shown when logged out.")]
    public CanvasGroup signedOutOverlay;

    void OnEnable()
    {
        // In case AuthState not spawned yet (should be created by login flow)
        Apply(AuthState.I != null && AuthState.I.IsSignedIn);

        if (AuthState.I != null)
            AuthState.I.OnAuthChanged += Apply;
    }

    void OnDisable()
    {
        if (AuthState.I != null)
            AuthState.I.OnAuthChanged -= Apply;
    }

    void Apply(bool isSignedIn)
    {
        // Enable/disable all drawing scripts
        if (drawingBehaviours != null)
        {
            foreach (var b in drawingBehaviours.Where(b => b != null))
                b.enabled = isSignedIn;
        }

        // Show/hide toolbars
        if (toolbarsToToggle != null)
        {
            foreach (var go in toolbarsToToggle.Where(g => g != null))
                go.SetActive(isSignedIn);
        }

        // Disable individual controls
        if (buttonsToDisable != null)
        {
            foreach (var s in buttonsToDisable.Where(s => s != null))
                s.interactable = isSignedIn;
        }

        // Overlay that blocks all touches when signed out (recommended)
        if (signedOutOverlay != null)
        {
            signedOutOverlay.alpha = isSignedIn ? 0f : 1f;
            signedOutOverlay.blocksRaycasts = !isSignedIn;
            signedOutOverlay.interactable = !isSignedIn;
        }

        Debug.Log($"[DrawAuthGuard] Auth={isSignedIn}. Drawing {(isSignedIn ? "ENABLED" : "DISABLED")}.");
    }
}
