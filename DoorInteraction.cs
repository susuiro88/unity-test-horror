using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// 発光対象を調べると、移動・回転・表示切替・音声のステップを順番に実行する。非表示にしない親に付ける。
// 発光判定（10000）の後、まばたき判定（10002）の前に決定入力を処理する。
// この順番にすることで、将来まばたきに同じキーを割り当てても「調べる」を優先する。
[DefaultExecutionOrder(10001)]
public class DoorInteraction : MonoBehaviour
{
    [SerializeField] private InteractableHighlight highlight;
    [Tooltip("実行後に調べる別の対象。服を消した後のハンガーなど。未指定なら元の対象を使います。")]
    [SerializeField] private InteractableHighlight openStateHighlight;
    [Tooltip("オフなら最初の実行だけ。オンなら再調査でステップを逆順に戻します。")]
    [SerializeField] private bool allowClose = true;
    [Tooltip("一度きりの実行後に？を消す場合に指定する、この操作専用の範囲判定。")]
    [SerializeField] private ProximityHintTarget proximityTarget;
    [Tooltip("指定するとMotion Stepsの代わりにイベントを開始します。条件不足なら未実行のまま再調査できます。")]
    [SerializeField] private EventSequenceController eventSequence;

    // 同じ発光対象を両欄に指定した場合は、共通の操作対象として扱う。
    private bool HasSeparateOpenHighlight => openStateHighlight != null && openStateHighlight != highlight;

    public enum StepType { MoveRotate = 0, SetActive = 1, PlayAudio = 2, StopAudio = 3 }
    public enum AudioTiming { OpeningOnly = 0, ClosingOnly = 1, Both = 2 }
    // 既存のシーン設定を維持するため、クラス名と移動用フィールド名は変えない。
    [Serializable]
    public class MotionStep
    {
        [Tooltip("Move Rotate：移動・回転、Set Active：表示切替、Play Audio：音声開始、Stop Audio：音声停止。")]
        public StepType stepType;
        public Transform target;
        [Tooltip("音声ステップ専用。Targetは空欄で構いません。")]
        public AudioSource audioSource;
        [Tooltip("Play Audio専用。空欄ならAudioSourceに設定済みのClipを使用。")]
        public AudioClip audioClip;
        [Tooltip("Play Audio専用。アラームなどを繰り返す場合はオン。")]
        public bool audioLoop;
        [Tooltip("音声専用。Opening Only：最初の調査、Closing Only：戻す調査、Both：両方。再生・停止は自動反転しません。")]
        public AudioTiming audioTiming;
        [Tooltip("Set Active専用。オンで表示、オフで非表示。戻すときはステップ直前の状態に戻します。")]
        public bool activeWhenOpen;
        [Tooltip("回転中心の目印。蝶番位置に置いた空オブジェクトを指定します。未指定ならTargetの原点。位置だけを使用し、向きは使用しません。")]
        public Transform rotationPivot;
        [Tooltip("このステップ開始時からの移動量。対象の親の座標基準です。")]
        public Vector3 positionOffset;
        [Tooltip("このステップ開始時からのローカル回転量（度）。")]
        public Vector3 rotationOffset;
        [Tooltip("Move Rotate専用の実行時間。Set ActiveはDelay後に即時切替します。")]
        [Min(0f)] public float openDuration = 1f;
        [Tooltip("Move Rotate専用の復帰時間。Set ActiveはDelay後に即時復帰します。")]
        [Min(0f)] public float closeDuration = 1f;
        [Tooltip("各ステップの動作開始前に待つ秒数。開閉の両方に適用します。")]
        [Min(0f)] public float delay;
    }

    [Tooltip("開くときは上から順番、閉じるときは逆順に実行します。")]
    [SerializeField] private List<MotionStep> motionSteps = new List<MotionStep>();

