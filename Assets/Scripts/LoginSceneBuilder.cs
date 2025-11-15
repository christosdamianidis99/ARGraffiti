#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class LoginSceneBuilder
{
    [MenuItem("Tools/ARGraffiti/Build Google-Only Login Scene")]
    public static void Build()
    {
        // Canvas + scaler
        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 2400);
        scaler.matchWidthOrHeight = 0.5f;

        // EventSystem if missing
        if (!Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>())
        {
            var es = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }

        // SafeArea
        var safe = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeArea));
        var safeRT = safe.GetComponent<RectTransform>();
        safeRT.SetParent(canvasGO.transform, false);
        safeRT.anchorMin = Vector2.zero; safeRT.anchorMax = Vector2.one;
        safeRT.offsetMin = safeRT.offsetMax = Vector2.zero;

        // Background
        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        var bgRT = bg.GetComponent<RectTransform>();
        bgRT.SetParent(safeRT, false);
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        var bgImg = bg.GetComponent<Image>();
        bgImg.color = new Color(0.08f, 0.09f, 0.12f, 1f); // deep gray

        // Header
        var header = CreateTMP(safeRT, "HeaderTitle", "AR Graffiti", 54, TextAlignmentOptions.Center, new Color(1, 1, 1, 0.95f));
        var headerRT = header.GetComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0.08f, 0.78f);
        headerRT.anchorMax = new Vector2(0.92f, 0.92f);
        headerRT.offsetMin = headerRT.offsetMax = Vector2.zero;

        var subtitle = CreateTMP(safeRT, "Subtitle", "Sign in to continue", 28, TextAlignmentOptions.Center, new Color(1, 1, 1, 0.65f));
        var subRT = subtitle.GetComponent<RectTransform>();
        subRT.anchorMin = new Vector2(0.12f, 0.72f);
        subRT.anchorMax = new Vector2(0.88f, 0.79f);
        subRT.offsetMin = subRT.offsetMax = Vector2.zero;

        // Card panel
        var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
        var cardRT = card.GetComponent<RectTransform>();
        cardRT.SetParent(safeRT, false);
        cardRT.anchorMin = new Vector2(0.06f, 0.42f);
        cardRT.anchorMax = new Vector2(0.94f, 0.68f);
        cardRT.offsetMin = cardRT.offsetMax = Vector2.zero;
        var cardImg = card.GetComponent<Image>();
        cardImg.color = new Color(1, 1, 1, 0.06f);

        // Google button (only action)
        var googleBtn = CreateButton(cardRT, "GoogleButton", "Continue with Google");
        var gRT = googleBtn.GetComponent<RectTransform>();
        gRT.anchorMin = new Vector2(0.06f, 0.28f);
        gRT.anchorMax = new Vector2(0.94f, 0.72f);
        gRT.offsetMin = gRT.offsetMax = Vector2.zero;

        // Status line
        var status = CreateTMP(safeRT, "Status", "Ready.", 20, TextAlignmentOptions.Center, new Color(1, 1, 1, 0.6f));
        var statusRT = status.GetComponent<RectTransform>();
        statusRT.anchorMin = new Vector2(0.08f, 0.14f);
        statusRT.anchorMax = new Vector2(0.92f, 0.20f);
        statusRT.offsetMin = statusRT.offsetMax = Vector2.zero;

        // Controller
        var ctrlGO = new GameObject("GoogleSignInController", typeof(GoogleSignInController));
        ctrlGO.transform.SetParent(safeRT, false);
        var ctrl = ctrlGO.GetComponent<GoogleSignInController>();
        ctrl.googleButton = googleBtn.GetComponent<Button>();
        ctrl.statusText = status;

        // Version (small top-right)
        var ver = CreateTMP(safeRT, "Version", "v" + Application.version, 16, TextAlignmentOptions.Right, new Color(1, 1, 1, 0.45f));
        var verRT = ver.GetComponent<RectTransform>();
        verRT.anchorMin = new Vector2(0.70f, 0.94f);
        verRT.anchorMax = new Vector2(0.96f, 0.985f);
        verRT.offsetMin = verRT.offsetMax = Vector2.zero;

        Undo.RegisterCreatedObjectUndo(canvasGO, "Build Google Login UI");
        Selection.activeObject = canvasGO;
        Debug.Log("[ARGraffiti] Google-only login UI built.");

        // Helpers
        static TextMeshProUGUI CreateTMP(Transform parent, string name, string text, int size, TextAlignmentOptions align, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.alignment = align; t.color = color;
            var rt = go.GetComponent<RectTransform>();
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return t;
        }

        static GameObject CreateButton(Transform parent, string name, string label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.95f, 0.95f, 1f, 1f);

            // Google logo placeholder (left square)
            var logo = new GameObject("GMark", typeof(RectTransform), typeof(Image));
            var lrt = logo.GetComponent<RectTransform>();
            lrt.SetParent(go.transform, false);
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(0f, 1f);
            lrt.pivot = new Vector2(0f, 0.5f);
            lrt.sizeDelta = new Vector2(120, 0);
            logo.GetComponent<Image>().color = new Color(0.91f, 0.95f, 1f, 1f); // pale accent

            // Label
            var txt = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var t = txt.GetComponent<TextMeshProUGUI>();
            t.text = label; t.fontSize = 30; t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.09f, 0.10f, 0.14f, 1f);
            var trt = txt.GetComponent<RectTransform>();
            trt.SetParent(go.transform, false);
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(8, 8); trt.offsetMax = new Vector2(-8, -8);

            // Button colors
            var bt = go.GetComponent<Button>();
            var cb = bt.colors;
            cb.normalColor = img.color;
            cb.highlightedColor = new Color(0.93f, 0.93f, 1f, 1f);
            cb.pressedColor = new Color(0.88f, 0.88f, 0.98f, 1f);
            cb.disabledColor = new Color(0.85f, 0.85f, 0.9f, 1f);
            bt.colors = cb;

            return go;
        }
    }
}
#endif
