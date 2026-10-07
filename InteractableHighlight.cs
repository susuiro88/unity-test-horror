using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

// 一人称で画面中央に対象を捉えたとき、マテリアルを切り替えて発光させる。
// Cinemachineがカメラの位置を更新した後に、画面内の位置を判定する。
[DefaultExecutionOrder(10000)]
public class InteractableHighlight : MonoBehaviour
{
    [SerializeField] private CameraSwitch cameraSwitch;// 一人称視点かどうかを取得する。
    [Tooltip("この対象の？マークの範囲設定。同じオブジェクトか親から自動取得します。")]
    [SerializeField] private ProximityHintTarget proximityTarget;
    [Tooltip("操作キャラのPlayerProximityHint。距離と遮蔽物の判定を共通で使用します。")]
    [SerializeField] private PlayerProximityHint playerProximityHint;
    // 実際にGame画面を描画するCameraを指定する。
    [Tooltip("The actual rendering camera (usually Main Camera), not a Cinemachine camera.")]
    [SerializeField] private Camera viewCamera;
    [Tooltip("従来の単体指定。複数指定・親指定だけを使う場合はNoneで構いません。")]
    // 変数名変更前にInspectorで設定したRendererの参照を引き継ぐ。
    [FormerlySerializedAs("knobRenderer")]
    [SerializeField] private Renderer targetRenderer;// 発光させる対象を描画するRenderer。
    [Tooltip("複数を直接指定する場合に登録します。Target Renderer、Target Rootの対象とまとめて発光します。")]
    [SerializeField] private Renderer[] targetRenderers = new Renderer[0];
    [Tooltip("この親自身と、すべての子孫のRendererを取得します。非アクティブな子も登録し、表示されたときに対応します。")]
    [SerializeField] private Transform targetRoot;
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
    // Rendererごとに元のマテリアルを保持し、複数の材質・サブメッシュを個別に復元する。
    private class HighlightMaterials
    {
        public Renderer renderer;
        public Material[] originalMaterials;
        public Material[] glowMaterials;
    }
    private readonly List<HighlightMaterials> targets = new List<HighlightMaterials>();
    public bool IsHighlighted { get; private set; }// 他のスクリプトから発光中か確認できる。
    private bool interactionSuppressed;
    // 開閉中など、操作を受け付けない間は発光も止める。
    public bool InteractionSuppressed
    {
        get => interactionSuppressed;
        set
        {
            interactionSuppressed = value;
            if (value) SetHighlighted(false);
        }
    }

    private void Awake()
    {
        // Inspectorで指定されていない参照を自動取得する。
        if (cameraSwitch == null)
            cameraSwitch = FindFirstObjectByType<CameraSwitch>();
        if (viewCamera == null)
            viewCamera = Camera.main;
        if (proximityTarget == null)
            proximityTarget = GetComponentInParent<ProximityHintTarget>();
        if (playerProximityHint == null && cameraSwitch != null)
            playerProximityHint = cameraSwitch.GetComponentInParent<PlayerProximityHint>();
        if (playerProximityHint == null)
            playerProximityHint = FindFirstObjectByType<PlayerProximityHint>();

        // 未設定の対象を距離無制限で操作できないよう、発光判定側でも参照を必須にする。
        if (proximityTarget == null || playerProximityHint == null)
            Debug.LogWarning("InteractableHighlight: Proximity TargetとPlayer Proximity Hintを指定してください。未設定の間は発光しません。", this);

        // 必要な参照が見つからない場合は警告を出し、このスクリプトを停止する。
        if (cameraSwitch == null || viewCamera == null)
        {
            Debug.LogWarning("InteractableHighlight: Assign Camera Switch, View Camera and Target Renderer.", this);
            enabled = false;
            return;
        }

        InitializeTargets();
        if (targets.Count == 0)
        {
            Debug.LogWarning("InteractableHighlight：Target Renderer、Target Renderers、Target Rootのいずれかに発光対象を指定してください。", this);
            enabled = false;
        }
    }

    // 再生開始時に対象を集める。同じRendererが親指定とリストに重複していても1回だけ準備する。
    private void InitializeTargets()
    {
        var renderers = new HashSet<Renderer>();
        if (targetRenderer != null) renderers.Add(targetRenderer);
        if (targetRenderers != null)
            foreach (Renderer item in targetRenderers)
                if (item != null) renderers.Add(item);
        if (targetRoot != null)
            foreach (Renderer item in targetRoot.GetComponentsInChildren<Renderer>(true))
                renderers.Add(item);
        // 以前の単体設定と同様に、明示的な対象がない場合だけ自身から補完する。
        if (renderers.Count == 0 && targetRoot == null)
        {
            Renderer ownRenderer = GetComponent<Renderer>();
            if (ownRenderer != null) renderers.Add(ownRenderer);
        }
        foreach (Renderer item in renderers) PrepareMaterials(item);
    }