    // 旧バージョンのシーン・Prefabの設定を失わないよう、以前のフィールドを移行用に残す。
    [SerializeField, HideInInspector] private Transform movingObject;
    [SerializeField, HideInInspector] private Vector3 openPositionOffset = new Vector3(1f, 0f, 0f);
    [SerializeField, HideInInspector] private Vector3 openRotationOffset;
    [SerializeField, HideInInspector] private float openDuration = 1f;
    [SerializeField, HideInInspector] private float closeDuration = 1f;
    [Tooltip("オフなら動作中はキャラの移動を禁止し、一人称に固定します。")]
    [SerializeField] private bool allowPlayerMovement = true;
    // 旧音声リストは読み取り用に残し、InspectorではMotion Stepsへ一本化する。
    [SerializeField, HideInInspector] private List<InteractionAudioAction> openingAudio = new List<InteractionAudioAction>();
    [SerializeField, HideInInspector] private List<InteractionAudioAction> closingAudio = new List<InteractionAudioAction>();
    [Header("開閉イベント")]
    // Inspectorから効果音や演出を接続できるよう、開始と完了を別のイベントにする。
    [SerializeField] private UnityEvent onOpeningStarted = new UnityEvent();
    [SerializeField] private UnityEvent onOpened = new UnityEvent();
    [SerializeField] private UnityEvent onClosingStarted = new UnityEvent();
    [SerializeField] private UnityEvent onClosed = new UnityEvent();

    // 1回の決定入力で複数の対象が動いたり、まばたきまで起きたりするのを防ぐ。
    private static DoorInteraction activeDoor;
    private static int consumedFrame = -1;
    // キャラ側の移動・視点処理が参照する。制限するのは開閉が進行している間だけ。
    public static bool BlocksPlayerMovement => activeDoor != null && !activeDoor.allowPlayerMovement;
    public static bool IsDoorBusy => activeDoor != null;
    // 名前は既存の参照用に残すが、マウスに限らず決定入力を処理済みのフレームを表す。
    // EyeBlinkが後から確認するため、即時完了するスーツの表示切替でも瞬きを抑止できる。
    public static bool ConsumedClick => consumedFrame == Time.frameCount;
    // IsOpenは最後に完了した状態を保持する。途中で中断したときの復帰先にも使う。
    public bool IsOpen { get; private set; }
    public bool IsMoving { get; private set; }

    // 実行中のInspector変更で復帰位置が変わらないよう、開始・終了姿勢と時間を保存する。
    private class PreparedStep
    {
        public StepType stepType;
        public InteractionAudioAction audio;
        public AudioTiming audioTiming;
        public bool startActive, endActive;
        public Transform target;
        public Vector3 startPosition, endPosition;
        public Quaternion startRotation, endRotation;
        public Vector3 pivotOffset, positionOffset;
        public float openDuration, closeDuration, delay;
    }

    private readonly List<PreparedStep> preparedSteps = new List<PreparedStep>();
    private readonly HashSet<InteractableHighlight> linkedHighlights = new HashSet<InteractableHighlight>();
    private float elapsed;
    private bool opening;
    private int stepIndex;

    // 旧設定がある場合だけ1要素のリストへ移行する。空の対象はエラーにせず実行対象から外す。
    private void MigrateLegacySettings()
    {
        if (motionSteps == null) motionSteps = new List<MotionStep>();
        if (movingObject != null && motionSteps.Count == 0)
        {
            motionSteps.Add(new MotionStep
            {
                target = movingObject,
                positionOffset = openPositionOffset,
                rotationOffset = openRotationOffset,
                openDuration = openDuration,
                closeDuration = closeDuration
            });
        }
        movingObject = null;
        // 開始音は先頭へ、戻すときの開始音は末尾へ移す。逆順実行でも旧リストの音の順序を保つ。
        if (openingAudio != null)
        {
            int insertion = 0;
            foreach (var action in openingAudio)
                if (action != null) motionSteps.Insert(insertion++, ConvertAudioStep(action, AudioTiming.OpeningOnly));
            openingAudio.Clear();
        }
        if (closingAudio != null)
        {
            for (int i = closingAudio.Count - 1; i >= 0; i--)
                if (closingAudio[i] != null) motionSteps.Add(ConvertAudioStep(closingAudio[i], AudioTiming.ClosingOnly));
            closingAudio.Clear();
        }
    }

    // 元のClip・ループ・音源参照を維持する。旧リストを空にするため繰り返し検証しても増殖しない。
    private static MotionStep ConvertAudioStep(InteractionAudioAction action, AudioTiming timing)
    {
        return new MotionStep
        {
            stepType = action.operation == InteractionAudioAction.Operation.Play ? StepType.PlayAudio : StepType.StopAudio,
            audioSource = action.source, audioClip = action.clip, audioLoop = action.loop,
            audioTiming = timing, delay = 0f
        };
    }

