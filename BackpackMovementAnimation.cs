using UnityEngine;

// 操作キャラのAnimatorのSpeedを読み、リュック専用Animatorを移動中だけ再生する。
// PlayerMovementが止まるイベント中も同じSpeedが更新されるため、手動操作・演出の両方に対応する。
// 任意で走行中だけ装着位置をずらし、前傾姿勢で背中から離れる見た目を補正する。
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[DefaultExecutionOrder(1000)]
public sealed class BackpackMovementAnimation : MonoBehaviour
{
    [Tooltip("操作キャラのPlayerMovement。キャラの子に置いた場合は自動取得します。")]
    [SerializeField] private PlayerMovement player;
    [Tooltip("リュック専用のAnimator。未設定ならこのGameObjectから取得します。")]
    [SerializeField] private Animator backpackAnimator;
    [Header("走行中の位置補正")]
    [Tooltip("オンなら、走行中だけこのGameObjectのローカル位置をずらします。歩行・停止で通常位置へ戻ります。")]
    [SerializeField] private bool adjustPositionWhenRunning;
    [Tooltip("通常位置に加えるローカルXYZの移動量。親の向き・拡大率が基準です。")]
    [SerializeField] private Vector3 runningLocalOffset;
    [Tooltip("通常位置と走行位置の切り替えにかける秒数。0なら即座に切り替えます。")]
    [SerializeField, Min(0)] private float positionTransitionTime = 0.15f;

    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private Animator sourceAnimator;
    private RuntimeAnimatorController sourceController;
    private bool hasSpeed;
    private bool warned;
    private Animator controlledAnimator;
    private float originalSpeed;
    private bool originalRootMotion;
    private Vector3 normalLocalPosition;
    private float runningPositionBlend;
    private bool positionApplied;

    private void Reset() => ResolveReferences();

    private void ResolveReferences()
    {
        if (player == null) player = GetComponentInParent<PlayerMovement>();
        if (backpackAnimator == null) backpackAnimator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        normalLocalPosition = transform.localPosition;
        runningPositionBlend = 0f;
        positionApplied = false;
        ResolveReferences();
        sourceAnimator = null;
        sourceController = null;
        hasSpeed = false;
        warned = false;
        UpdatePlayback();
    }

    // 通常のPlayerMovement.Updateの後で判定し、Animatorが評価される前に再生速度を設定する。
    private void Update() => UpdatePlayback();

    private void UpdatePlayback()
    {
        ResolveReferences();
        if (backpackAnimator == null) return;
        if (player != null && backpackAnimator == player.animator)
        {
            if (!warned) Debug.LogWarning("BackpackMovementAnimation：キャラクター本体ではなく、リュック専用のAnimatorを指定してください。", this);
            warned = true;
            RestoreAnimator();
            return;
        }
        if (controlledAnimator != backpackAnimator)
        {
            RestoreAnimator();
            controlledAnimator = backpackAnimator;
            originalSpeed = controlledAnimator.speed;
            originalRootMotion = controlledAnimator.applyRootMotion;
            // リュックの揺れで装着位置そのものが移動しないよう、Root Motionは適用しない。
            controlledAnimator.applyRootMotion = false;
        }

        Animator current = player != null ? player.animator : null;
        RuntimeAnimatorController controller = current != null ? current.runtimeAnimatorController : null;
        // 着替えでAnimatorやControllerが変わった場合だけパラメーターを再取得する。
        if (current != sourceAnimator || controller != sourceController)
        {
            sourceAnimator = current;
            sourceController = controller;
            hasSpeed = false;
            warned = false;
            if (current != null && controller != null)
                foreach (AnimatorControllerParameter parameter in current.parameters)
                    if (parameter.nameHash == SpeedId && parameter.type == AnimatorControllerParameterType.Float)
                    { hasSpeed = true; break; }
        }
        if (!hasSpeed && !warned)
        {
            Debug.LogWarning("BackpackMovementAnimation：Playerに、FloatのSpeedを持つAnimatorを使用している操作キャラを指定してください。", this);
            warned = true;
        }

        bool canPlay = hasSpeed && current != null && current.isActiveAndEnabled &&
            (player.isActiveAndEnabled || EventSequenceController.IsAnyPlaying) &&
            !GameOptions.Blocked && Time.timeScale > 0f;
        float movementSpeed = canPlay ? current.GetFloat(SpeedId) : 0f;
        // 0なら現在の姿勢で停止、0以外なら等速で再開する。歩行と走行で揺れの速さは変えない。
        controlledAnimator.speed = canPlay && !float.IsNaN(movementSpeed) && !float.IsInfinity(movementSpeed) && movementSpeed != 0f ? 1f : 0f;
    }

    // Animatorの姿勢更新後に装着位置を確定する。毎フレーム基準位置から計算し、補正の加算累積を防ぐ。
    private void LateUpdate()
    {
        if (!adjustPositionWhenRunning)
        {
            RestorePosition();
            return;
        }
        if (GameOptions.Blocked || Time.timeScale <= 0f) return;

        bool canRead = player != null && sourceAnimator != null && sourceAnimator == player.animator &&
            sourceAnimator != backpackAnimator && hasSpeed && sourceAnimator.isActiveAndEnabled &&
            (player.isActiveAndEnabled || EventSequenceController.IsAnyPlaying);
        float speed = canRead ? sourceAnimator.GetFloat(SpeedId) : 0f;
        bool running = canRead && IsRunningSpeed(speed, player.speed, player.dashSpeed);
        runningPositionBlend = positionTransitionTime <= 0f ? (running ? 1f : 0f)
            : Mathf.MoveTowards(runningPositionBlend, running ? 1f : 0f, Time.deltaTime / positionTransitionTime);
        if (!positionApplied && runningPositionBlend == 0f) return;
        transform.localPosition = normalLocalPosition + runningLocalOffset * runningPositionBlend;
        positionApplied = true;
    }

    // キー入力ではなく移動アニメーションの速度で判定し、イベントのRunにも対応する。
    // 歩行と走行の速度が同じ場合は区別できないため、位置補正を行わない。
    private static bool IsRunningSpeed(float speed, float walkSpeed, float runSpeed)
    {
        if (float.IsNaN(speed) || float.IsInfinity(speed) || speed == 0f ||
            walkSpeed <= 0f || runSpeed <= 0f || Mathf.Approximately(walkSpeed, runSpeed)) return false;
        float threshold = (walkSpeed + runSpeed) * 0.5f;
        return runSpeed > walkSpeed ? Mathf.Abs(speed) > threshold : Mathf.Abs(speed) < threshold;
    }

    // 装備を外した後に再表示しても補正が重ならないよう、通常位置とAnimatorの元の設定を戻す。
    private void OnDisable()
    {
        RestorePosition();
        RestoreAnimator();
    }

    private void RestorePosition()
    {
        if (positionApplied) transform.localPosition = normalLocalPosition;
        positionApplied = false;
        runningPositionBlend = 0f;
    }

    private void RestoreAnimator()
    {
        if (controlledAnimator != null)
        {
            controlledAnimator.speed = originalSpeed;
            controlledAnimator.applyRootMotion = originalRootMotion;
        }
        controlledAnimator = null;
    }
}
