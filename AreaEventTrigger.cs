using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// プレイヤーの基準点が箱型の範囲に入ると音声やInspector指定のイベントを実行する。
// 物理のOnTriggerEnterに依存しないため、起動時から範囲内でもRigidbodyなしで判定できる。
[RequireComponent(typeof(BoxCollider))]
public sealed class AreaEventTrigger : MonoBehaviour
{
    [Tooltip("プレイヤーの足元などの基準点。未指定なら起動時にPlayerMovementを探します。")]
    [SerializeField] private Transform player;
    [SerializeField] private bool triggerOnStartInside = true;
    [Tooltip("オンなら、このシーンで一度だけ実行。無効化・再有効化しても実行済みを維持します。")]
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private List<InteractionAudioAction> audioActions = new List<InteractionAudioAction>();
    [Tooltip("GameObject.SetActive、Animator.SetTriggerなどを登録します。音声実行後に呼び出します。")]
    [SerializeField] private UnityEvent onEntered = new UnityEvent();
    private BoxCollider area;
    private bool sampled, wasInside, hasTriggered;

    // 範囲は壁ではないためTriggerにする。サイズとCenterはBoxColliderで編集する。
    private void Reset() => GetComponent<BoxCollider>().isTrigger = true;
    private void Awake()
    {
        area = GetComponent<BoxCollider>();
        area.isTrigger = true;
    }
    private void Start()
    {
        if (player == null)
        {
            var movement = FindFirstObjectByType<PlayerMovement>();
            if (movement != null) player = movement.transform;
        }
        if (player == null) Debug.LogWarning("AreaEventTrigger：Playerに判定するプレイヤーを指定してください。", this);
    }

    // Updateで現在位置を調べるため、起床中に移動スクリプトが停止していても判定する。
    // 箱のローカル座標へ変換し、回転・拡大した範囲でも正確に基準点を判定する。
    private void Update()
    {
        if (player == null || !area.enabled || GameOptions.MenuOpen || Time.timeScale == 0f) return;
        Vector3 point = area.transform.InverseTransformPoint(player.position) - area.center;
        Vector3 half = area.size * 0.5f;
        bool inside = Mathf.Abs(point.x) <= half.x && Mathf.Abs(point.y) <= half.y && Mathf.Abs(point.z) <= half.z;
        bool entered = inside && (sampled ? !wasInside : triggerOnStartInside);
        sampled = true;
        wasInside = inside;
        if (!entered || (triggerOnce && hasTriggered)) return;
        // コールバックがこのオブジェクトを無効化しても二重実行にならないよう先に記録する。
        hasTriggered = true;
        foreach (var action in audioActions) action?.Execute();
        onEntered.Invoke();
    }
}
