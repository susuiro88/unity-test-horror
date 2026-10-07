using UnityEngine;
using Unity.Cinemachine;

// イベント中だけ視線を確定するCinemachine拡張。EventSequenceControllerが生成・破棄する。
// 三人称の自動注視による上書きを防ぎ、必要なステップだけ位置もプレイヤー基準で指定する。
public sealed class EventCameraLookOverride : CinemachineExtension
{
    public bool HasRotation { get; private set; }
    public Quaternion Rotation { get; private set; }
    private Transform lookTarget;
    private Renderer[] targetRenderers;
    private Vector3 targetOffset;
    private Quaternion startRotation;
    private float targetAmount;
    private bool trackingTarget;
    private Transform positionAnchor;
    private Vector3 positionFrom;
    private Vector3 positionTo;
    private float positionAmount;
    private bool returnToFollow;
    private bool hasPosition;

    // 開始位置をキャラ基準で保存し、歩行・旋回と同時でもカメラが置き去りにならないようにする。
    public void BeginPosition(Transform anchor, Vector3 currentPosition, Vector3 destination, bool useFollowCamera)
    {
        positionAnchor = anchor;
        Quaternion yaw = Quaternion.Euler(0, anchor.eulerAngles.y, 0);
        positionFrom = Quaternion.Inverse(yaw) * (currentPosition - anchor.position);
        positionTo = destination;
        positionAmount = 0;
        returnToFollow = useFollowCamera;
        hasPosition = true;
    }

    public void SetPositionProgress(float amount) => positionAmount = Mathf.Clamp01(amount);

    // Cinemachineが計算した位置に毎回加算せず、最終位置との差分で補正して累積移動を防ぐ。
    // 注視角を求める前に適用することで、移動後のカメラから対象を中央に捉える。
    private void ApplyPosition(ref CameraState state)
    {
        if (!hasPosition || positionAnchor == null) return;
        Quaternion yaw = Quaternion.Euler(0, positionAnchor.eulerAngles.y, 0);
        Vector3 from = positionAnchor.position + yaw * positionFrom;
        Vector3 to = returnToFollow ? state.GetFinalPosition() : positionAnchor.position + yaw * positionTo;
        Vector3 position = Vector3.Lerp(from, to, positionAmount);
        state.PositionCorrection += position - state.GetFinalPosition();
        if (returnToFollow && positionAmount >= 1) hasPosition = false;
    }

    public void SetRotation(Quaternion rotation)
    {
        trackingTarget = false;
        Rotation = rotation;
        HasRotation = true;
    }

    // 対象の向きではなく位置を見つめる。移動中も見続けるため参照を保存する。
    public void SetLookTarget(Transform target, Vector3 offset, Quaternion from, float amount)
    {
        if (lookTarget != target || targetRenderers == null)
        {
            lookTarget = target;
            targetRenderers = target != null ? target.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
        }
        targetOffset = offset;
        startRotation = from;
        targetAmount = Mathf.Clamp01(amount);
        trackingTarget = true;
        if (!HasRotation) Rotation = from;
        HasRotation = true;
    }

    // モデルの原点が足元や蝶番でも見た目の中心を狙う。空の目印ならその位置を使う。
    private Vector3 GetLookPoint()
    {
        Bounds bounds = new Bounds();
        bool found = false;
        foreach (Renderer item in targetRenderers)
        {
            if (item == null || !(item is MeshRenderer || item is SkinnedMeshRenderer) ||
                !item.enabled || !item.gameObject.activeInHierarchy) continue;
            if (!found) { bounds = item.bounds; found = true; }
            else bounds.Encapsulate(item.bounds);
        }
        return (found ? bounds.center : lookTarget.position) + lookTarget.TransformVector(targetOffset);
    }

    // Brainが描画カメラへ反映する直前の姿勢を変更し、Update順序に依存しないようにする。
    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (!enabled || stage != CinemachineCore.Stage.Finalize) return;
        ApplyPosition(ref state);
        if (!HasRotation) return;
        if (trackingTarget && lookTarget != null && !GameOptions.MenuOpen && Time.timeScale != 0f)
        {
            // Bodyと揺れ補正の適用後の描画位置から角度を求め、移動時の1フレームの遅れを防ぐ。
            Vector3 direction = GetLookPoint() - state.GetFinalPosition();
            if (direction.sqrMagnitude > 0.000001f)
            {
                Vector3 up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.9999f ? Vector3.forward : Vector3.up;
                Rotation = Quaternion.Slerp(startRotation, Quaternion.LookRotation(direction, up), targetAmount);
            }
        }
        state.RawOrientation = Rotation;
        state.OrientationCorrection = Quaternion.identity;
        // Z角はLook Rotationで指定するため、LensのDutchを二重に加えない。
        var lens = state.Lens;
        lens.Dutch = 0;
        state.Lens = lens;
    }
}
