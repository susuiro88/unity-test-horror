using UnityEngine;

// 操作キャラのルートに付ける。三人称で近くに見える対象があれば頭上のアイコンを表示する。
// カメラ更新後にアイコンの向きを合わせる。
[DefaultExecutionOrder(10000)]
public class PlayerProximityHint : MonoBehaviour
{
    [SerializeField] private CameraSwitch cameraSwitch;
    [Tooltip("Game画面を描画する実際のCamera。既存構成ではThirdPersonCameraを指定します。")]
    [SerializeField] private Camera viewCamera;
    [Tooltip("操作キャラ全体のルート。未指定なら、このスクリプトのTransformを使用します。")]
    [SerializeField] private Transform playerRoot;
    [Tooltip("遮蔽物を調べる始点。キャラの胸や目の位置に置いた空オブジェクトを指定します。")]
    [SerializeField] private Transform sightOrigin;
    [Tooltip("始点未指定時の、キャラのルートからのローカルオフセット。")]
    [SerializeField] private Vector3 sightOffset = new Vector3(0f, 1.5f, 0f);
    [Tooltip("頭上のWorld Space Canvasなど、表示を切り替える子オブジェクト。")]
    [SerializeField] private GameObject iconObject;
    [Tooltip("壁など、視線を遮るColliderのレイヤー。Triggerは無視します。")]
    [SerializeField] private LayerMask obstructionMask = ~0;

    public bool IsHintVisible { get; private set; }

    private void Awake()
    {
        if (playerRoot == null)
            playerRoot = transform;
        if (cameraSwitch == null)
            cameraSwitch = playerRoot.GetComponentInChildren<CameraSwitch>();
        if (cameraSwitch == null)
            cameraSwitch = FindFirstObjectByType<CameraSwitch>();
        if (viewCamera == null)
            viewCamera = Camera.main;

        // キャラ自身を非表示にしてしまわないよう、アイコンの指定を確認する。
        if (iconObject == null || transform.IsChildOf(iconObject.transform) ||
            playerRoot.IsChildOf(iconObject.transform))
        {
            Debug.LogWarning("PlayerProximityHint: Icon Objectには頭上アイコン専用の子オブジェクトを指定してください。", this);
            enabled = false;
            return;
        }

        iconObject.SetActive(false);
        if (cameraSwitch == null || viewCamera == null)
        {
            Debug.LogWarning("PlayerProximityHint: Camera SwitchとView Cameraを指定してください。", this);
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        // 演出中は三人称へ切り替わっても調査を促さない。完了後に操作を保持している間も隠す。
        // LateUpdateで表示を更新するため、通常のUpdateから始まった演出は描画前に非表示になる。
        if (EventSequenceController.IsAnyPlaying)
        {
            SetVisible(false);
            return;
        }

        bool visible = false;
        if (cameraSwitch != null && cameraSwitch.isActiveAndEnabled && !cameraSwitch.FirstPersonMode &&
            viewCamera != null && viewCamera.isActiveAndEnabled && playerRoot != null)
        {
            foreach (ProximityHintTarget target in ProximityHintTarget.ActiveTargets)
            {
                if (!CanReachTarget(target))
                    continue;

                // 1つでも条件を満たせば表示。複数の対象があってもアイコンは1つだけ。
                visible = true;
                break;
            }
        }

        SetVisible(visible);
        if (visible && iconObject != null)
        {
            // Canvasをカメラの画面と平行にする。キャラの周囲を回っても「？」を読める。
            iconObject.transform.rotation = viewCamera.transform.rotation;
        }
    }

    // アイコン表示と一人称の操作で同じ距離・遮蔽物条件を使う。
    // 視点モードやアイコンの表示状態に依存させず、対象ごとに現在の位置で判定する。
    public bool CanReachTarget(ProximityHintTarget target)
    {
        if (!isActiveAndEnabled || playerRoot == null || target == null || !target.isActiveAndEnabled)
            return false;

        // 距離はカメラではなくキャラから測り、高さの差も半径に含める。
        Vector3 point = target.DetectionPosition;
        if ((point - playerRoot.position).sqrMagnitude > target.DetectionRadius * target.DetectionRadius)
            return false;

        Vector3 origin = sightOrigin != null ? sightOrigin.position : playerRoot.TransformPoint(sightOffset);
        return HasClearSight(origin, point, target);
    }

    // Raycastでキャラから対象までの遮蔽物を確認する。カメラだけが壁を越えても操作を許可しない。
    private bool HasClearSight(Vector3 origin, Vector3 point, ProximityHintTarget target)
    {
        Vector3 direction = point - origin;
        float distance = direction.magnitude;
        if (distance <= Mathf.Epsilon)
            return true;

        // キャラ自身を無視した後も奥の壁を確認できるよう、途中のすべての衝突を調べる。
        RaycastHit[] hits = Physics.RaycastAll(origin, direction / distance, distance,
            obstructionMask, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            Transform hitTransform = hit.collider.transform;
            if (hitTransform.IsChildOf(playerRoot) || hitTransform.IsChildOf(target.transform))
                continue;
            return false;
        }
        return true;
    }

    private void SetVisible(bool visible)
    {
        IsHintVisible = visible;
        if (iconObject != null && iconObject.activeSelf != visible)
            iconObject.SetActive(visible);
    }

    private void OnDisable()
    {
        // 自分自身や親を誤指定した場合は、そのオブジェクトを無効化しない。
        if (iconObject != null && !transform.IsChildOf(iconObject.transform) &&
            (playerRoot == null || !playerRoot.IsChildOf(iconObject.transform)))
            SetVisible(false);
    }
}
