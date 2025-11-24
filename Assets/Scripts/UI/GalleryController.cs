using UnityEngine;

/// <summary>
/// Lightweight bridge that routes gallery requests to the AR world presenter inside
/// <see cref="AppStateControllerPhone"/>. The old scroll/list UI is replaced by an
/// in-world gallery that spawns preview quads at their saved poses.
/// </summary>
public class GalleryController : MonoBehaviour
{
    public string ownerEmailFilter;

    AppStateControllerPhone _app;

    void Awake()
    {
        if (!_app)
            _app = FindObjectOfType<AppStateControllerPhone>(true);
    }

    public void SetOwnerFilter(string email)
    {
        ownerEmailFilter = email;
    }

    public void Show()
    {
        if (!_app)
            _app = FindObjectOfType<AppStateControllerPhone>(true);

        if (_app)
            _app.ShowGalleryInAR();
    }

    public void Hide()
    {
        if (!_app)
            _app = FindObjectOfType<AppStateControllerPhone>(true);

        if (_app)
            _app.HideGalleryPreviews();
    }
}
