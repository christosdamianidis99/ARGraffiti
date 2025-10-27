using UnityEngine;

/// <summary>
/// 跨平台安全区域适配器
/// 自动适配有刘海/Notch的设备（iPhone X系列、Android打孔屏等）
/// 对于没有刘海的设备，不做任何调整
/// </summary>
public class SafeAreaAdapter : MonoBehaviour
{
    private RectTransform rectTransform;
    private Rect lastSafeArea = new Rect(0, 0, 0, 0);
    private bool needsAdaptation = false;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    void Update()
    {
        // 只有在需要适配且安全区域改变时才更新（例如设备旋转）
        if (needsAdaptation && lastSafeArea != Screen.safeArea)
        {
            ApplySafeArea();
        }
    }

    void ApplySafeArea()
    {
        Rect safeArea = Screen.safeArea;
        lastSafeArea = safeArea;

        // 检查是否真的需要适配
        // 如果安全区域等于屏幕尺寸，说明没有刘海/Notch，无需调整
        bool hasSafeAreaInsets = 
            safeArea.x > 0 || 
            safeArea.y > 0 || 
            safeArea.width < Screen.width || 
            safeArea.height < Screen.height;

        if (!hasSafeAreaInsets)
        {
            // 没有刘海/Notch，保持默认布局（填满整个屏幕）
            needsAdaptation = false;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            return;
        }

        // 有刘海/Notch，需要适配
        needsAdaptation = true;

        // 将安全区域转换为锚点（标准化坐标 0-1）
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        
        // 重置偏移，让内容完全填充安全区域
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}

