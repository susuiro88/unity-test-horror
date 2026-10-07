using UnityEngine;
using UnityEngine.UI;

// 発光判定の後に、現在の割り当てを使った案内を画面右下へ表示する。
[DefaultExecutionOrder(10003)]
public sealed class OptionsControlGuide : MonoBehaviour
{
    private Text label;
    private GameObject hud;
    private CameraSwitch cameraSwitch;
    private PlayerProximityHint proximity;
    private InteractableHighlight[] highlights;
    private float nextScan;

    public void Initialize(Font font)
    {
        // メニューのCanvasを隠しても案内は表示できるよう、独立したCanvasを作る。
        hud = new GameObject("Control Guide", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = hud.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 150;
        var scaler = hud.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var item = new GameObject("Hint", typeof(RectTransform), typeof(Text), typeof(Shadow)); item.transform.SetParent(hud.transform, false);
        var rect = item.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0);
        rect.anchoredPosition = new Vector2(-48, 42); rect.sizeDelta = new Vector2(760, 100);
        label = item.GetComponent<Text>(); label.font = font; label.fontSize = 26; label.color = Color.white;
        label.alignment = TextAnchor.LowerRight; label.raycastTarget = false;
    }
    private void LateUpdate()
    {
        if (label == null) return;
        int size = GameOptions.Current.choices[5];
        label.fontSize = Mathf.RoundToInt(26 * (size == 1 ? 1.25f : size == 2 ? .85f : 1f));
        label.text = "";
        if (GameOptions.Blocked || GameOptions.Current.choices[6] != 0) return;
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + .5f;
            cameraSwitch = FindFirstObjectByType<CameraSwitch>(); proximity = FindFirstObjectByType<PlayerProximityHint>();
            highlights = FindObjectsByType<InteractableHighlight>(FindObjectsSortMode.None);
        }
        if (cameraSwitch == null || cameraSwitch.IntroActive || DoorInteraction.IsDoorBusy) return;
        if (cameraSwitch.FirstPersonMode)
        {
            if (highlights != null) foreach (var target in highlights)
                if (target != null && target.isActiveAndEnabled && target.IsHighlighted)
                { label.text = "[ " + GameOptions.ActionLabel(GameOptions.Action.Interact) + " ]  調べる"; return; }
        }
        else if (proximity != null && proximity.IsHintVisible)
            label.text = "[ " + GameOptions.ActionLabel(GameOptions.Action.Observe) + " ]  一人称で観察";
    }
    private void OnDestroy() { if (hud != null) Destroy(hud); }
}
