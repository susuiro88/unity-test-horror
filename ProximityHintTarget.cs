using System.Collections.Generic;
using UnityEngine;

// アイテム・ボタン・隠れる場所など、「近づくと気づく対象」に付ける。
public class ProximityHintTarget : MonoBehaviour
{
    // 有効な対象だけを登録し、キャラ側が毎フレームシーン全体を検索しないようにする。
    private static readonly HashSet<ProximityHintTarget> activeTargets = new HashSet<ProximityHintTarget>();
    public static IEnumerable<ProximityHintTarget> ActiveTargets => activeTargets;

    [Tooltip("操作キャラが反応する半径（メートル）。高さの差も距離に含みます。")]
    [SerializeField, Min(0f)] private float detectionRadius = 3f;
    [Tooltip("距離と遮蔽物の判定位置。必要なら対象の表面に空の子オブジェクトを置いて指定します。")]
    [SerializeField] private Transform detectionPoint;
    [Tooltip("判定位置が未指定なら、このRendererの見た目の中心を使用します。")]
    [SerializeField] private Renderer targetRenderer;

    public float DetectionRadius => detectionRadius;
    public Vector3 DetectionPosition => detectionPoint != null
        ? detectionPoint.position
        : targetRenderer != null ? targetRenderer.bounds.center : transform.position;

    private void Awake()
    {
        // モデルの原点がずれていても、見た目の中心で反応できるようにする。
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();
    }

    private void OnEnable()
    {
        activeTargets.Add(this);
    }

    private void OnDisable()
    {
        // 拾ったアイテムなどを無効にすると、判定対象からも外れる。
        activeTargets.Remove(this);
    }

    private void OnDrawGizmosSelected()
    {
        // 選択中の対象の反応範囲をScene画面に表示する。
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(DetectionPosition, detectionRadius);
    }
}