    // Inspectorへ旧形式のデータが読み込まれたときも、動作リストとして編集できるようにする。
    private void OnValidate()
    {
        MigrateLegacySettings();
    }
    // Domain Reloadを無効にしたUnityの再生でも、前回のstatic変数が残らないよう初期化する。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInputState()
    {
        activeDoor = null;
        consumedFrame = -1;
    }

    // Unityがインスタンスを初期化するときに参照を確認し、閉じた状態の基準を保存する。
    private void Awake()
    {
        if (highlight == null)
            highlight = GetComponent<InteractableHighlight>();
        MigrateLegacySettings();
        if (highlight == null)
        {
            Debug.LogWarning("DoorInteraction: Highlightを指定してください。", this);
            enabled = false;
            return;
        }

        // 同じ対象を複数回動かす場合は、前のステップの終点から次の動作を積み上げる。
        var lastSteps = new Dictionary<Transform, PreparedStep>();
        var lastActiveStates = new Dictionary<Transform, bool>();
        foreach (MotionStep step in motionSteps)
        {
            if (step == null) continue;
            // 音声はTransformを必要としない。既存の移動・表示ステップとは別に準備する。
            if (step.stepType == StepType.PlayAudio || step.stepType == StepType.StopAudio)
            {
                if (step.audioSource == null)
                {
                    Debug.LogWarning("DoorInteraction：音声ステップのAudio Sourceを指定してください。", this);
                    continue;
                }
                preparedSteps.Add(new PreparedStep
                {
                    stepType = step.stepType, audioTiming = step.audioTiming,
                    delay = Mathf.Max(0f, step.delay),
                    audio = new InteractionAudioAction
                    {
                        operation = step.stepType == StepType.PlayAudio ? InteractionAudioAction.Operation.Play : InteractionAudioAction.Operation.Stop,
                        source = step.audioSource, clip = step.audioClip, loop = step.audioLoop
                    }
                });
                continue;
            }
            if (step.target == null) continue;
            // 自分や親を消すとステップ進行が停止するため、操作元は必ず残す。
            if (step.stepType == StepType.SetActive && transform.IsChildOf(step.target))
            {
                Debug.LogWarning("DoorInteraction：Set ActiveのTargetに操作元自身やその親は指定できません。", this);
                preparedSteps.Clear();
                enabled = false;
                return;
            }
            if (step.stepType == StepType.SetActive)
            {
                bool wasActive = lastActiveStates.TryGetValue(step.target, out bool previousActive)
                    ? previousActive : step.target.gameObject.activeSelf;
                preparedSteps.Add(new PreparedStep
                {
                    stepType = step.stepType, target = step.target,
                    startActive = wasActive, endActive = step.activeWhenOpen,
                    delay = Mathf.Max(0f, step.delay)
                });
                lastActiveStates[step.target] = step.activeWhenOpen;
                continue;
            }
            lastSteps.TryGetValue(step.target, out PreparedStep previous);
            Vector3 position = previous != null ? previous.endPosition : step.target.localPosition;
            Quaternion rotation = previous != null ? previous.endRotation : step.target.localRotation;
            // 目印を対象内の点として保存する。子の目印が回転中に動いても軸を再計算しない。
            // localScaleも含めることで、拡大縮小されたモデルでも蝶番の点がずれないようにする。
            Vector3 pivotOffset = step.rotationPivot != null
                ? Vector3.Scale(step.target.InverseTransformPoint(step.rotationPivot.position), step.target.localScale)
                : Vector3.zero;
            Quaternion endRotation = rotation * Quaternion.Euler(step.rotationOffset);
            var prepared = new PreparedStep
            {
                target = step.target,
                startPosition = position,
                startRotation = rotation,
                endPosition = position + rotation * pivotOffset - endRotation * pivotOffset + step.positionOffset,
                endRotation = endRotation,
                pivotOffset = pivotOffset,
                positionOffset = step.positionOffset,
                openDuration = Mathf.Max(0f, step.openDuration),
                closeDuration = Mathf.Max(0f, step.closeDuration),
                delay = Mathf.Max(0f, step.delay)
            };
            preparedSteps.Add(prepared);
            lastSteps[step.target] = prepared;
        }
        // 動作リストが空でも、音声やUnityEventだけの調査対象として使用できる。
    }
    // 毎フレーム、カメラと発光判定の更新後に入力を受け付け、開閉中なら姿勢を更新する。
    // 押した瞬間だけを見ることで、長押しによる開閉の繰り返しを防ぐ。
    private void LateUpdate()
    {
        if (GameOptions.Blocked) return;
        RefreshInteractionAvailability();
        if (IsMoving)
        {
            // 完了するフレームの連打も、別の操作やまばたきには渡さない。
            if (GameOptions.Down(GameOptions.Action.Interact)) consumedFrame = Time.frameCount;
            AdvanceMotion(Time.deltaTime);
            return;
        }

        // 発光判定が終わった後で、Eキーを押した瞬間だけ調べる操作を受け付ける。
        if (GameOptions.Down(GameOptions.Action.Interact))
            TryInteract(IsOpen && HasSeparateOpenHighlight ? openStateHighlight : highlight);
    }

