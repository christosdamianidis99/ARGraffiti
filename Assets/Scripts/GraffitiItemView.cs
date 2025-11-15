using System;
using UnityEngine;
using UnityEngine.UI;

public class GraffitiItemView : MonoBehaviour
{
    [Header("UI")]
    public RawImage preview;
    public Button btnDelete;

    private string _objectId;
    private Action<string, GraffitiItemView> _onDelete;

    public void Init(string objectId, Texture2D tex, Action<string, GraffitiItemView> onDelete)
    {
        _objectId = objectId;
        _onDelete = onDelete;

        if (preview) preview.texture = tex;

        if (btnDelete)
        {
            btnDelete.onClick.RemoveAllListeners();
            btnDelete.onClick.AddListener(() =>
            {
                _onDelete?.Invoke(_objectId, this);
            });
        }

        // Optional: adjust preview aspect ratio
        if (tex && preview)
        {
            var aspect = (float)tex.width / Mathf.Max(1, tex.height);
            var rt = preview.rectTransform;
            rt.sizeDelta = new Vector2(rt.sizeDelta.y * aspect, rt.sizeDelta.y);
        }
    }
}
