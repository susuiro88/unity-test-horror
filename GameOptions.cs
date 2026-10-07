using System;
using System.Collections.Generic;
using UnityEngine;

// 保存形式と入力判定をまとめ、どの操作スクリプトも同じ割り当てを参照する。
public static class GameOptions
{
    public enum Action { Dash, Interact, Observe, Blink }
    [Serializable]
    public sealed class Data
    {
        public int version = 1;
        public float[] ranges = { 50, 30, 80, 70, 80 };
        // 既存の配列の順序を変えず、保存済みの音量やキー設定を維持する。
        public float firstPersonSensitivity = 30f;
        public int[] choices = new int[7];
        public int width = 1920, height = 1080;
        public KeyCode[] keys = { KeyCode.LeftShift, KeyCode.None, KeyCode.E, KeyCode.Mouse0,
            KeyCode.Mouse1, KeyCode.None, KeyCode.Mouse0, KeyCode.None };
        public Data Copy() => JsonUtility.FromJson<Data>(JsonUtility.ToJson(this));
    }
    private const string SaveKey = "Horror.Options.v1";
    public static Data Current { get; private set; } = new Data();
    public static event System.Action Changed;
    public static bool MenuOpen { get; private set; }
    private static int blockedFrame = -1;
    private static int observationFrame = -1;
    private static readonly HashSet<KeyCode> observationPresses = new();
    public static bool Blocked => MenuOpen || blockedFrame == Time.frameCount;