    // 共有マテリアルを編集せず複製することで、登録していない物体への発光の波及を防ぐ。
    private void PrepareMaterials(Renderer renderer)
    {
        Material[] originalMaterials = renderer.sharedMaterials;
        Material[] glowMaterials = new Material[originalMaterials.Length];
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
            Debug.LogWarning("InteractableHighlight：発光対応マテリアルを使用してください。対象：" + renderer.name, this);
        targets.Add(new HighlightMaterials
        {
            renderer = renderer, originalMaterials = originalMaterials, glowMaterials = glowMaterials
        });
    }

    // カメラ更新後に、現在の視点・距離・遮蔽物から発光だけを更新する。
    private void LateUpdate()
    {
        SetHighlighted(CanHighlight());
    }

    private bool CanHighlight()
    {
        if (interactionSuppressed || EventSequenceController.IsAnyPlaying)
            return Reject("Interaction is busy.");
        // 他の対象の？マークが出ていても、この対象が範囲外なら操作させない。
        // 一人称ではアイコンが消えるため、IsHintVisibleではなく共通の範囲判定を呼ぶ。
        if (proximityTarget == null || playerProximityHint == null || !playerProximityHint.CanReachTarget(proximityTarget))
            return Reject("Outside proximity range, obstructed, or proximity references missing.");
        // 一人称でないときや、必要なコンポーネントが無効なときは反応しない。
        if (cameraSwitch == null || !cameraSwitch.isActiveAndEnabled || !cameraSwitch.FirstPersonMode ||
            viewCamera == null || !viewCamera.isActiveAndEnabled)
            return Reject("Not in first person, or a required component is inactive.");

        // 対象全体の中心ではなく、表示中の各パーツで判定する。服が1枚消えても残った対象を調べられる。
        bool hasVisibleTarget = false;
        foreach (HighlightMaterials item in targets)
        {
            Renderer renderer = item.renderer;
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if ((viewCamera.cullingMask & (1 << renderer.gameObject.layer)) == 0) continue;
            hasVisibleTarget = true;
            if (CanFocusRenderer(renderer)) return true;
        }
        return hasVisibleTarget ? false : Reject("No active target renderer is included by the camera.");
    }

    // どれか1つを画面中央に捉え、遮られていなければ、登録したグループ全体を発光させる。
    private bool CanFocusRenderer(Renderer renderer)
    {
        // インポートしたモデルは原点が対象から離れている場合がある。
        // 対象自身が指定されていても、Rendererの見た目を囲む範囲の中心を使う。
        Vector3 point = focusPoint != null && focusPoint != renderer.transform && focusPoint != transform && focusPoint != targetRoot
            ? focusPoint.position : renderer.bounds.center;
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
            // 同じ発光グループの別パーツが手前にある場合も、自分自身として扱う。
            Transform hitTransform = hit.collider.transform;
            if (!IsTargetCollider(hitTransform))
                return Reject("Blocked by collider: " + hit.collider.name);
        }
        highlightStatus = "Highlighted";
        return true;
    }

    // 親配下の無関係なColliderまで通過扱いにしないよう、実際に登録されたRendererの枝だけを許可する。
    private bool IsTargetCollider(Transform hitTransform)
    {
        foreach (HighlightMaterials item in targets)
            if (item.renderer != null && item.renderer.enabled && item.renderer.gameObject.activeInHierarchy &&
                hitTransform.IsChildOf(item.renderer.transform)) return true;
        return false;
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
        foreach (HighlightMaterials item in targets)
            if (item.renderer != null)
                item.renderer.sharedMaterials = highlighted ? item.glowMaterials : item.originalMaterials;
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
        foreach (HighlightMaterials item in targets)
            foreach (Material material in item.glowMaterials)
                if (material != null) Destroy(material);
        targets.Clear();
    }

    private void OnValidate()
    {
        // Inspectorで範囲を編集したとき、画面の0～100%に収まるよう補正する。
        focusArea.x = Mathf.Clamp01(focusArea.x);
        focusArea.y = Mathf.Clamp01(focusArea.y);
    }
}
