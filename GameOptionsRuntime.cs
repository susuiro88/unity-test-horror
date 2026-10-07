using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

// シーンごとにUIを用意し、描画・音量・Cinemachineの設定を実際のゲームへ反映する。
[DefaultExecutionOrder(-20000)]
public sealed class GameOptionsRuntime : MonoBehaviour
{
    private readonly Dictionary<CinemachineInputAxisController.Reader, Vector2> gains = new();
    private readonly Dictionary<AudioSource, Vector2> volumes = new();
    private readonly Dictionary<UniversalAdditionalCameraData, bool> postProcessing = new();
    private readonly Dictionary<UniversalAdditionalCameraData, LayerMask> volumeMasks = new();
    private readonly Dictionary<CinemachineInputAxisController, bool> pausedAxes = new();
    private ColorAdjustments exposure;
    private VolumeProfile profile;
    private float nextScan;
    private float originalListenerVolume;
    private float initialExposure;
    private int originalVSync, originalFrameRate;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        SceneManager.sceneLoaded += SceneLoaded;
    }
    private static void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureAvailable();
        var menus = FindObjectsByType<OptionsMenuView>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
        if (menus.Length == 0)
        {
            var prefab = Resources.Load<GameObject>("OptionsMenu");
            if (prefab != null) Instantiate(prefab);
        }
        else
        {
            menus[0].gameObject.SetActive(true); menus[0].enabled = true;
            for (int i = 1; i < menus.Length; i++) menus[i].gameObject.SetActive(false);
        }
    }
    // Prefabを後から配置した場合も、保存だけ行われて反映先が存在しない状態を防ぐ。
    public static void EnsureAvailable()
    {
        if (FindFirstObjectByType<GameOptionsRuntime>() == null)
            new GameObject("Game Options Runtime").AddComponent<GameOptionsRuntime>();
    }
    // メニューを閉じた呼び出し内でカメラ入力も復帰させる。
    public static void RefreshInputState()
    {
        var runtime = FindFirstObjectByType<GameOptionsRuntime>();
        if (runtime != null) runtime.ApplyInputState();
    }
    private void Awake()
    {
        originalListenerVolume = AudioListener.volume;
        originalVSync = QualitySettings.vSyncCount; originalFrameRate = Application.targetFrameRate;
        // 最初の描画前にはVolumeのスタックがまだ作成されていない場合がある。
        var originalExposure = VolumeManager.instance.stack?.GetComponent<ColorAdjustments>();
        initialExposure = originalExposure != null ? originalExposure.postExposure.value : 0;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        exposure = profile.Add<ColorAdjustments>(true);
        // 明るさだけを上書きし、既存の彩度や色調補正は維持する。
        exposure.SetAllOverridesTo(false); exposure.postExposure.overrideState = true;
        var volume = gameObject.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 10000; volume.sharedProfile = profile;
        GameOptions.Changed += Apply; Apply();
    }
    private void Update()
    {
        if (Time.unscaledTime >= nextScan) { nextScan = Time.unscaledTime + 1; Scan(); }
        ApplyInputState();
    }
    private void ApplyInputState()
    {
        // 感度をゼロにするだけでは加速・慣性が残るため、メニュー中は入力軸の更新自体を止める。
        if (GameOptions.MenuOpen && pausedAxes.Count == 0)
            foreach (var axis in FindObjectsByType<CinemachineInputAxisController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { pausedAxes[axis] = axis.enabled; axis.enabled = false; }
        if (!GameOptions.MenuOpen && pausedAxes.Count > 0)
        {
            foreach (var axis in pausedAxes) if (axis.Key != null) axis.Key.enabled = axis.Value;
            pausedAxes.Clear();
        }
        foreach (var entry in gains)
        {
            // 停止はenabledだけで管理する。ゲインを0にすると再有効化時に0が基準値として残り得る。
            float multiplier = GameOptions.Current.ranges[1] / 30f;
            entry.Key.Gain = entry.Value.x * multiplier; entry.Key.LegacyGain = entry.Value.y * multiplier;
        }
    }
    // 後から生成された音源も対象にし、元の音量バランスを保ってカテゴリ音量を掛ける。
    private void Scan()
    {
        foreach (var camera in FindObjectsByType<UniversalAdditionalCameraData>(FindObjectsSortMode.None))
        {
            if (camera.GetComponent<Camera>().cameraType != CameraType.Game) continue;
            if (!postProcessing.ContainsKey(camera))
            { postProcessing.Add(camera, camera.renderPostProcessing); volumeMasks.Add(camera, camera.volumeLayerMask); }
            camera.renderPostProcessing = true;
            camera.volumeLayerMask |= 1 << gameObject.layer;
        }
        foreach (var axis in FindObjectsByType<CinemachineInputAxisController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            foreach (var control in axis.Controllers)
                if (control.Input != null && !gains.ContainsKey(control.Input)) gains.Add(control.Input, new Vector2(control.Input.Gain, control.Input.LegacyGain));
        foreach (var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (!volumes.ContainsKey(source)) volumes.Add(source, new Vector2(source.volume, source.volume));
        foreach (var source in new List<AudioSource>(volumes.Keys))
        {
            if (source == null) { volumes.Remove(source); continue; }
            Vector2 state = volumes[source];
            if (!Mathf.Approximately(source.volume, state.y)) state.x = source.volume;
            var channel = source.GetComponent<OptionsAudioChannel>();
            float amount = GameOptions.Current.ranges[channel != null && channel.channel == OptionsAudioChannel.Channel.Music ? 3 : 4] / 100f;
            state.y = state.x * amount; source.volume = state.y; volumes[source] = state;
        }
    }
    private void Apply()
    {
        var data = GameOptions.Current;
        AudioListener.volume = data.ranges[2] / 100f;
        exposure.postExposure.overrideState = !Mathf.Approximately(data.ranges[0], 50);
        exposure.postExposure.value = initialExposure + (data.ranges[0] - 50f) / 25f;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = new[] { 60, 120, 144, -1, 30 }[data.choices[2]];
        // EditorのGameビューサイズは変更しない。ビルドでは実際のウィンドウへ反映する。
        if (!Application.isEditor)
            Screen.SetResolution(data.width, data.height, new[] { FullScreenMode.FullScreenWindow, FullScreenMode.ExclusiveFullScreen, FullScreenMode.Windowed }[data.choices[0]]);
        Scan();
        ApplyInputState();
    }
    private void OnDestroy()
    {
        GameOptions.Changed -= Apply;
        foreach (var item in gains) { item.Key.Gain = item.Value.x; item.Key.LegacyGain = item.Value.y; }
        foreach (var item in volumes) if (item.Key != null) item.Key.volume = item.Value.x;
        AudioListener.volume = originalListenerVolume;
        QualitySettings.vSyncCount = originalVSync; Application.targetFrameRate = originalFrameRate;
        foreach (var axis in pausedAxes) if (axis.Key != null) axis.Key.enabled = axis.Value;
        foreach (var camera in postProcessing) if (camera.Key != null) camera.Key.renderPostProcessing = camera.Value;
        foreach (var camera in volumeMasks) if (camera.Key != null) camera.Key.volumeLayerMask = camera.Value;
        if (profile != null) { foreach (var item in profile.components) if (item != null) Destroy(item); Destroy(profile); }
    }
}
