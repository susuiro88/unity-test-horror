using UnityEngine;
using Unity.Cinemachine;
using System.Collections;
using System.Collections.Generic;

[DefaultExecutionOrder(-11000)]
public class CameraSwitch : MonoBehaviour
{
    [SerializeField] private CinemachineCamera thirdPersonCamera;
    [SerializeField] private CinemachineCamera firstPersonCamera;
    //足音SEでイベント開始
    [SerializeField] private PlayerFootstep playerFootstep;

    // 旧シーンの参照を専用コンポーネントへ引き継ぐため、旧フィールドを残す。
    [SerializeField, HideInInspector] private PlayerMovement playerMovement;
    [SerializeField, HideInInspector] private GameObject pajamaStyle;
    [SerializeField, HideInInspector] private GameObject suitStyle;
    public bool IntroActive => introActive;
    public bool EventActive { get; private set; }
    public CinemachineCamera FirstPersonCamera => firstPersonCamera;
    private readonly Dictionary<Unity.Cinemachine.InputAxisControllerBase<CinemachineInputAxisController.Reader>.Controller, bool> eventAxes = new();
    private bool bodyVisible = true;
    private readonly Dictionary<Renderer, bool> bodyVisibility = new Dictionary<Renderer, bool>();
    private bool firstPersonMode;
    private bool wasDoorLocked;
    private bool introActive;
    private bool toggledObservation;
    private float observationYaw;
    private float observationPitch;
    private readonly Dictionary<Unity.Cinemachine.InputAxisControllerBase<CinemachineInputAxisController.Reader>.Controller, bool> observationAxes = new();
    public CinemachineCamera ThirdPersonCamera => thirdPersonCamera;

    // 起床とは別の演出ロック。既存の追従カメラを使い、入力軸だけを止める。
    // 軸単位の停止により、オプション画面がコンポーネントを復帰しても視点が動かない。
    public void BeginEvent(bool firstPerson)
    {
        EventActive = true;
        StopAllCoroutines();
        RestoreThirdPersonInput();
        foreach (var camera in new[] { firstPersonCamera, thirdPersonCamera })
            foreach (var input in camera.GetComponents<CinemachineInputAxisController>())
                foreach (var axis in input.Controllers)
                    if (!eventAxes.ContainsKey(axis)) { eventAxes.Add(axis, axis.Enabled); axis.Enabled = false; }
        SetEventView(firstPerson);
    }

    // カメラの追従はCinemachineに任せ、優先度と体の表示を同時に切り替える。
    public void SetEventView(bool firstPerson)
    {
        if (!EventActive) return;
        if (firstPerson && !firstPersonMode)
            firstPersonCamera.transform.rotation = thirdPersonCamera.transform.rotation;
        firstPersonMode = firstPerson;
        if (firstPerson) FpCamera(); else SpCamera();
        SetBodyVisible(!firstPerson);
    }

    // 終了直後は最後の視点を維持し、次フレームから通常の観察操作へ戻す。
    public void EndEvent()
    {
        if (!EventActive) return;
        EventActive = false;
        foreach (var axis in eventAxes) axis.Key.Enabled = axis.Value;
        eventAxes.Clear();
        Vector3 angles = firstPersonCamera.transform.eulerAngles;
        observationYaw = angles.y;
        observationPitch = Mathf.DeltaAngle(0f, angles.x);
        toggledObservation = firstPersonMode && GameOptions.Current.choices[3] == 1;
        if (firstPersonMode)
            foreach (var input in thirdPersonCamera.GetComponents<CinemachineInputAxisController>())
                foreach (var axis in input.Controllers)
                    if (!observationAxes.ContainsKey(axis)) { observationAxes.Add(axis, axis.Enabled); axis.Enabled = false; }
    }

    // 演出中は通常のUpdateによるカメラの上書きを止める。
    public void SetIntroActive(bool active)
    {
        introActive = active;
        firstPersonMode = active;
        // 遅延中の表示復元が、起床演出の非表示を上書きしないようにする。
        if (active) StopAllCoroutines();
        RestoreThirdPersonInput();
        if (active) { firstPersonCamera.Priority.Value = 0; thirdPersonCamera.Priority.Value = 10; }
        else SpCamera();
    }
    public bool FirstPersonMode
    {
        get{ return firstPersonMode; }
    }
    // 起床演出のAwakeより先に服装と元の描画状態を確定する。
    void Awake()
    {
        // 旧設定しかないシーンでも、起床演出より先に専用コンポーネントへ引き継ぐ。
        CharacterOutfitController outfit = GetComponent<CharacterOutfitController>();
        if (outfit == null && pajamaStyle != null && suitStyle != null)
            outfit = gameObject.AddComponent<CharacterOutfitController>();
        if (outfit != null) outfit.Initialize();
        // 既存シーンのタグ設定も維持し、初めからオフのRendererは再表示しない。
        foreach (GameObject body in GameObject.FindGameObjectsWithTag("PlayerBody"))
            RememberRenderer(body.GetComponent<Renderer>());
        // 非アクティブな服装と、タグのないパジャマのパーツも視点切替の対象にする。
        if (pajamaStyle != null) RememberStyle(pajamaStyle);
        if (suitStyle != null) RememberStyle(suitStyle);
    }

    // 専用コンポーネントで未設定の参照だけを移行する。新しいInspector設定を優先する。
    public void GetLegacyOutfitReferences(out PlayerMovement movement, out GameObject pajamas, out GameObject suit)
    {
        movement = playerMovement;
        pajamas = pajamaStyle;
        suit = suitStyle;
    }

