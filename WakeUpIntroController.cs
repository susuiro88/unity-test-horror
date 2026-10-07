using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

// Inspectorのステップを順に再生し、寝た視点から通常操作へ引き継ぐ。
[DefaultExecutionOrder(-10000)]
public class WakeUpIntroController : MonoBehaviour
{
    [Serializable]
    public class Step
    {
        public string name = "ステップ";
        [Min(0)] public float duration = 1;
        public AnimationCurve easing = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public bool move;
        [Tooltip("目印があればワールド座標。未指定なら開始時の水平な向きを基準に相対移動。")]
        public Transform positionTarget;
        public Vector3 localMove;
        public bool rotate;
        public Transform rotationTarget;
        [Tooltip("回転目印がなければ相対角度。左90度はY=-90。")]
        public Vector3 rotationDelta;
        public bool changeEyes;
        [Range(0, 2)] public float eyeOpen = 2;
        public bool startFuton;
        public bool waitForFuton;
        [Min(0.1f)] public float futonTimeout = 15;
        [Tooltip("最後のステップ専用。回転と同時に三人称へ切り替える。")]
        public bool switchToThirdPerson;
    }

    [Header("必須参照")]
    [Tooltip("寝ているときの目の位置に置いた空オブジェクトを登録します。開始時はこの位置から必ず真上（ワールドの＋Y方向）を見ます。Y回転は、後で視線を下ろすときの正面方向に使います。プレイヤーの子にはしないでください。")]
    [SerializeField] private Transform startPoint;
    [Tooltip("立ち上がった後のプレイヤーの配置先となる空オブジェクトを登録します。カメラの目の高さではなく、操作キャラ本体の基準位置（通常は足元）に置いてください。Y軸の回転が操作開始時の向きになります。プレイヤーの子にはしないでください。")]
    [SerializeField] private Transform playerEndPoint;
    [Tooltip("操作キャラに付いているPlayerMovementを登録します。演出中は移動処理を停止し、三人称への移行時にこのキャラをPlayer End Pointへ配置します。")]
    [SerializeField] private PlayerMovement player;
    [Tooltip("普段の一人称・三人称を切り替えているCameraSwitchを登録します。CameraSwitch側のFirst Person CameraとThird Person Cameraも設定してください。演出中の通常切替を止め、終了時に三人称へ戻します。")]
    [SerializeField] private CameraSwitch cameraSwitch;
    [Tooltip("実際に画面を描画するMain Cameraに付いているCinemachineBrainを登録します。Cinemachine Cameraではありません。演出カメラから三人称カメラへの滑らかな切替に使用します。")]
    [SerializeField] private CinemachineBrain brain;
    [Tooltip("既存のまばたきを管理するEyeBlinkを登録します。EyeBlink側のEye Close PanelとEye Materialも設定してください。同じまぶたの表示を使って、開始時の閉眼と開眼を行います。")]
    [SerializeField] private EyeBlink eyeBlink;
    [Tooltip("布団を上げるアニメーションを制御するAnimatorを登録します。Start Futonを使う場合に必要です。Futon State Nameで指定したステートの再生完了を自動検知します。Animation Eventによる通知も併用できます。")]
    [SerializeField] private Animator futonAnimator;
    [Tooltip("布団のAnimator ControllerにあるBoolパラメーター名です。通常はFutonWakeUpのまま使用します。大文字・小文字も一致させ、Animator側の初期値をfalseにしてください。Start Futonの開始時にtrueへ変更します。")]
    [SerializeField] private string futonBool = "FutonWakeUp";
    [Tooltip("終了を自動検知するAnimatorのステート名です。Bool名ではなく、Animator画面の状態名を指定します。通常はBase Layer.FutonWakeUpのままで使用できます。Animation Eventの終了通知も引き続き使用できます。")]
    [SerializeField] private string futonStateName = "Base Layer.FutonWakeUp";
    [Tooltip("布団ステートがあるAnimatorレイヤーの番号です。Base Layerは0です。")]
    [Min(0)] [SerializeField] private int futonLayer = 0;
    [Header("演出中に停止する入力処理・非表示にする体")]
    [Tooltip("マウス入力、ドア操作、Cinemachine Input Axis Controllerなどを登録。BrainとEyeBlinkは除く。")]
    [SerializeField] private Behaviour[] pauseDuringIntro = new Behaviour[0];
    [Tooltip("追加で隠したいRendererを登録します。操作キャラ配下のMeshRendererとSkinnedMeshRendererは自動で隠し、三人称カメラへの切替完了後に元の表示状態へ戻します。")]
    [SerializeField] private Renderer[] hideDuringIntro = new Renderer[0];
    [Header("上から順に実行（移動・回転・まぶたは同時）")]
    [SerializeField] private List<Step> steps = new List<Step>
    {
        new Step { name = "閉眼して待つ", duration = 0.3f },
        new Step { name = "目を開く", duration = 1.5f, changeEyes = true },
        new Step { name = "視線を下げる", duration = 1.5f, rotate = true, rotationDelta = new Vector3(70, 0, 0) },
        new Step { name = "布団を上げる", duration = 0, startFuton = true, waitForFuton = true },
        new Step { name = "左を向く", rotate = true, rotationDelta = new Vector3(0, -90, 0) },
        new Step { name = "立ち上がる", duration = 1.5f, move = true, localMove = new Vector3(0, 1, 0.6f) },
        new Step { name = "左を向き三人称へ", duration = 1.5f, rotate = true, rotationDelta = new Vector3(0, -90, 0), switchToThirdPerson = true }
    };
    private readonly Dictionary<Behaviour, bool> savedBehaviours = new Dictionary<Behaviour, bool>();
    private readonly Dictionary<Renderer, bool> savedRenderers = new Dictionary<Renderer, bool>();
    private CinemachineCamera introCamera;
    private CinemachineBlendDefinition savedBlend;
    private CinemachineBlenderSettings savedCustomBlends;
    private bool running;
    private bool futonFinished;
    private bool thirdPersonStarted;
    private float currentEyeOpen;

