using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 字幕のText/TMP_Textと同じオブジェクトに付ける。本文や表示タイミングは既存の字幕処理に任せる。
public sealed class OptionsSubtitle : MonoBehaviour
{
    private Text label;
    private TMP_Text tmp;
    private float originalSize;
    private CanvasGroup group;
    private float originalAlpha;
    private void Awake()
    {
        label = GetComponent<Text>(); tmp = GetComponent<TMP_Text>();
        originalSize = tmp != null ? tmp.fontSize : label != null ? label.fontSize : 24;
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        originalAlpha = group.alpha;
    }
    private void OnEnable() { GameOptions.Changed += Refresh; Refresh(); }
    private void OnDisable() { GameOptions.Changed -= Refresh; }
    private void Refresh()
    {
        float scale = new[] { 1f, 1.25f, .85f }[GameOptions.Current.choices[5]];
        if (tmp != null) tmp.fontSize = originalSize * scale;
        if (label != null) label.fontSize = Mathf.RoundToInt(originalSize * scale);
        if (group != null) group.alpha = GameOptions.Current.choices[4] == 0 ? originalAlpha : 0;
    }
}
