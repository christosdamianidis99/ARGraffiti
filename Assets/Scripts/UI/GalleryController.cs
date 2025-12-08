using UnityEngine;

public class GalleryController : MonoBehaviour
{
    public string ownerEmailFilter;

    AppStateControllerPhone _app;

    void Awake()
    {
        if (!_app)
            _app = FindFirstObjectByType<AppStateControllerPhone>(FindObjectsInactive.Include);
    }

    public void SetOwnerFilter(string email)
    {
        ownerEmailFilter = email;
    }

    public void Show()
    {
        if (!_app)
            _app = FindFirstObjectByType<AppStateControllerPhone>(FindObjectsInactive.Include);

        if (_app)
            _app.ShowGalleryInAR();
    }

    public void Hide()
    {
        if (!_app)
            _app = FindFirstObjectByType<AppStateControllerPhone>(FindObjectsInactive.Include);

        if (_app)
            _app.HideGalleryPreviews();
    }
}
