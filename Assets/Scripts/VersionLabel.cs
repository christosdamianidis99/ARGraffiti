using UnityEngine;
using TMPro;

public class VersionLabel : MonoBehaviour
{
    void Awake()
    {
        var tmp = GetComponent<TMP_Text>();
        tmp.text = $"ARGraffiti v{Application.version}";
    }
}