    // 反対側のノブも同じ管理元へ操作を渡す。開閉状態や動作リストはノブごとに複製しない。
    public bool TryInteract(InteractableHighlight source)
    {
        if (GameOptions.Blocked || EventSequenceController.IsAnyPlaying || !isActiveAndEnabled || activeDoor != null || ConsumedClick ||
            (IsOpen && !allowClose) ||
            source == null || !source.isActiveAndEnabled || !source.IsHighlighted ||
            (source != highlight && source != openStateHighlight && !linkedHighlights.Contains(source)) ||
            (HasSeparateOpenHighlight && (source == openStateHighlight ? !IsOpen : source == highlight && IsOpen)))
            return false;

        // 条件に失敗した調査を「開いた」と記録しない。イベント側が一度きりの状態を管理する。
        if (eventSequence != null)
        {
            consumedFrame = Time.frameCount;
            return eventSequence.TryPlay();
        }

        // 開始イベントで別の処理が呼ばれても競合しないよう、先に操作中の状態を確定する。
        consumedFrame = Time.frameCount;
        activeDoor = this;
        opening = !IsOpen;
        stepIndex = opening ? 0 : preparedSteps.Count - 1;
        elapsed = 0f;
        IsMoving = true;
        SetHighlightsSuppressed(true);
        if (opening) onOpeningStarted.Invoke();
        else onClosingStarted.Invoke();
        // 待機ゼロの表示切替は決定キーを押したフレームで完了させる。
        if (IsMoving) AdvanceMotion(0f);
        return true;
    }

    // 登録済みの両面のノブをまとめて消灯し、動作中に調べられるように見えるのを防ぐ。
    private void SetHighlightsSuppressed(bool suppressed)
    {
        bool unavailable = suppressed || (IsOpen && !allowClose) ||
            (eventSequence != null && eventSequence.IsCompletedOnce);
        if (highlight != null) highlight.InteractionSuppressed = unavailable || (IsOpen && HasSeparateOpenHighlight);
        if (HasSeparateOpenHighlight) openStateHighlight.InteractionSuppressed = unavailable || !IsOpen;
        foreach (InteractableHighlight linked in linkedHighlights)
            if (linked != null && linked != highlight && linked != openStateHighlight)
                linked.InteractionSuppressed = unavailable;
    }

    // 最初の描画から未使用側を消灯する。戻し設定をInspectorで変更した場合も反映する。
    private void Start() => RefreshInteractionAvailability();

    private void RefreshInteractionAvailability()
    {
        SetHighlightsSuppressed(IsMoving);
        if (proximityTarget != null) proximityTarget.enabled = !(IsOpen && !allowClose) &&
            !(eventSequence != null && eventSequence.IsCompletedOnce);
    }

    // 開閉途中で有効になったノブにも、現在の操作制限を適用する。
    public void RegisterHandle(InteractableHighlight source)
    {
        if (source == null) return;
        linkedHighlights.Add(source);
        SetHighlightsSuppressed(IsMoving);
    }

    // 無効化・破棄されたノブの登録を外し、発光抑制が残らないよう解除する。
    public void UnregisterHandle(InteractableHighlight source)
    {
        if (source == null || !linkedHighlights.Remove(source)) return;
        if (source != highlight) source.InteractionSuppressed = false;
    }

