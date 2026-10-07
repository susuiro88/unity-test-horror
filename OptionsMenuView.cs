using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Prefabの外観を維持し、設定の編集・保存・キー変更・一時停止を管理する。
[ExecuteAlways, DefaultExecutionOrder(-30000)]
public sealed class OptionsMenuView : MonoBehaviour
{
    [Serializable] public class RangeRow { public Slider slider; public Text valueText; public float defaultValue; }
    [Serializable] public class ChoiceRow { public Button button; public Text valueText; public string[] options; }
    [SerializeField] private Font japaneseFont;
    [SerializeField] private RangeRow[] ranges;
    [SerializeField] private RangeRow firstPersonSensitivity;
    [SerializeField] private ChoiceRow[] choices;
    [SerializeField] private Button resetButton, cancelButton, applyButton;
    [SerializeField] private Text statusText;
    private Font previewFont;
    private bool fontDirty, initialized, open;
    private GameOptions.Data draft;
    private readonly List<Vector2Int> resolutions = new();
    private readonly List<Button> bindingButtons = new();
    private KeyCode[] allKeys;
    private int captureSlot = -1, captureFrame;
    private CursorLockMode previousLock;
    private bool previousCursor, previousAudioPause;
    private float previousTimeScale;
    private GameObject ownedEventSystem;
    private Canvas canvas;
    private GraphicRaycaster raycaster;

    private void OnEnable() { RefreshFont(); }
    private void OnValidate() { fontDirty = true; }

