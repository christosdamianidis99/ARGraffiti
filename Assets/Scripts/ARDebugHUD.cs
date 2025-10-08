using UnityEngine;

public class ARDebugHUD : MonoBehaviour
{
    public AppStateControllerPhone controller;
    public PhonePainter painter;
    public ReticleDot reticle;

    void OnGUI()
    {
        string s =
            $"Reticle over plane: {reticle?.isOverAnyPlane}\n" +
            $"Locked plane: {(painter?.lockedPlane ? painter.lockedPlane.trackableId.ToString() : "null")}\n" +
            $"Painting active: {painter?.paintingActive}";
        GUI.Label(new Rect(10, 10, 1200, 80), s);
    }
}