    // 服装の切り替え自体は専用コンポーネントが担当し、ここでは視点による非表示だけを扱う。
    public void RememberStyle(GameObject style)
    {
        if (style == null) return;
        foreach (Renderer body in style.GetComponentsInChildren<Renderer>(true))
        {
            RememberRenderer(body);
            if (!bodyVisible || firstPersonMode || introActive) body.enabled = false;
        }
    }
    private void RememberRenderer(Renderer body)
    {
        if (body != null && !bodyVisibility.ContainsKey(body))
            bodyVisibility.Add(body, body.enabled);
    }

    // カメラによる一時非表示を解除しても、元から隠していたパーツは表示しない。
    private void SetBodyVisible(bool visible)
    {
        bodyVisible = visible;
        foreach (var entry in bodyVisibility)
            if (entry.Key != null) entry.Key.enabled = visible && entry.Value;
    }

    void Update()
    {
        if (introActive || EventActive || GameOptions.Blocked) return;

        bool previousDoorLock = wasDoorLocked;
        wasDoorLocked = DoorInteraction.BlocksPlayerMovement;
        // 設定した2枠のどちらでも観察でき、長押し方式と切替方式を共通で扱う。
        bool isFirstPersonButtonDown = GameOptions.Down(GameOptions.Action.Observe);
        if (isFirstPersonButtonDown) GameOptions.ConsumeObservationPress();
        if (GameOptions.Current.choices[3] == 1 && isFirstPersonButtonDown) toggledObservation = !toggledObservation;
        if (GameOptions.Current.choices[3] == 0) toggledObservation = false;
        bool isFirstPersonButtonHeld = GameOptions.Current.choices[3] == 1 ? toggledObservation : GameOptions.Held(GameOptions.Action.Observe);
        // 複数キーの片方を離しても、もう一方を押していれば一人称を維持する。
        bool isFirstPersonButtonUp = firstPersonMode && !isFirstPersonButtonHeld;

        if (isFirstPersonButtonHeld || DoorInteraction.BlocksPlayerMovement)
        {
            // 入口だけ三人称の向きを引き継ぎ、その後は独立したマウス入力で回転する。
            bool entering = !firstPersonMode;
            if (entering) BeginObservation();
            //一人称のカメラの優先度を上げる
            FpCamera();
            //一人称視点モード オン
            firstPersonMode = true;

            if (!entering) RotateObservation();

            SetBodyVisible(false);
        }
        else
        {
            RestoreThirdPersonInput();
            SpCamera();
            //一人称視点モード オフ
            firstPersonMode = false;
        }
        if (!DoorInteraction.BlocksPlayerMovement && !isFirstPersonButtonHeld && (isFirstPersonButtonUp || previousDoorLock))//カメラの切替後、0.3秒待って体を戻す。
        {
            firstPersonMode=false;
            StopAllCoroutines();
            StartCoroutine(StopTime());
        }

    }

    // 非表示の三人称へマウス入力が流れ続けないよう、入力軸の元の有効状態を保存する。
    // メニューはコンポーネント単位で停止するため、こちらは軸単位で停止して復帰処理を分ける。
    private void BeginObservation()
    {
        firstPersonCamera.transform.rotation = thirdPersonCamera.transform.rotation;
        Vector3 angles = firstPersonCamera.transform.eulerAngles;
        observationYaw = angles.y;
        observationPitch = Mathf.DeltaAngle(0f, angles.x);
        foreach (var input in thirdPersonCamera.GetComponents<CinemachineInputAxisController>())
            foreach (var axis in input.Controllers)
                if (!observationAxes.ContainsKey(axis)) { observationAxes.Add(axis, axis.Enabled); axis.Enabled = false; }
    }

    // Mouse X/Yはフレーム内の移動量なのでdeltaTimeを重ねて掛けず、FPSで感度が変わるのを防ぐ。
    // 上下だけを制限し、左右は360度回転できる。オプションで適用した値を毎フレーム読む。
    private void RotateObservation()
    {
        float gain = GameOptions.Current.firstPersonSensitivity / 15f;
        observationYaw = Mathf.Repeat(observationYaw + Input.GetAxisRaw("Mouse X") * gain, 360f);
        observationPitch = Mathf.Clamp(observationPitch - Input.GetAxisRaw("Mouse Y") * gain, -80f, 80f);
        firstPersonCamera.transform.rotation = Quaternion.Euler(observationPitch, observationYaw, 0f);
    }

    private void RestoreThirdPersonInput()
    {
        foreach (var entry in observationAxes) entry.Key.Enabled = entry.Value;
        observationAxes.Clear();
    }

    private void OnDisable() { EndEvent(); RestoreThirdPersonInput(); }

    void FpCamera()
    {
        // 一人称
        firstPersonCamera.Priority.Value = 10;//一人称のカメラの優先度をあげる
        thirdPersonCamera.Priority.Value = 0;//三人称のカメラの優先度を下げる
    }

    void SpCamera()
    {
        // 三人称
        firstPersonCamera.Priority.Value = 0;
        thirdPersonCamera.Priority.Value = 10;
    }
    // Coroutineでカメラの移行を待ち、一人称や起床演出に戻っていれば表示しない。
    IEnumerator StopTime()
    {
        yield return new WaitForSeconds(0.3f);
        if (introActive || EventActive || firstPersonMode || DoorInteraction.BlocksPlayerMovement) yield break;
        SetBodyVisible(true);
    }
}
