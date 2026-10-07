using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Unity.Cinemachine;

// WakeUpIntroControllerの順次再生を基に、調査や範囲イベントから呼べる演出にしたもの。
// 起床専用の天井向き・布団・三人称への強制復帰を持たず、実行時の位置から動作する。
public sealed class EventSequenceController : MonoBehaviour
{
    public enum ViewMode { FirstPerson, ThirdPerson, KeepCurrent }
    // 削除したCamera=1、PlayerMove=4は読み込み時に移行する。他の保存値は変更しない。
    public enum StepType { Wait = 0, ObjectTransform = 2, RendererVisibility = 3, PlayAudio = 5, StopAudio = 6, Fade = 7, Event = 8, CameraLook = 9 }
    public enum MovementStyle { Walk, Run }
    public enum LookDirection { LevelCurrentView, PlayerForward, DirectionMarker, NumericAngles, LookAtTarget, KeepCurrent, LookAtPlayer }
    public enum ThirdPersonPositionMode { KeepCurrent, PlayerRelative, FollowCamera }

    [Serializable]
    public sealed class Step
    {
        public string name = "ステップ";
        public StepType type;
        [Min(0)] public float delay;
        [Tooltip("動作時間。表示・音声開始・イベントは即時実行後、この秒数待機します。")]
        [Min(0)] public float duration = 1f;
        public AnimationCurve easing = AnimationCurve.Linear(0, 0, 1, 1);
        public ViewMode cameraMode;
        [Tooltip("三人称専用。Keep Current：現在の位置制御を維持。Player Relative：プレイヤー基準の位置へ移動。Follow Camera：通常の位置追従へ戻す。")]
        public ThirdPersonPositionMode thirdPersonPositionMode;
        [Tooltip("プレイヤーの足元からの距離。X：右、Y：上、Z：前（負なら背後）。キャラの水平な向きを基準に追従し、拡大率の影響は受けません。")]
        public Vector3 thirdPersonPosition = new Vector3(0, 1.5f, -3f);
        [Tooltip("水平化・キャラ正面・目印の向き・数値角度・対象を中央へ・視線変更なし・プレイヤーを中央へ、から選択します。")]
        public LookDirection lookDirection;
        [Tooltip("Direction Marker：目印のY回転。Look At Target：対象の描画範囲の中心（メッシュがなければTransformの位置）を見ます。")]
        public Transform lookDirectionTarget;
        [Tooltip("Look At Target / Look At Player専用。描画範囲の中心に加える対象のローカル座標基準の補正。中央に映す場合は0です。")]
        public Vector3 lookTargetOffset;
        [Tooltip("Numeric Angles専用。オン：開始時からの追加回転。オフ：ワールド基準の到着角度。")]
        public bool lookRelative;
        [Tooltip("度数で指定。X：上下、Y：左右、Z：傾き。相対ではYをワールド上方向、X/Zを開始視点のローカル軸基準で回します。")]
        public Vector3 lookRotation;

        [Tooltip("Object Transform / Renderer Visibilityの対象。プレイヤーの移動はCamera LookのMove Playerを使います。")]
        public Transform target;
        [Tooltip("オン：開始姿勢からの相対値。オフ：ワールド座標・ワールド角度・ローカル拡大率を直接指定。")]
        public bool relative = true;
        public bool move;
        public Vector3 position;
        public bool rotate;
        public Vector3 rotation;
        public bool scale;
        public Vector3 scaleValue = Vector3.one;
        [Tooltip("ドアの蝶番など。回転中心のワールド位置をステップ開始時に保存します。")]
        public Transform rotationPivot;
        [Tooltip("MeshRendererとSkinnedMeshRendererだけを変更。Colliderやスクリプトは停止しません。")]
        public bool visible;
        public bool includeChildren = true;

        [Tooltip("Camera Lookで視線の変更と同時にプレイヤーを移動します。オフならカメラ操作だけです。")]
        public bool movePlayer;
        public MovementStyle movementStyle;
        [Tooltip("到着先の足元位置。空欄なら下の移動量を使います。プレイヤーの子には置かないでください。")]
        public Transform playerDestination;
        [Tooltip("オン：移動開始時のワールドY座標を維持してXZだけ移動。オフ：到着地点またはPlayer OffsetのYも反映します。")]
        public bool ignorePlayerY;
        [Tooltip("横・高さ・前への移動量。開始時の水平な視点方向（またはキャラ方向）が基準です。")]
        public Vector3 playerOffset = new Vector3(0, 0, 1);
        public bool useViewDirection = true;
        public bool faceMovement = true;
        [Tooltip("オンなら距離÷歩行／走行速度をステップ全体（視線と移動）の時間にします。オフなら両方ともDuration秒で完了します。")]
        public bool useMovementSpeed = true;

