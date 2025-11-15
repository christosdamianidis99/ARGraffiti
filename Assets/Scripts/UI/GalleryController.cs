using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class GalleryController : MonoBehaviour
{
    public RectTransform contentRoot;   // the Content of a ScrollView
    public GameObject itemPrefab;       // optional; auto-create simple one if null

    void Start()
    {
        EnsureUI();
        Refresh();
    }

    void EnsureUI()
    {
        if (contentRoot != null) return;

        // Look for existing
        var scroll = GameObject.Find("GalleryScroll");
        if (scroll != null)
        {
            var sv = scroll.GetComponentInChildren<ScrollRect>(true);
            if (sv != null) { contentRoot = sv.content; return; }
        }

        // Create simple ScrollView overlay
        var canvasGO = new GameObject("GalleryCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4000;
        canvasGO.AddComponent<GraphicRaycaster>();

        var panel = new GameObject("Panel");
        panel.transform.SetParent(canvasGO.transform, false);
        var pRect = panel.AddComponent<RectTransform>();
        pRect.anchorMin = new Vector2(0, 0);
        pRect.anchorMax = new Vector2(1, 1);
        pRect.offsetMin = new Vector2(20, 20);
        pRect.offsetMax = new Vector2(-20, -20);
        var img = panel.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0.4f);

        var scrollGO = new GameObject("GalleryScroll");
        scrollGO.transform.SetParent(panel.transform, false);
        var sRect = scrollGO.AddComponent<RectTransform>();
        sRect.anchorMin = new Vector2(0, 0);
        sRect.anchorMax = new Vector2(1, 1);
        sRect.offsetMin = new Vector2(20, 20);
        sRect.offsetMax = new Vector2(-20, -20);

        var scrollRect = scrollGO.AddComponent<ScrollRect>();
        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollGO.transform, false);
        var vRect = viewport.AddComponent<RectTransform>();
        vRect.anchorMin = Vector2.zero; vRect.anchorMax = Vector2.one;
        vRect.offsetMin = vRect.offsetMax = Vector2.zero;
        var mask = viewport.AddComponent<Mask>(); mask.showMaskGraphic = false;
        var vImg = viewport.AddComponent<Image>(); vImg.color = new Color(0, 0, 0, 0.1f);

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        contentRoot = content.AddComponent<RectTransform>();
        contentRoot.anchorMin = new Vector2(0, 1);
        contentRoot.anchorMax = new Vector2(1, 1);
        contentRoot.pivot = new Vector2(0.5f, 1);
        contentRoot.offsetMin = contentRoot.offsetMax = Vector2.zero;

        var layout = content.AddComponent<VerticalLayoutGroup>();
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.spacing = 12;
        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = contentRoot;
        scrollRect.viewport = vRect;
    }

    public void Refresh()
    {
        foreach (Transform t in contentRoot) Destroy(t.gameObject);

        foreach (var data in GraffitiRepository.I.All())
        {
            var item = itemPrefab ? Instantiate(itemPrefab, contentRoot) : CreateItemGO();
            BindItem(item, data);
        }
    }

    GameObject CreateItemGO()
    {
        var go = new GameObject("Item");
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 160);

        var imgGO = new GameObject("Thumb");
        imgGO.transform.SetParent(go.transform, false);
        var imgRect = imgGO.AddComponent<RectTransform>();
        imgRect.anchorMin = new Vector2(0, 0);
        imgRect.anchorMax = new Vector2(0, 1);
        imgRect.pivot = new Vector2(0, 0.5f);
        imgRect.sizeDelta = new Vector2(160, 0);
        var img = imgGO.AddComponent<Image>();

        var txtGO = new GameObject("Label");
        txtGO.transform.SetParent(go.transform, false);
        var tRect = txtGO.AddComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0, 0);
        tRect.anchorMax = new Vector2(1, 1);
        tRect.offsetMin = new Vector2(170, 20);
        tRect.offsetMax = new Vector2(-180, -20);
        var txt = txtGO.AddComponent<Text>();
        txt.alignment = TextAnchor.MiddleLeft;
        txt.resizeTextForBestFit = true;
        txt.resizeTextMinSize = 14;
        txt.resizeTextMaxSize = 28;
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.color = Color.white;

        var btnGO = new GameObject("ViewAR");
        btnGO.transform.SetParent(go.transform, false);
        var bRect = btnGO.AddComponent<RectTransform>();
        bRect.anchorMin = new Vector2(1, 0.5f);
        bRect.anchorMax = new Vector2(1, 0.5f);
        bRect.pivot = new Vector2(1, 0.5f);
        bRect.sizeDelta = new Vector2(160, 60);
        bRect.anchoredPosition = new Vector2(-10, 0);
        var imgBtn = btnGO.AddComponent<Image>();
        imgBtn.color = new Color(1, 1, 1, 0.1f);
        var btn = btnGO.AddComponent<Button>();

        var btnTxtGO = new GameObject("Text");
        btnTxtGO.transform.SetParent(btnGO.transform, false);
        var btRect = btnTxtGO.AddComponent<RectTransform>();
        btRect.anchorMin = Vector2.zero; btRect.anchorMax = Vector2.one;
        btRect.offsetMin = btRect.offsetMax = Vector2.zero;
        var bt = btnTxtGO.AddComponent<Text>();
        bt.alignment = TextAnchor.MiddleCenter;
        bt.text = "View in AR";
        bt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        bt.color = Color.white;

        return go;
    }

    void BindItem(GameObject item, GraffitiData data)
    {
        var thumb = item.transform.Find("Thumb")?.GetComponent<Image>();
        var label = item.transform.Find("Label")?.GetComponent<Text>();
        var btn = item.transform.Find("ViewAR")?.GetComponent<Button>();

        // Load thumbnail or main image
        string path = File.Exists(data.thumbPath) ? data.thumbPath : data.pngPath;
        if (thumb && File.Exists(path))
        {
            var bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2);
            tex.LoadImage(bytes);
            var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100);
            thumb.sprite = sp;
            thumb.preserveAspect = true;
        }

        if (label)
        {
            var title = string.IsNullOrEmpty(data.title) ? data.id : data.title;
            label.text = $"{title}\n{data.CreatedUtc.ToLocalTime():yyyy-MM-dd HH:mm}";
        }

        if (btn)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                // Stash the selected id for the AR scene to load
                PlayerPrefs.SetString("graffiti.last_id", data.id);
                PlayerPrefs.Save();
                // Load your AR scene (replace with your AR scene name)
                UnityEngine.SceneManagement.SceneManager.LoadScene("02_ARMainScene");
            });
        }
    }
}
