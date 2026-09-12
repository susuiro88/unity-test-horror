using UnityEngine;
using UnityEngine.Serialization;

// 一人称で画面中央に対象を捉えたとき、マテリアルを切り替えて発光させる。
// Cinemachineがカメラの位置を更新した後に、画面内の位置を判定する。
[DefaultExecutionOrder(10000)]
public class InteractableHighlight : MonoBehaviour
{
    [SerializeField] private CameraSwitch cameraSwitch;// 一人称視点かどうかを取得する。
    // 実際にGame画面を描画するCameraを指定する。
    [Tooltip("The actual rendering camera (usually Main Camera), not a Cinemachine camera.")]
    [SerializeField] private Camera viewCamera;
    [Tooltip("Assign the renderer of the object to highlight.")]
    // 変数名変更前にInspectorで設定したRendererの参照を引き継ぐ。
    [FormerlySerializedAs("knobRenderer")]
    [SerializeField] private Renderer targetRenderer;// 発光させる対象を描画するRenderer。
    // 判定位置を別の目印で指定したい場合に使用。未指定・対象自身なら見た目の中心を使う。
    [Tooltip("Optional separate focus marker. None or the target itself uses the visible mesh center, not its imported pivot.")]
    [SerializeField] private Transform focusPoint;
    [Tooltip("0.2 means the central 20% of screen width and height (80% reduction).")]
    [SerializeField] private Vector2 focusArea = new Vector2(0.2f, 0.2f);// 中央の幅20%・高さ20%を判定範囲にする。
    // HDRカラーで1より大きな明るさも指定できる。
    [ColorUsage(false, true)]
    [SerializeField] private Color glowColor = new Color(3f, 2.5f, 1f);
    [Tooltip("Layers containing sight blockers. Exclude the player layer.")]
    [SerializeField] private LayerMask obstructionMask = ~0;// 視線を遮る物体のレイヤー。~0はすべてのレイヤー。

    [Header("Runtime diagnostics (updated during Play mode)")]
    [SerializeField] private string highlightStatus;// 再生中に発光しない理由をInspectorで確認するための表示。

    // シェーダーの発光色プロパティを、名前ではなくIDで指定できるようにする。
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private Material[] originalMaterials;// 発光を解除するときに戻す元のマテリアル。
    private Material[] glowMaterials;// この対象専用に複製した発光用マテリアル。
    public bool IsHighlighted { get; private set; }// 他のスクリプトから発光中か確認できる。

    private void Awake()
    {
        // Inspectorで指定されていない参照を自動取得する。
        if (cameraSwitch == null)
            cameraSwitch = FindFirstObjectByType<CameraSwitch>();
        if (viewCamera == null)
            viewCamera = Camera.main;
        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        // 必要な参照が見つからない場合は警告を出し、このスクリプトを停止する。
        if (cameraSwitch == null || viewCamera == null || targetRenderer == null)
        {
            Debug.LogWarning("InteractableHighlight: Assign Camera Switch, View Camera and Target Renderer.", this);
            enabled = false;
            return;
        }

        // 元の共有マテリアルは編集せず、複製を作る。他の物体まで光るのを防ぐ。
        originalMaterials = targetRenderer.sharedMaterials;
        glowMaterials = new Material[originalMaterials.Length];
        bool supportsEmission = false;
        for (int i = 0; i < originalMaterials.Length; i++)
        {
            Material source = originalMaterials[i];
            if (source == null)
                continue;
            glowMaterials[i] = new Material(source);
            // 発光色に対応しているシェーダーだけ、Emissionを有効化して色を設定する。
            if (!source.HasProperty(EmissionColor))
                continue;
            supportsEmission = true;
            glowMaterials[i].EnableKeyword("_EMISSION");
            glowMaterials[i].SetColor(EmissionColor, glowColor);
        }

        if (!supportsEmission)
            Debug.LogWarning("InteractableHighlight: Use an emission-capable material such as URP/Lit.", this);
    }

    private void LateUpdate()
    {
        // 毎フレーム、カメラ更新後の判定結果に合わせて発光を切り替える。
        SetHighlighted(CanHighlight());
    }