        public AudioSource audioSource;
        [Tooltip("空欄ならAudioSourceのClipを使用。Play One ShotではなくClipを置き換えて再生します。")]
        public AudioClip audioClip;
        public bool loop;
        [Tooltip("非ループ音声の終了を待ちます。Duration以上、かつ音声終了まで待機します。")]
        public bool waitForAudio;
        [Range(0, 1)] public float fadeAlpha = 1f;
        public UnityEvent onStarted = new UnityEvent();
        public UnityEvent onCompleted = new UnityEvent();

        // 旧データの時間・目印・イベント通知を保ち、重複した種類だけを新しい形に移す。
        public void MigrateLegacyType()
        {
            if ((int)type != 1 && (int)type != 4) return;
            movePlayer = (int)type == 4;
            if (movePlayer) cameraMode = ViewMode.KeepCurrent;
            lookDirection = LookDirection.KeepCurrent;
            type = StepType.CameraLook;
        }
    }

    [Header("操作キャラと既存カメラ")]
    [SerializeField] private PlayerMovement player;
    [SerializeField] private CameraSwitch cameraSwitch;
    [SerializeField] private CinemachineBrain brain;
    [SerializeField] private ViewMode initialView = ViewMode.FirstPerson;
    [Tooltip("画面全体を覆う黒いUI Image。起動時に透明にし、Fade開始時に自動で表示を有効にします。Fadeを使わない場合は不要です。")]
    [SerializeField] private Image fadeImage;
    [Tooltip("通常はオン。シーン開始時のImageのAlphaを0にします。最初から暗転させる演出ではオフにします。")]
    [SerializeField] private bool hideFadeOnAwake = true;
    [Tooltip("演出と競合する追加の処理だけを登録。PlayerMovement・まばたき入力は自動停止します。")]
    [SerializeField] private Behaviour[] pauseDuringEvent = new Behaviour[0];

    [Header("開始・終了")]
    [SerializeField] private bool playOnStart;
    [SerializeField] private bool playOnce = true;
    [Tooltip("完了後も操作と最後の視点を固定。シーン遷移やReleaseControl()まで維持します。")]
    [SerializeField] private bool holdControlOnCompletion;
    [Tooltip("オンならこの演出が再生した音源を正常終了時にも停止。中断時は常に停止します。")]
    [SerializeField] private bool stopAudioOnCompletion;
    [Header("任意の開始条件")]
    [SerializeField] private bool requireSuit;
    [SerializeField] private CharacterOutfitController outfit;
    [Tooltip("リュック所持など外部の条件。未使用ならオフ。所持処理からSetExternalCondition(true)を呼びます。")]
    [SerializeField] private bool requireExternalCondition;
    [SerializeField] private bool externalConditionMet;
    [Header("上から順に再生")]
    [SerializeField] private List<Step> steps = new List<Step>();
    [SerializeField] private UnityEvent onStarted = new UnityEvent();
    [SerializeField] private UnityEvent onCompleted = new UnityEvent();
    [SerializeField] private UnityEvent onCancelled = new UnityEvent();
    [SerializeField] private UnityEvent onConditionsNotMet = new UnityEvent();

    private static EventSequenceController activeSequence;
    public static bool IsAnyPlaying => activeSequence != null;
    public bool IsPlaying { get; private set; }
    public bool HasCompleted { get; private set; }
    public bool IsCompletedOnce => playOnce && HasCompleted;
    private bool sequenceFinished;
    private readonly Dictionary<Behaviour, bool> savedBehaviours = new Dictionary<Behaviour, bool>();
    private readonly HashSet<AudioSource> startedAudio = new HashSet<AudioSource>();
    private readonly HashSet<AudioSource> menuPausedAudio = new HashSet<AudioSource>();
    private readonly Dictionary<CinemachineCamera, EventCameraLookOverride> lookOverrides = new Dictionary<CinemachineCamera, EventCameraLookOverride>();
    private CharacterController characterController;
    private bool controllerEnabled;
    private Animator animator;
    private readonly Dictionary<Animator, bool> savedRootMotion = new Dictionary<Animator, bool>();
    private float animationSpeed;
    private Color savedFadeColor;
    private CinemachineBlendDefinition savedBlend;
    private CinemachineBlenderSettings savedCustomBlends;
    private bool acquiredControl;
    private bool restoringControl;
    private int playbackVersion;
    [Header("実行状況（確認用）")]
    [SerializeField, TextArea] private string playbackStatus = "待機中";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() => activeSequence = null;