    // 最初の描画より前に閉眼し、通常処理による位置変更を止める。
    private void Awake()
    {
        if (!ValidateSetup()) { enabled = false; return; }
        running = true;
        savedBlend = brain.DefaultBlend;
        savedCustomBlends = brain.CustomBlends;
        brain.CustomBlends = null;
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0);
        Pause(player);
        foreach (Behaviour item in pauseDuringIntro) Pause(item);
        // 手動リストが空でも操作キャラの体が切替途中に映り込まないようにする。
        foreach (Renderer item in player.GetComponentsInChildren<Renderer>(true))
            if (item is MeshRenderer || item is SkinnedMeshRenderer) HideRenderer(item);
        foreach (Renderer item in hideDuringIntro) HideRenderer(item);
        cameraSwitch.SetIntroActive(true);
        eyeBlink.SetIntroEyeOpen(0);
        // 追従処理のないCinemachineカメラを作り、目印の座標・向きをそのまま使う。
        var cameraObject = new GameObject("WakeUp Intro Camera");
        cameraObject.SetActive(false);
        introCamera = cameraObject.AddComponent<CinemachineCamera>();
        introCamera.Lens = cameraSwitch.ThirdPersonCamera.Lens;
        introCamera.OutputChannel = cameraSwitch.ThirdPersonCamera.OutputChannel;
        introCamera.Priority.Value = int.MaxValue;
        // 最初は必ず天井を見る。目印のY回転を残して、視線を下ろす方向を指定できるようにする。
        Quaternion startRotation = Quaternion.Euler(0, startPoint.eulerAngles.y, 0) * Quaternion.Euler(-90, 0, 0);
        introCamera.transform.SetPositionAndRotation(startPoint.position, startRotation);
        cameraObject.SetActive(true);
    }

    // Coroutineで各ステップの時間と布団の終了を待ち、同一ステップの動きは並行する。
    private IEnumerator Start()
    {
        foreach (Step step in steps)
        {
            if (step.startFuton) { futonFinished = false; futonAnimator.SetBool(futonBool, true); }
            if (step.switchToThirdPerson) BeginThirdPerson(step.duration);
            Transform view = introCamera.transform;
            Vector3 fromPosition = view.position;
            Quaternion fromRotation = view.rotation;
            Quaternion heading = Quaternion.Euler(0, view.eulerAngles.y, 0);
            Vector3 toPosition = step.positionTarget != null ? step.positionTarget.position : fromPosition + heading * step.localMove;
            // 左右はワールドの上方向を軸に回すため、下を向いていても首を傾けず旋回する。
            Quaternion toRotation = step.rotationTarget != null ? step.rotationTarget.rotation :
                Quaternion.AngleAxis(step.rotationDelta.y, Vector3.up) * fromRotation * Quaternion.Euler(step.rotationDelta.x, 0, step.rotationDelta.z);
            float fromEye = currentEyeOpen;
            float elapsed = 0;
            while (true)
            {
                float t = step.duration <= 0 ? 1 : Mathf.Clamp01(elapsed / step.duration);
                float amount = t >= 1 ? 1 : Mathf.Clamp01(step.easing.Evaluate(t));
                if (step.move) view.position = Vector3.Lerp(fromPosition, toPosition, amount);
                if (step.rotate) view.rotation = Quaternion.Slerp(fromRotation, toRotation, amount);
                if (step.changeEyes)
                {
                    currentEyeOpen = Mathf.Lerp(fromEye, step.eyeOpen, amount);
                    eyeBlink.SetIntroEyeOpen(currentEyeOpen);
                }
                if (t >= 1) break;
                yield return null;
                elapsed += Time.deltaTime;
            }
            if (step.waitForFuton)
            {
                float waited = 0;
                while (!HasFutonFinished() && waited < step.futonTimeout)
                { yield return null; waited += Time.deltaTime; }
                if (!HasFutonFinished())
                {
                    Debug.LogWarning($"布団の再生完了を確認できませんでした。Animatorのステート「{futonStateName}」、レイヤー、再生速度、Loop Timeを確認してください。演出を終了して操作を戻します。", this);
                    break;
                }
            }
        }
        if (!thirdPersonStarted) BeginThirdPerson(0);
        // 指定秒数だけではなく、Brainが実際にブレンドを終えた描画後まで体を隠す。
        // 最初に必ず1回待ち、優先度変更がLateUpdateへ反映される前の判定を避ける。
        do { yield return new WaitForEndOfFrame(); }
        while (brain != null && brain.isActiveAndEnabled && brain.IsBlending);
        Finish();
    }

    // Animator側の中継コンポーネントから呼び出す。
    public void NotifyFutonFinished() { futonFinished = true; }

    // 対象ステートが最後まで再生されたら終了扱いにする。Idleの再生時間を誤って判定しない。
    // normalizedTimeは再生率で、1が1回分の終了。ループは終了しないため自動完了から除く。
    private bool HasFutonFinished()
    {
        if (futonFinished) return true;
        if (futonAnimator == null || !futonAnimator.isActiveAndEnabled ||
            futonLayer < 0 || futonLayer >= futonAnimator.layerCount) return false;
        AnimatorStateInfo state = futonAnimator.GetCurrentAnimatorStateInfo(futonLayer);
        if (state.IsName(futonStateName) && !state.loop && state.normalizedTime >= 1f &&
            !futonAnimator.IsInTransition(futonLayer))
            futonFinished = true;
        return futonFinished;
    }

    // 元から非表示のパーツは、演出終了時にも勝手に表示しない。
    private void HideRenderer(Renderer item)
    {
        if (item == null || savedRenderers.ContainsKey(item)) return;
        savedRenderers.Add(item, item.enabled);
        item.enabled = false;
    }

    private void Pause(Behaviour item)
    {
        if (item == null || item == this || item == brain || item == eyeBlink || item == cameraSwitch || item == futonAnimator || item is CinemachineCamera) return;
        if (!savedBehaviours.ContainsKey(item)) { savedBehaviours.Add(item, item.enabled); item.enabled = false; }
    }

    // CharacterControllerの衝突補正を一時停止し、安全に操作開始地点へ配置する。
    private void PlacePlayer()
    {
        CharacterController controller = player.GetComponent<CharacterController>();
        bool wasEnabled = controller != null && controller.enabled;
        if (controller != null) controller.enabled = false;
        player.transform.SetPositionAndRotation(playerEndPoint.position, Quaternion.Euler(0, playerEndPoint.eulerAngles.y, 0));
        if (controller != null) controller.enabled = wasEnabled;
    }

    // ブレンド中も入力をロックし、演出カメラの回転と三人称への移行を同時に行う。
    private void BeginThirdPerson(float duration)
    {
        PlacePlayer();
        thirdPersonStarted = true;
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, duration);
        introCamera.Priority.Value = -100;
    }

    // 途中で無効化されても通常操作・まぶた・カメラ設定を復元する。
    private void Finish()
    {
        if (!running) return;
        running = false;
        PlacePlayer();
        cameraSwitch.SetIntroActive(false);
        eyeBlink.EndIntro();
        if (introCamera != null) { introCamera.gameObject.SetActive(false); Destroy(introCamera.gameObject); }
        brain.DefaultBlend = savedBlend;
        brain.CustomBlends = savedCustomBlends;
        foreach (var entry in savedRenderers) if (entry.Key != null) entry.Key.enabled = entry.Value;
        foreach (var entry in savedBehaviours) if (entry.Key != null) entry.Key.enabled = entry.Value;
    }

    private void OnDisable() { StopAllCoroutines(); Finish(); }

    // 設定不足のまま開始して操作が戻らなくなることを防ぐ。
    private bool ValidateSetup()
    {
        var problems = new List<string>();
        if (startPoint == null) problems.Add("Start Point：寝た視点の空オブジェクトを登録してください。");
        if (playerEndPoint == null) problems.Add("Player End Point：操作開始位置の空オブジェクトを登録してください。");
        if (player == null) problems.Add("Player：操作キャラのPlayerMovementを登録してください。");
        if (cameraSwitch == null) problems.Add("Camera Switch：既存のCameraSwitchを登録してください。");
        else if (cameraSwitch.ThirdPersonCamera == null) problems.Add("Camera Switch内のThird Person Cameraが未設定です。");
        if (brain == null) problems.Add("Brain：Main CameraのCinemachineBrainを登録してください。");
        if (eyeBlink == null) problems.Add("Eye Blink：既存のEyeBlinkを登録してください。");
        else if (!eyeBlink.CanPlayIntro) problems.Add("Eye Blink内のEye Close PanelまたはEye Materialが未設定です。");
        if (steps == null || steps.Count == 0) problems.Add("Steps：ステップを1つ以上追加してください。");
        bool startedFuton = false;
        if (steps != null) for (int i = 0; i < steps.Count; i++)
        {
            Step step = steps[i];
            if (step == null) { problems.Add($"ステップ{i + 1}が未設定です。"); continue; }
            string label = $"ステップ{i + 1}「{step.name}」";
            if (step.easing == null) problems.Add(label + "：Easingを設定してください。");
            if (step.startFuton)
            {
                startedFuton = true;
                bool found = false;
                if (futonAnimator == null) problems.Add(label + "：Futon Animatorに布団のAnimatorを登録してください。");
                else if (futonAnimator.runtimeAnimatorController == null)
                    problems.Add($"Futon Animator「{futonAnimator.name}」：Animator Controllerが未設定です。");
                else
                {
                    foreach (var parameter in futonAnimator.parameters)
                        if (parameter.name == futonBool && parameter.type == AnimatorControllerParameterType.Bool) found = true;
                    if (!found) problems.Add($"Futon Animator「{futonAnimator.name}」（Controller：{futonAnimator.runtimeAnimatorController.name}）にBool「{futonBool}」がありません。操作キャラではなく、布団のAnimatorを登録してください。");
                }
            }
            if (step.waitForFuton && !startedFuton) problems.Add(label + "：布団の終了待ちより前にStart Futonを有効にしてください。");
            if (step.switchToThirdPerson && i != steps.Count - 1) problems.Add(label + "：Switch To Third Personは最後のステップだけで有効にしてください。");
        }
        // 設定不足は例外ではないため警告にし、Error Pauseで通常プレイまで停止させない。
        if (problems.Count > 0) Debug.LogWarning("起床演出を開始できません。以下を修正してください：\n・" + string.Join("\n・", problems), this);
        return problems.Count == 0;
    }
}