    // 現在のステップが完了してから次へ進む。フレーム内の余り時間も次へ渡し、時間のずれを抑える。
    // Time.deltaTimeを使うためTime.timeScaleが0なら待機・開閉も停止する。
    private void AdvanceMotion(float deltaTime)
    {
        if (Time.timeScale == 0f) return;
        elapsed += deltaTime;
        while (stepIndex >= 0 && stepIndex < preparedSteps.Count)
        {
            PreparedStep step = preparedSteps[stepIndex];
            if (step.audio != null)
            {
                // 対象外の方向では待機も行わない。閉じる順序は他のステップと同様に逆順。
                bool execute = step.audioTiming == AudioTiming.Both ||
                    (opening ? step.audioTiming == AudioTiming.OpeningOnly : step.audioTiming == AudioTiming.ClosingOnly);
                if (execute)
                {
                    if (elapsed < step.delay) return;
                    step.audio.Execute();
                    elapsed = Mathf.Max(0f, elapsed - step.delay);
                }
                // 音の再生終了は待たず次へ進み、同じステップを毎フレーム再生しない。
                stepIndex += opening ? 1 : -1;
                continue;
            }
            if (step.target == null)
            {
                CancelMotion();
                return;
            }
            if (elapsed < step.delay) return;
            if (step.stepType == StepType.SetActive)
            {
                step.target.gameObject.SetActive(opening ? step.endActive : step.startActive);
                // SetActive先のコールバックで操作元が停止した場合は続行しない。
                if (!IsMoving) return;
                elapsed = Mathf.Max(0f, elapsed - step.delay);
                stepIndex += opening ? 1 : -1;
                continue;
            }
            float duration = opening ? step.openDuration : step.closeDuration;
            float progress = duration <= 0f ? 1f : Mathf.Clamp01((elapsed - step.delay) / duration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            ApplyPose(step, opening ? eased : 1f - eased);
            if (progress < 1f) return;

            elapsed = Mathf.Max(0f, elapsed - step.delay - duration);
            stepIndex += opening ? 1 : -1;
        }

        // リスト全体が完了して初めて操作制限を解除し、開閉完了を通知する。
        IsOpen = opening;
        ReleaseMotion();
        if (IsOpen) onOpened.Invoke();
        else onClosed.Invoke();
    }

    // ローカル座標で補間するため、ノブの親であるドアが動いても親子関係を維持できる。
    // 蝶番の位置が固定されるよう、回転だけでなく対象の原点の位置も円弧に沿って補正する。
    // 始点と終点の位置を直線補間すると途中で蝶番がずれるため、各フレームの回転から計算する。
    private void ApplyPose(PreparedStep step, float amount)
    {
        Quaternion rotation = Quaternion.Slerp(step.startRotation, step.endRotation, amount);
        step.target.localPosition = step.startPosition + step.startRotation * step.pivotOffset
            - rotation * step.pivotOffset + step.positionOffset * amount;
        step.target.localRotation = rotation;
    }
    // 正常終了と中断で共通の後片付け。入力制限と発光の抑制を解除する。
    private void ReleaseMotion()
    {
        IsMoving = false;
        if (activeDoor == this) activeDoor = null;
        RefreshInteractionAvailability();
    }

    // 動作途中で対象が消えたり無効になった場合に、最後の完了状態へ戻す。
    // 開閉は完了していないため、完了イベントは呼び出さない。
    private void CancelMotion()
    {
        // 無効化された場合は、直前の完了状態に戻し、操作制限が残らないようにする。
        if (IsMoving)
        {
            // 復元先のOnEnable/OnDisableが操作元を停止しても、復元処理へ再入しない。
            IsMoving = false;
            // 同じ対象が複数ある場合も、最後の適用結果が正しい完了姿勢になる順番で戻す。
            for (int i = 0; i < preparedSteps.Count; i++)
            {
                int index = IsOpen ? i : preparedSteps.Count - 1 - i;
                PreparedStep step = preparedSteps[index];
                // 中断による姿勢復元では音を再実行しない。停止済みのアラームも再開しない。
                if (step.audio != null) continue;
                if (step.target == null) continue;
                if (step.stepType == StepType.SetActive)
                    step.target.gameObject.SetActive(IsOpen ? step.endActive : step.startActive);
                else ApplyPose(step, IsOpen ? 1f : 0f);
            }
        }
        ReleaseMotion();
    }

    // UnityはコンポーネントやGameObjectの無効化・破棄時にここを呼ぶ。
    // 開閉中に無効化されても、プレイヤーが操作禁止のままにならないよう解除する。
    private void OnDisable()
    {
        CancelMotion();
    }
}