    // Imageのチェックを外して隠す必要がないよう、最初の描画前に透明にする。
    // Raycastを無効にし、透明なパネルがオプション画面のクリックを遮らないようにする。
    private void Awake()
    {
        MigrateLegacySteps();
        if (fadeImage == null) return;
        if (hideFadeOnAwake)
        {
            Color color = fadeImage.color;
            color.a = 0;
            fadeImage.color = color;
        }
        fadeImage.raycastTarget = false;
    }

    private void Start() { if (playOnStart) Play(); }
    private void OnValidate() => MigrateLegacySteps();

    private void MigrateLegacySteps()
    {
        if (steps != null) foreach (Step step in steps) step?.MigrateLegacyType();
    }
    // UnityEventから引数なしで選べる入口。条件失敗時は再生済みにしない。
    public void Play() => TryPlay();
    public void SetExternalCondition(bool value) => externalConditionMet = value;

    public bool TryPlay()
    {
        MigrateLegacySteps();
        if (!isActiveAndEnabled || restoringControl || IsAnyPlaying || IsCompletedOnce || GameOptions.Blocked ||
            DoorInteraction.IsDoorBusy || (cameraSwitch != null && (cameraSwitch.IntroActive || cameraSwitch.EventActive))) return false;
        if (!ValidateSetup()) return false;
        if ((requireSuit && !outfit.IsWearingSuit) || (requireExternalCondition && !externalConditionMet))
        {
            playbackStatus = "開始条件が不足しています";
            onConditionsNotMet.Invoke();
            return false;
        }
        activeSequence = this;
        IsPlaying = true;
        sequenceFinished = false;
        playbackStatus = "開始処理中";
        int version = ++playbackVersion;
        try
        {
            AcquireControl();
            StartCoroutine(PlaySequence(version));
        }
        catch
        {
            RestoreControl(false);
            throw;
        }
        return true;
    }