    // 保存済みのPrefab参照を維持し、キー枠は固定の階層名から接続する。
    private void Start()
    {
        if (!Application.isPlaying) return;
        GameOptionsRuntime.EnsureAvailable();
        canvas = GetComponent<Canvas>(); raycaster = GetComponent<GraphicRaycaster>();
        EnsureSensitivityRow();
        firstPersonSensitivity.slider.onValueChanged.AddListener(value =>
        {
            if (draft == null) return;
            draft.firstPersonSensitivity = value;
            firstPersonSensitivity.valueText.text = Mathf.RoundToInt(value).ToString();
        });
        allKeys = (KeyCode[])Enum.GetValues(typeof(KeyCode));
        foreach (var r in Screen.resolutions) AddResolution(r.width, r.height);
        AddResolution(Screen.width, Screen.height); AddResolution(GameOptions.Current.width, GameOptions.Current.height);
        if (resolutions.Count == 0) AddResolution(1920, 1080);
        resolutions.Sort((a,b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        choices[1].options = resolutions.ConvertAll(r => r.x + " × " + r.y).ToArray();
        for (int i = 0; i < ranges.Length; i++)
        {
            int index = i;
            ranges[i].slider.onValueChanged.AddListener(value =>
            {
                if (draft == null) return;
                draft.ranges[index] = value; ranges[index].valueText.text = Mathf.RoundToInt(value).ToString();
            });
        }
        for (int i = 0; i < choices.Length; i++)
        {
            int index = i;
            choices[i].button.onClick.AddListener(() =>
            {
                if (captureSlot >= 0) return;
                draft.choices[index] = (draft.choices[index] + 1) % choices[index].options.Length;
                if (index == 1) { var r = resolutions[draft.choices[1]]; draft.width = r.x; draft.height = r.y; }
                Draw();
            });
        }
        foreach (string action in new[] { "Dash", "Interact", "FirstPerson", "Blink" })
            foreach (string slot in new[] { "PrimaryKey", "SecondaryKey" })
            {
                var button = transform.Find("OptionsPanel/KeyBindings/" + action + "/" + slot).GetComponent<Button>();
                int index = bindingButtons.Count; bindingButtons.Add(button);
                button.onClick.AddListener(() => BeginCapture(index));
            }
        resetButton.onClick.AddListener(() => { if (captureSlot < 0) { LoadDraft(GameOptions.Defaults()); statusText.text = "初期値に戻しました（未適用）"; } });
        cancelButton.onClick.AddListener(() => { if (captureSlot < 0) Close(); });
        applyButton.onClick.AddListener(Apply);
        initialized = true; LoadDraft(GameOptions.Current); SetVisible(false);
        gameObject.AddComponent<OptionsControlGuide>().Initialize(japaneseFont != null ? japaneseFont : previewFont);
    }
    private void AddResolution(int width, int height)
    {
        var r = new Vector2Int(width, height);
        if (width >= 640 && height >= 480 && !resolutions.Contains(r)) resolutions.Add(r);
    }
    // 古いPrefabを展開して配置していた場合にも、新しい項目を同じ部品から補う。
    private void EnsureSensitivityRow()
    {
        var source = ranges[1].slider.transform.parent;
        source.Find("Label").GetComponent<Text>().text = "三人称マウス感度";
        if (firstPersonSensitivity == null || firstPersonSensitivity.slider == null)
        {
            var row = Instantiate(source.gameObject, source.parent).transform;
            row.name = "FirstPersonSensitivity"; row.SetSiblingIndex(source.GetSiblingIndex() + 1);
            row.Find("Label").GetComponent<Text>().text = "一人称マウス感度";
            firstPersonSensitivity = new RangeRow { slider = row.Find("Slider").GetComponent<Slider>(), valueText = row.Find("Value").GetComponent<Text>(), defaultValue = 30 };
        }
        var parent = source.parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var rect = parent.GetChild(i) as RectTransform;
            if (rect != null) rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -i * 42f);
        }
    }
    private void LoadDraft(GameOptions.Data data)
    {
        draft = data.Copy();
        int index = resolutions.FindIndex(r => r.x == draft.width && r.y == draft.height);
        draft.choices[1] = Mathf.Max(0, index);
        var resolution = resolutions[draft.choices[1]]; draft.width = resolution.x; draft.height = resolution.y;
        Draw();
    }
    private void Draw()
    {
        firstPersonSensitivity.slider.SetValueWithoutNotify(draft.firstPersonSensitivity);
        firstPersonSensitivity.valueText.text = Mathf.RoundToInt(draft.firstPersonSensitivity).ToString();
        for (int i = 0; i < ranges.Length; i++)
        {
            ranges[i].slider.SetValueWithoutNotify(draft.ranges[i]);
            ranges[i].valueText.text = Mathf.RoundToInt(draft.ranges[i]).ToString();
        }
        for (int i = 0; i < choices.Length; i++) choices[i].valueText.text = "‹ " + choices[i].options[draft.choices[i]] + " ›";
        for (int i = 0; i < bindingButtons.Count; i++)
            bindingButtons[i].GetComponentInChildren<Text>().text = captureSlot == i ? "入力待ち…" : GameOptions.KeyLabel(draft.keys[i]);
    }
    private void SetVisible(bool value)
    {
        canvas.enabled = value; raycaster.enabled = value;
        transform.Find("DimBackground").gameObject.SetActive(value);
        transform.Find("OptionsPanel").gameObject.SetActive(value);
    }
    private void Open()
    {
        LoadDraft(GameOptions.Current);
        previousLock = Cursor.lockState; previousCursor = Cursor.visible;
        previousTimeScale = Time.timeScale; previousAudioPause = AudioListener.pause;
        GameOptions.SetMenu(true); open = true; Time.timeScale = 0; AudioListener.pause = true;
        GameOptionsRuntime.RefreshInputState();
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        if (EventSystem.current == null) ownedEventSystem = new GameObject("Options EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        SetVisible(true); EventSystem.current.SetSelectedGameObject(ranges[0].slider.gameObject);
        statusText.text = "Q / Esc：閉じる　Enter：決定　キー変更中はDeleteで解除";
    }
    private void Close()
    {
        if (!open) return;
        // 表示部品の無効化より先に停止状態を解除し、操作の復帰をUIのイベントに依存させない。
        open = false; GameOptions.SetMenu(false);
        Time.timeScale = previousTimeScale; AudioListener.pause = previousAudioPause;
        Cursor.lockState = previousLock; Cursor.visible = previousCursor;
        GameOptionsRuntime.RefreshInputState();
        captureSlot = -1; SetControls(true); SetVisible(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }
    // UpdateはTime.timeScaleが0でも呼ばれるため、一時停止中も開閉とキー変更を受け付ける。
    private void Update()
    {
        if (fontDirty) { fontDirty = false; RefreshFont(); }
        if (!Application.isPlaying || !initialized) return;
        if (captureSlot >= 0) { Capture(); return; }
        if (Input.GetKeyDown(KeyCode.Q) || (open && Input.GetKeyDown(KeyCode.Escape)))
        { if (open) Close(); else Open(); return; }
    }
    private void BeginCapture(int index)
    {
        if (captureSlot >= 0) return;
        captureSlot = index; captureFrame = Time.frameCount; SetControls(false); Draw();
        statusText.text = "キーかマウスを入力／Escで中止／Deleteで解除（Q・WASDは固定）";
    }
    private void SetControls(bool enabled)
    {
        foreach (var control in GetComponentsInChildren<Selectable>(true)) control.interactable = enabled;
    }
    private void Capture()
    {
        if (Time.frameCount <= captureFrame) return;
        if (Input.GetKeyDown(KeyCode.Escape)) { FinishCapture(); return; }
        foreach (KeyCode key in allKeys)
        {
            if (key == KeyCode.None || !Input.GetKeyDown(key)) continue;
            if (GameOptions.Reserved(key) && key != KeyCode.Delete) { statusText.text = "そのキーは固定操作用です。別のキーを選んでください"; return; }
            KeyCode value = key == KeyCode.Delete ? KeyCode.None : key;
            if (!GameOptions.CanAssign(draft.keys, captureSlot, value, out string error)) { statusText.text = error; return; }
            draft.keys[captureSlot] = value; FinishCapture(); return;
        }
    }
    private void FinishCapture()
    {
        captureSlot = -1; Draw(); statusText.text = "キー割り当てを編集しました（未適用）";
        // 捕捉したクリックが別ボタンを押さないよう、次フレームまで無効のままにする。
        StartCoroutine(EnableNextFrame());
    }
    private IEnumerator EnableNextFrame() { yield return null; SetControls(true); }
    private void Apply()
    {
        if (captureSlot >= 0) return;
        for (int i = 0; i < 4; i++) if (draft.keys[i * 2] == KeyCode.None && draft.keys[i * 2 + 1] == KeyCode.None)
        { statusText.text = "各操作に最低1つのキーを設定してください"; return; }
        if (!GameOptions.HasIndependentPress(draft.keys, GameOptions.Action.Interact) ||
            !GameOptions.HasIndependentPress(draft.keys, GameOptions.Action.Blink))
        { statusText.text = "調べる・瞬きには、視点切替と重ならないキーも1つ設定してください"; return; }
        // 通常の設定も画面設定も一度の適用で確定する。閉じる操作を待たず通知する。
        GameOptionsRuntime.EnsureAvailable();
        GameOptions.Commit(draft); statusText.text = "設定を適用・保存しました";
    }
    private void RefreshFont()
    {
        Font font = japaneseFont;
        if (font == null)
        {
            if (previewFont == null) previewFont = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo", "MS Gothic", "Hiragino Sans", "Noto Sans CJK JP" }, 24);
            font = previewFont;
        }
        if (font != null) foreach (Text text in GetComponentsInChildren<Text>(true)) text.font = font;
    }
    private void OnDisable() { if (Application.isPlaying && initialized) Close(); }
    private void OnDestroy()
    {
        if (ownedEventSystem != null) Destroy(ownedEventSystem);
        if (previewFont != null) { if (Application.isPlaying) Destroy(previewFont); else DestroyImmediate(previewFont); }
    }
}