    private bool CanHighlight()
    {
        // 一人称でないときや、必要なコンポーネントが無効なときは反応しない。
        if (cameraSwitch == null || !cameraSwitch.isActiveAndEnabled || !cameraSwitch.FirstPersonMode ||
            viewCamera == null || !viewCamera.isActiveAndEnabled ||
            targetRenderer == null || !targetRenderer.enabled || !targetRenderer.gameObject.activeInHierarchy)
            return Reject("Not in first person, or a required component is inactive.");

        // カメラの描画対象に含まれていないレイヤーなら、画面に映らないので除外する。
        if ((viewCamera.cullingMask & (1 << targetRenderer.gameObject.layer)) == 0)
            return Reject("Target layer is excluded by the camera.");

        // インポートしたモデルは原点が対象から離れている場合がある。
        // 対象自身が指定されていても、Rendererの見た目を囲む範囲の中心を使う。
        Vector3 point = focusPoint != null && focusPoint != targetRenderer.transform && focusPoint != transform
            ? focusPoint.position : targetRenderer.bounds.center;
        // ワールド座標を画面内の座標に変換。左下が(0, 0)、右上が(1, 1)、中央が(0.5, 0.5)。
        Vector3 viewport = viewCamera.WorldToViewportPoint(point);
        // 範囲0.2なら中央から左右・上下に0.1ずつ、つまり0.4～0.6の範囲になる。
        Vector2 halfArea = focusArea * 0.5f;
        // カメラの描画距離外（背後を含む）、または中央の範囲外なら発光させない。
        if (viewport.z < viewCamera.nearClipPlane || viewport.z > viewCamera.farClipPlane ||
            Mathf.Abs(viewport.x - 0.5f) > halfArea.x ||
            Mathf.Abs(viewport.y - 0.5f) > halfArea.y)
            return Reject("Target center is outside the focus area or camera clipping range.");

        // カメラの手前の描画面から対象までRayを飛ばして、遮蔽物を調べる。
        // 対象までの距離に限定するので、対象の後ろにある壁などは判定しない。
        // Triggerは視線を遮る物体として扱わない。
        Ray ray = viewCamera.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
        float distance = Vector3.Distance(ray.origin, point);
        if (Physics.Raycast(ray, out RaycastHit hit, distance, obstructionMask, QueryTriggerInteraction.Ignore))
        {
            // 対象自身かその子のColliderなら許可。それ以外に当たったら遮られている。
            Transform hitTransform = hit.collider.transform;
            if (hitTransform != targetRenderer.transform && !hitTransform.IsChildOf(targetRenderer.transform))
                return Reject("Blocked by collider: " + hit.collider.name);
        }
        highlightStatus = "Highlighted";
        return true;
    }

    private bool Reject(string reason)
    {
        // 発光できない理由を保存して、判定失敗を返す。
        highlightStatus = reason;
        return false;
    }

    private void SetHighlighted(bool highlighted)
    {
        // 前のフレームと状態が同じなら、マテリアルを再設定しない。
        if (IsHighlighted == highlighted)
            return;
        IsHighlighted = highlighted;
        // 発光時は複製したマテリアル、解除時は元のマテリアルに切り替える。
        if (targetRenderer != null)
            targetRenderer.sharedMaterials = highlighted ? glowMaterials : originalMaterials;
    }

    private void OnDisable()
    {
        // スクリプトやオブジェクトを無効にしたときも、発光を解除する。
        SetHighlighted(false);
    }

    private void OnDestroy()
    {
        // 削除時は元に戻してから、実行中に作ったマテリアルを破棄する。
        // 元のマテリアルは共有しているため破棄しない。
        SetHighlighted(false);
        if (glowMaterials == null)
            return;
        foreach (Material material in glowMaterials)
            if (material != null)
                Destroy(material);
    }

    private void OnValidate()
    {
        // Inspectorで範囲を編集したとき、画面の0～100%に収まるよう補正する。
        focusArea.x = Mathf.Clamp01(focusArea.x);
        focusArea.y = Mathf.Clamp01(focusArea.y);
    }
}