    // ドメインリロードを無効にしたEditorでも前回の静的状態を引き継がない。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Initialize()
    {
        MenuOpen = false; blockedFrame = -1; Changed = null;
        observationFrame = -1; observationPresses.Clear();
        Current = Defaults();
        try
        {
            if (PlayerPrefs.HasKey(SaveKey)) Current = Validate(JsonUtility.FromJson<Data>(PlayerPrefs.GetString(SaveKey)));
        }
        catch (Exception) { Current = Defaults(); }
    }
    public static Data Defaults()
    {
        var data = new Data();
        if (Screen.currentResolution.width > 0)
        { data.width = Screen.currentResolution.width; data.height = Screen.currentResolution.height; }
        return data;
    }
    public static Data Validate(Data data)
    {
        if (data == null || data.version != 1 || data.ranges == null || data.ranges.Length != 5 ||
            data.choices == null || data.choices.Length != 7 || data.keys == null || data.keys.Length != 8) return Defaults();
        int[] counts = { 3, int.MaxValue, 5, 2, 2, 3, 2 };
        for (int i = 0; i < 5; i++) data.ranges[i] = float.IsNaN(data.ranges[i]) || float.IsInfinity(data.ranges[i]) ? new Data().ranges[i] : Mathf.Clamp(data.ranges[i], i == 1 ? 1 : 0, 100);
        // 旧保存データでは未記録（0）の場合もあるため、初期値へ補完する。
        data.firstPersonSensitivity = float.IsNaN(data.firstPersonSensitivity) || float.IsInfinity(data.firstPersonSensitivity) || data.firstPersonSensitivity <= 0
            ? 30f : Mathf.Clamp(data.firstPersonSensitivity, 1f, 100f);
        for (int i = 0; i < 7; i++) data.choices[i] = Mathf.Clamp(data.choices[i], 0, counts[i] - 1);
        for (int i = 0; i < 8; i++) if (!Enum.IsDefined(typeof(KeyCode), data.keys[i]) || Reserved(data.keys[i])) data.keys[i] = KeyCode.None;
        for (int i = 0; i < 4; i++)
            if (data.keys[i * 2] == KeyCode.None && data.keys[i * 2 + 1] == KeyCode.None)
            { data.keys = new Data().keys; break; }
        // 保存データからの復元でも、UIと同じ重複制限を適用する。
        for (int i = 0; i < data.keys.Length; i++)
            if (!CanAssign(data.keys, i, data.keys[i], out _)) { data.keys = new Data().keys; break; }
        if (!HasIndependentPress(data.keys, Action.Interact) || !HasIndependentPress(data.keys, Action.Blink))
            data.keys = new Data().keys;
        if (data.width < 640 || data.height < 480 || data.width > 16384 || data.height > 16384)
        { data.width = Defaults().width; data.height = Defaults().height; }
        return data;
    }
    public static void Commit(Data data)
    {
        Current = Validate(data.Copy());
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Current)); PlayerPrefs.Save(); Changed?.Invoke();
    }
    public static void SetMenu(bool open)
    {
        MenuOpen = open; blockedFrame = Time.frameCount;
        // 閉じた瞬間の決定入力だけを遮断し、次フレームからは押し続けている移動も再開する。
    }
    public static bool Reserved(KeyCode key) => key == KeyCode.Q || key == KeyCode.Escape || key == KeyCode.Delete ||
        key == KeyCode.W || key == KeyCode.A || key == KeyCode.S || key == KeyCode.D;
    public static bool Held(Action action) => Read(action, false);
    public static bool Down(Action action) => Read(action, true);
    // 視点切替に押下を消費されるキーだけでは、調べる・瞬きを実行できなくなる。
    public static bool HasIndependentPress(KeyCode[] keys, Action action)
    {
        for (int i = (int)action * 2; i < (int)action * 2 + 2; i++)
            if (keys[i] != KeyCode.None && keys[i] != keys[4] && keys[i] != keys[5]) return true;
        return false;
    }
    // 1操作に2枠、1キーを共有できる操作は2つまで。同一行への二重登録は許可しない。
    public static bool CanAssign(KeyCode[] keys, int slot, KeyCode key, out string error)
    {
        error = null;
        if (key == KeyCode.None) return true;
        if (Reserved(key)) { error = "そのキーは固定操作用です。別のキーを選んでください"; return false; }
        if (keys[slot ^ 1] == key) { error = "同じ操作のもう一方に登録されています"; return false; }
        int uses = 0;
        for (int i = 0; i < keys.Length; i++) if (i != slot && keys[i] == key) uses++;
        if (uses >= 2) { error = "このキーはすでに2つの操作に設定されています。共有は2つまでです"; return false; }
        return true;
    }
    // 観察ボタンで視点を切り替えた同じ押下を、調べる・瞬きへ重ねて渡さない。
    // キー単位の消費なので、別のキーで同時に操作した場合は妨げない。
    public static void ConsumeObservationPress()
    {
        if (Blocked) return;
        if (observationFrame != Time.frameCount) { observationPresses.Clear(); observationFrame = Time.frameCount; }
        for (int i = 4; i < 6; i++)
            if (Current.keys[i] != KeyCode.None && Input.GetKeyDown(Current.keys[i])) observationPresses.Add(Current.keys[i]);
    }
    private static bool Read(Action action, bool down)
    {
        if (Blocked) return false;
        for (int i = (int)action * 2; i < (int)action * 2 + 2; i++)
        {
            KeyCode key = Current.keys[i];
            if (down && (action == Action.Interact || action == Action.Blink) && observationFrame == Time.frameCount && observationPresses.Contains(key)) continue;
            if (key != KeyCode.None && (down ? Input.GetKeyDown(key) : Input.GetKey(key))) return true;
        }
        return false;
    }
    public static string KeyLabel(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.None: return "未設定";
            case KeyCode.Mouse0: return "左クリック";
            case KeyCode.Mouse1: return "右クリック";
            case KeyCode.Mouse2: return "中央クリック";
            case KeyCode.LeftShift: return "左Shift";
            case KeyCode.RightShift: return "右Shift";
            default: return key.ToString();
        }
    }
    public static string ActionLabel(Action action)
    {
        int i = (int)action * 2; string a = KeyLabel(Current.keys[i]), b = KeyLabel(Current.keys[i + 1]);
        return Current.keys[i] == KeyCode.None ? b : Current.keys[i + 1] == KeyCode.None ? a : a + " / " + b;
    }
}