    // 通常の移動・入力と競合させず、実際のキャラを既存カメラが追う構成を維持する。
    // CharacterControllerとRoot Motionは演出中だけ停止し、指定経路を正確に通す。
    private void AcquireControl()
    {
        savedBlend = brain.DefaultBlend;
        savedCustomBlends = brain.CustomBlends;
        if (fadeImage != null) savedFadeColor = fadeImage.color;
        characterController = player.controller != null ? player.controller : player.GetComponent<CharacterController>();
        controllerEnabled = characterController != null && characterController.enabled;
        acquiredControl = true;
        Pause(player);
        foreach (EyeBlink blink in FindObjectsByType<EyeBlink>(FindObjectsSortMode.None)) Pause(blink);
        foreach (Behaviour item in pauseDuringEvent) Pause(item);
        if (characterController != null) characterController.enabled = false;
        SetSpeed(0);
        brain.CustomBlends = null;
        // 体の表示とカメラがずれないよう、初期視点はカットで確定する。
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0);
        cameraSwitch.BeginEvent(initialView == ViewMode.KeepCurrent ? cameraSwitch.FirstPersonMode : initialView == ViewMode.FirstPerson);
    }

    private void Pause(Behaviour item)
    {
        if (item == null || item == this || item == cameraSwitch || item == brain ||
            item is Animator || item is CinemachineCamera || item is GameOptionsRuntime || item is OptionsMenuView) return;
        if (!savedBehaviours.ContainsKey(item)) { savedBehaviours.Add(item, item.enabled); item.enabled = false; }
    }

    // 完了通知から別の再生が始まっても、古いCoroutineが新しい演出を終了させない。
    private bool IsCurrent(int version) => IsPlaying && playbackVersion == version;

    // ネストしたCoroutineで各動作の終了を待つ。無効化・中断時も操作ロックを解放する。
    private IEnumerator PlaySequence(int version)
    {
        try
        {
            onStarted.Invoke();
            if (!IsCurrent(version)) yield break;
            for (int i = 0; i < steps.Count; i++)
            {
                Step step = steps[i];
                playbackStatus = $"{i + 1}/{steps.Count}：{step.name}（開始前の待機：{step.delay}秒）";
                yield return WaitSeconds(step.delay);
                if (!IsCurrent(version)) yield break;
                playbackStatus = $"{i + 1}/{steps.Count}：{step.name}（実行中）";
                step.onStarted.Invoke();
                if (!IsCurrent(version)) yield break;
                yield return ExecuteStep(step);
                if (!IsCurrent(version)) yield break;
                step.onCompleted.Invoke();
                if (!IsCurrent(version)) yield break;
            }
            // 次フレームまで待てばBrainがCutを反映できる。WaitForEndOfFrameはEditorで
            // Gameビューから離れると止まるため、完了処理の待機には使わない。
            yield return null;
            if (!IsCurrent(version)) yield break;
            sequenceFinished = true;
            HasCompleted = true;
            playbackStatus = holdControlOnCompletion ? "完了：操作を保持中（ReleaseControlで解除）" : "完了";
            if (stopAudioOnCompletion)
                foreach (AudioSource source in startedAudio) if (source != null) source.Stop();
            onCompleted.Invoke();
            if (IsCurrent(version) && !holdControlOnCompletion) ReleaseControl();
        }
        finally
        {
            if (IsCurrent(version) && !sequenceFinished) RestoreControl(false);
        }
    }

    private IEnumerator ExecuteStep(Step step)
    {
        switch (step.type)
        {
            case StepType.CameraLook:
                yield return CameraAndPlayerMotion(step);
                yield break;
            case StepType.ObjectTransform:
                yield return TransformObject(step);
                yield break;
            case StepType.RendererVisibility:
                if (step.target == null) { Cancel(); yield break; }
                Renderer[] renderers = step.includeChildren ? step.target.GetComponentsInChildren<Renderer>(true) : step.target.GetComponents<Renderer>();
                foreach (Renderer item in renderers)
                    if (item is MeshRenderer || item is SkinnedMeshRenderer) item.enabled = step.visible;
                break;
            case StepType.PlayAudio:
                if (step.audioSource == null) { Cancel(); yield break; }
                if (step.audioClip != null) step.audioSource.clip = step.audioClip;
                step.audioSource.loop = step.loop;
                step.audioSource.Play();
                startedAudio.Add(step.audioSource);
                break;
            case StepType.StopAudio:
                if (step.audioSource != null) step.audioSource.Stop();
                startedAudio.Remove(step.audioSource);
                break;
            case StepType.Fade:
                yield return Fade(step);
                yield break;
        }
        yield return WaitSeconds(step.duration);
        if (step.type == StepType.PlayAudio && step.waitForAudio)
        {
            // Pitchが再生中に変更されても永久待機しないよう、終了待ちに上限を設ける。
            float timeout = step.audioSource != null && step.audioSource.clip != null
                ? step.audioSource.clip.length / Mathf.Max(0.01f, Mathf.Abs(step.audioSource.pitch)) + 1f : 0;
            float elapsed = 0;
            while (IsPlaying && step.audioSource != null && elapsed < timeout)
            {
                if (!Paused && !step.audioSource.isPlaying) break;
                yield return null;
                if (!Paused) elapsed += Time.deltaTime;
            }
        }
    }

    // 両視点でカメラの向きを補間する。三人称のComposerはTransformを書き戻すため、
    // Cinemachineの最終段階で向きを指定する拡張を使い、位置追従と視線の演出を両立する。
    private IEnumerator CameraAndPlayerMotion(Step step)
    {
        if (cameraSwitch == null || player == null ||
            ((step.lookDirection == LookDirection.DirectionMarker || step.lookDirection == LookDirection.LookAtTarget) && step.lookDirectionTarget == null))
        { Cancel(); yield break; }

        bool firstPerson = step.cameraMode == ViewMode.KeepCurrent ? cameraSwitch.FirstPersonMode : step.cameraMode == ViewMode.FirstPerson;
        CinemachineCamera view = firstPerson ? cameraSwitch.FirstPersonCamera : cameraSwitch.ThirdPersonCamera;
        if (view == null) { Cancel(); yield break; }
        cameraSwitch.SetEventView(firstPerson);
        bool changeLook = step.lookDirection != LookDirection.KeepCurrent;
        bool changePosition = !firstPerson && step.thirdPersonPositionMode != ThirdPersonPositionMode.KeepCurrent;
        lookOverrides.TryGetValue(view, out EventCameraLookOverride lookOverride);
        if ((changeLook || changePosition) && lookOverride == null)
        {
            lookOverride = view.gameObject.AddComponent<EventCameraLookOverride>();
            lookOverrides[view] = lookOverride;
        }
        Quaternion from = lookOverride != null && lookOverride.HasRotation ? lookOverride.Rotation : view.transform.rotation;
        if (changePosition)
            lookOverride.BeginPosition(player.transform, view.State.GetFinalPosition(),
                step.thirdPersonPosition, step.thirdPersonPositionMode == ThirdPersonPositionMode.FollowCamera);

        // 進行方向は開始時に確定する。注視でカメラが回っても移動経路は曲がらない。
        Vector3 playerFrom = player.transform.position;
        Quaternion playerRotationFrom = player.transform.rotation;
        Quaternion heading = Quaternion.Euler(0, step.useViewDirection ? from.eulerAngles.y : playerRotationFrom.eulerAngles.y, 0);
        Vector3 playerTo = !step.movePlayer ? playerFrom : step.playerDestination != null
            ? step.playerDestination.position : playerFrom + heading * step.playerOffset;
        // 目印・移動量のどちらでも高さを固定し、速度から求める所要時間もXZの距離に揃える。
        if (step.ignorePlayerY) playerTo.y = playerFrom.y;
        Vector3 facing = Vector3.ProjectOnPlane(playerTo - playerFrom, Vector3.up);
        Quaternion playerRotationTo = step.movePlayer && step.faceMovement && facing.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(facing) : playerRotationFrom;
        float speed = step.movementStyle == MovementStyle.Run ? player.dashSpeed : player.speed;
        float duration = step.movePlayer && step.useMovementSpeed
            ? Vector3.Distance(playerFrom, playerTo) / Mathf.Max(0.01f, speed) : step.duration;

        float yaw = from.eulerAngles.y;
        if (step.lookDirection == LookDirection.PlayerForward) yaw = playerRotationTo.eulerAngles.y;
        else if (step.lookDirection == LookDirection.DirectionMarker) yaw = step.lookDirectionTarget.eulerAngles.y;
        // 開始時に目標方向を確定し、目印が動いても途中で追いかけ続けないようにする。
        Quaternion to = Quaternion.Euler(0, yaw, 0);
        if (step.lookDirection == LookDirection.NumericAngles) to = Quaternion.Euler(step.lookRotation);
        float elapsed = 0;
        while (IsPlaying)
        {
            if (Paused) { if (step.movePlayer) SetSpeed(0); yield return null; continue; }
            if (view == null || player == null || ((changeLook || changePosition) && lookOverride == null) ||
                (step.lookDirection == LookDirection.LookAtTarget && step.lookDirectionTarget == null))
            { Cancel(); yield break; }
            float t = Progress(elapsed, duration);
            float amount = Ease(step, t);
            // 視線と移動に同じ進捗を使い、開始・終了・メニュー中の一時停止を一致させる。
            if (step.movePlayer)
            {
                player.transform.position = Vector3.Lerp(playerFrom, playerTo, amount);
                if (step.faceMovement) player.transform.rotation = Quaternion.Slerp(playerRotationFrom, playerRotationTo, amount);
                SetSpeed(t < 1 && facing.sqrMagnitude > 0.0001f ? speed : 0);
            }
            if (changePosition) lookOverride.SetPositionProgress(amount);
            if (step.lookDirection == LookDirection.LookAtTarget || step.lookDirection == LookDirection.LookAtPlayer)
            {
                // 実際の描画位置はCinemachineの追従処理後に確定するため、注視角は拡張側で求める。
                lookOverride.SetLookTarget(step.lookDirection == LookDirection.LookAtPlayer ? player.transform : step.lookDirectionTarget,
                    step.lookTargetOffset, from, amount);
            }
            else if (changeLook)
            {
                // 相対回転は毎フレーム角度から計算し、+270度や1周の指示も短い回転に省略しない。
                Quaternion rotation = step.lookDirection == LookDirection.NumericAngles && step.lookRelative
                    ? Quaternion.AngleAxis(step.lookRotation.y * amount, Vector3.up) * from *
                        Quaternion.Euler(step.lookRotation.x * amount, 0, step.lookRotation.z * amount)
                    : Quaternion.Slerp(from, to, amount);
                lookOverride.SetRotation(rotation);
                // 次の時間0のステップと、通常操作への復帰も最終角度を引き継げるよう同期する。
                view.transform.rotation = rotation;
            }
            if (t >= 1) yield break;
            yield return null;
            if (!Paused) elapsed += Time.deltaTime;
        }
    }

    // この演出が追加した拡張だけを解除し、終了・中断後は通常のCinemachine制御に返す。
    private void ReleaseLookOverrides()
    {
        foreach (var entry in lookOverrides)
        {
            if (entry.Value == null) continue;
            entry.Value.enabled = false;
            if (entry.Key != null && entry.Value.HasRotation)
                entry.Key.ForceCameraPosition(entry.Key.transform.position, entry.Value.Rotation);
            Destroy(entry.Value);
        }
        lookOverrides.Clear();
    }

    // 各ステップの開始時に姿勢を取るため、起動時から動いていた物にも相対操作できる。
    // 蝶番の点を中心とする円弧を毎フレーム計算し、ドアの原点が中央でも軸を固定する。
    private IEnumerator TransformObject(Step step)
    {
        if (step.target == null) { Cancel(); yield break; }
        Transform target = step.target;
        Vector3 fromPosition = target.position;
        Quaternion fromRotation = target.rotation;
        Vector3 fromScale = target.localScale;
        Vector3 toPosition = step.relative ? fromPosition + fromRotation * step.position : step.position;
        Quaternion toRotation = step.relative ? fromRotation * Quaternion.Euler(step.rotation) : Quaternion.Euler(step.rotation);
        Vector3 toScale = step.relative ? Vector3.Scale(fromScale, step.scaleValue) : step.scaleValue;
        Vector3 pivot = step.rotationPivot != null ? step.rotationPivot.position : fromPosition;
        bool usePivot = step.rotationPivot != null && step.rotate;
        float elapsed = 0;
        while (IsPlaying)
        {
            if (Paused) { yield return null; continue; }
            if (target == null) { Cancel(); yield break; }
            float t = Progress(elapsed, step.duration);
            float amount = Ease(step, t);
            Quaternion rotation = step.rotate ? Quaternion.Slerp(fromRotation, toRotation, amount) : fromRotation;
            Vector3 position = fromPosition;
            if (usePivot) position = pivot + rotation * Quaternion.Inverse(fromRotation) * (fromPosition - pivot);
            if (step.move) position += (toPosition - fromPosition) * amount;
            if (step.move || usePivot) target.position = position;
            if (step.rotate) target.rotation = rotation;
            if (step.scale) target.localScale = Vector3.Lerp(fromScale, toScale, amount);
            if (t >= 1) yield break;
            yield return null;
            if (!Paused) elapsed += Time.deltaTime;
        }
    }

    // 既存ScreenFadeとは別にAlphaを補間する。完了時の黒を保ち、次のイベントへつなげられる。
    private IEnumerator Fade(Step step)
    {
        if (fadeImage == null) { Cancel(); yield break; }
        // Imageコンポーネントや自身のGameObjectを非表示にしていても暗転だけは再生できる。
        // 親Canvasは勝手に有効化せず、開始前の検証で設定不足として知らせる。
        fadeImage.raycastTarget = false;
        fadeImage.gameObject.SetActive(true);
        fadeImage.enabled = true;
        Color from = fadeImage.color;
        float elapsed = 0;
        while (IsPlaying)
        {
            if (Paused) { yield return null; continue; }
            if (fadeImage == null) { Cancel(); yield break; }
            float t = Progress(elapsed, step.duration);
            fadeImage.color = new Color(from.r, from.g, from.b, Mathf.Lerp(from.a, step.fadeAlpha, Ease(step, t)));
            if (t >= 1) yield break;
            yield return null;
            if (!Paused) elapsed += Time.deltaTime;
        }
    }

    private static bool Paused => GameOptions.MenuOpen || Time.timeScale == 0f;
    private static float Progress(float elapsed, float duration) => duration <= 0 ? 1 : Mathf.Clamp01(elapsed / duration);
    // Inspectorの配列追加では初期化子のLinearが入らず、キー0件のカーブになることがある。
    // 空カーブのEvaluateは0のままなので、未設定時は等速移動にして終点への瞬間移動を防ぐ。
    private static float Ease(Step step, float t)
    {
        if (t >= 1) return 1;
        if (step.easing == null || step.easing.length == 0) return Mathf.Clamp01(t);
        return Mathf.Clamp01(step.easing.Evaluate(t));
    }
    private IEnumerator WaitSeconds(float duration)
    {
        float elapsed = 0;
        while (IsPlaying && (Paused || elapsed < duration))
        {
            yield return null;
            if (!Paused) elapsed += Time.deltaTime;
        }
    }

    // オプション画面中は、この演出で鳴らした音だけを一時停止する。
    private void Update()
    {
        if (!IsPlaying) return;
        if (player == null || cameraSwitch == null || !cameraSwitch.isActiveAndEnabled || brain == null)
        { Cancel(); return; }
        if (Paused)
        {
            foreach (AudioSource source in startedAudio)
                if (source != null && source.isPlaying && menuPausedAudio.Add(source)) source.Pause();
        }
        else ResumeAudio();
    }

    private void ResumeAudio()
    {
        foreach (AudioSource source in menuPausedAudio) if (source != null) source.UnPause();
        menuPausedAudio.Clear();
    }

    // モデルを切り替えた同じフレームで、演出の移動速度とRoot Motion制御を新しいAnimatorへ渡す。
    internal static void NotifyPlayerAnimatorChanged(PlayerMovement movement)
    {
        if (activeSequence != null && activeSequence.IsPlaying && activeSequence.acquiredControl && activeSequence.player == movement)
            activeSequence.SetSpeed(activeSequence.animationSpeed);
    }

    private void SetSpeed(float speed)
    {
        animationSpeed = speed;
        animator = player != null ? player.animator : null;
        if (animator != null && acquiredControl)
        {
            // 途中で複数モデルを使っても、終了・中断時にそれぞれ元の設定へ戻せるよう記録する。
            if (!savedRootMotion.ContainsKey(animator)) savedRootMotion.Add(animator, animator.applyRootMotion);
            if (animator.applyRootMotion) animator.applyRootMotion = false;
        }
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
            if (parameter.name == "Speed" && parameter.type == AnimatorControllerParameterType.Float)
            { animator.SetFloat("Speed", speed); return; }
    }

    // 保持設定で完了した演出の操作を返す。途中ならCancel扱いにし、完了通知を偽装しない。
    public void ReleaseControl()
    {
        if (!IsPlaying) return;
        if (!sequenceFinished) { Cancel(); return; }
        RestoreControl(true);
    }

    // 中断ではオブジェクトとプレイヤーを現在位置に残し、入力・物理・暗転を復帰する。
    public void Cancel()
    {
        if (!IsPlaying) return;
        bool completed = sequenceFinished;
        RestoreControl(completed);
        StopAllCoroutines();
        if (!completed) onCancelled.Invoke();
    }

    private void RestoreControl(bool completed)
    {
        if (!IsPlaying) return;
        restoringControl = true;
        IsPlaying = false;
        playbackStatus = completed ? "完了：操作を復帰しました" : "中断：操作を復帰しました";
        ResumeAudio();
        if (!completed || stopAudioOnCompletion)
            foreach (AudioSource source in startedAudio) if (source != null) source.Stop();
        startedAudio.Clear();
        if (acquiredControl)
        {
            ReleaseLookOverrides();
            SetSpeed(0);
            foreach (var entry in savedRootMotion)
                if (entry.Key != null) entry.Key.applyRootMotion = entry.Value;
            if (characterController != null) characterController.enabled = controllerEnabled;
            if (cameraSwitch != null) cameraSwitch.EndEvent();
            if (brain != null) { brain.DefaultBlend = savedBlend; brain.CustomBlends = savedCustomBlends; }
            if (!completed && fadeImage != null) fadeImage.color = savedFadeColor;
            foreach (var entry in savedBehaviours) if (entry.Key != null) entry.Key.enabled = entry.Value;
        }
        savedBehaviours.Clear();
        savedRootMotion.Clear();
        acquiredControl = false;
        if (activeSequence == this) activeSequence = null;
        restoringControl = false;
    }

    private void OnDisable() => Cancel();

    // 再生前に不足をまとめて報告する。Inspector設定の失敗で途中まで実行しない。
    private bool ValidateSetup()
    {
        var errors = new List<string>();
        if (player == null || !player.gameObject.activeInHierarchy) errors.Add("Playerを指定し、有効にしてください。");
        if (cameraSwitch == null || !cameraSwitch.isActiveAndEnabled ||
            cameraSwitch.FirstPersonCamera == null || !cameraSwitch.FirstPersonCamera.isActiveAndEnabled ||
            cameraSwitch.ThirdPersonCamera == null || !cameraSwitch.ThirdPersonCamera.isActiveAndEnabled)
            errors.Add("Camera Switchと一人称・三人称カメラを指定し、有効にしてください。");
        if (brain == null || !brain.isActiveAndEnabled) errors.Add("Main Cameraの有効なCinemachineBrainを指定してください。");
        if (requireSuit && outfit == null) errors.Add("スーツ条件のOutfitを指定してください。");
        if (steps == null || steps.Count == 0) errors.Add("Stepsを1つ以上追加してください。");
        if (steps != null) foreach (Step step in steps)
        {
            if (step == null) { errors.Add("未設定のステップがあります。"); continue; }
            string label = step.name + "：";
            if (step.type == StepType.CameraLook &&
                (step.lookDirection == LookDirection.DirectionMarker || step.lookDirection == LookDirection.LookAtTarget) && step.lookDirectionTarget == null)
                errors.Add(label + "Look Direction Targetに向きの目印を指定してください。");
            if (step.type == StepType.CameraLook && step.lookDirection == LookDirection.NumericAngles &&
                (float.IsNaN(step.lookRotation.sqrMagnitude) || float.IsInfinity(step.lookRotation.sqrMagnitude)))
                errors.Add(label + "Look Rotationに有限の角度を指定してください。");
            if (step.type == StepType.CameraLook && step.thirdPersonPositionMode == ThirdPersonPositionMode.PlayerRelative &&
                (float.IsNaN(step.thirdPersonPosition.sqrMagnitude) || float.IsInfinity(step.thirdPersonPosition.sqrMagnitude)))
                errors.Add(label + "Third Person Positionに有限の距離を指定してください。");
            if (float.IsNaN(step.duration) || float.IsInfinity(step.duration) || step.duration < 0 ||
                float.IsNaN(step.delay) || float.IsInfinity(step.delay) || step.delay < 0)
                errors.Add(label + "時間を0以上の有限値にしてください。");
            if (step.type == StepType.ObjectTransform || step.type == StepType.RendererVisibility)
            {
                if (step.target == null) errors.Add(label + "Targetを指定してください。");
                else if (player != null && (step.target.IsChildOf(player.transform) || player.transform.IsChildOf(step.target)))
                    errors.Add(label + "プレイヤー本体や見た目はCamera Lookで制御してください。");
            }
            if (step.type == StepType.CameraLook && step.movePlayer && player != null)
            {
                if (step.playerDestination != null && step.playerDestination.IsChildOf(player.transform))
                    errors.Add(label + "到着地点をプレイヤーの外に置いてください。");
                if (step.useMovementSpeed && (step.movementStyle == MovementStyle.Run ? player.dashSpeed : player.speed) <= 0)
                    errors.Add(label + "歩行・走行速度を0より大きくしてください。");
                bool hasSpeed = false;
                if (player.animator != null && player.animator.isActiveAndEnabled && player.animator.runtimeAnimatorController != null)
                    foreach (var parameter in player.animator.parameters)
                        if (parameter.name == "Speed" && parameter.type == AnimatorControllerParameterType.Float) hasSpeed = true;
                if (!hasSpeed) errors.Add(label + "有効なAnimatorとFloatのSpeedパラメーターが必要です。");
            }
            if (step.type == StepType.PlayAudio || step.type == StepType.StopAudio)
            {
                if (step.audioSource == null) errors.Add(label + "Audio Sourceを指定してください。");
                else if (step.type == StepType.PlayAudio && (!step.audioSource.isActiveAndEnabled ||
                    (step.audioClip == null && step.audioSource.clip == null) || step.audioSource.pitch <= 0))
                    errors.Add(label + "有効な音源・Clip・正のPitchを指定してください。");
                if (step.type == StepType.PlayAudio && step.loop && step.waitForAudio)
                    errors.Add(label + "ループ音声は終了待ちできません。");
            }
            if (step.type == StepType.Fade)
            {
                if (fadeImage == null) errors.Add(label + "Fade Imageを指定してください。");
                else
                {
                    Canvas canvas = fadeImage.GetComponentInParent<Canvas>(true);
                    if (canvas == null || !canvas.isActiveAndEnabled ||
                        (fadeImage.transform.parent != null && !fadeImage.transform.parent.gameObject.activeInHierarchy))
                        errors.Add(label + "Fade Imageの親Canvasと親オブジェクトを有効にしてください。Image自体は自動で有効になります。");
                }
            }
        }
        if (errors.Count > 0)
        {
            playbackStatus = "開始できません：" + string.Join(" / ", errors);
            Debug.LogWarning("イベントを開始できません：\n・" + string.Join("\n・", errors), this);
        }
        return errors.Count == 0;
    }
}
