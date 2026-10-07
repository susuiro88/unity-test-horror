using UnityEngine;

// 反対側のドアノブに付け、開閉操作を既存のDoorInteractionへ渡す。
// 自分では開閉状態を持たないため、どちら側から操作しても同じ扉の状態で判断できる。
// 発光更新の後、EyeBlinkの前に入力を渡し、開閉とまばたきの同時発生を防ぐ。
[DefaultExecutionOrder(10001)]
public class DoorInteractionSide : MonoBehaviour
{
    [Tooltip("最初のノブに設定したDoorInteraction。動作リストと開閉状態はここで共通管理します。")]
    [SerializeField] private DoorInteraction doorInteraction;
    [Tooltip("このノブ自身のInteractableHighlight。未指定なら同じオブジェクトから取得します。")]
    [SerializeField] private InteractableHighlight highlight;
    private DoorInteraction registeredDoor;
    private InteractableHighlight registeredHighlight;

    // シーン内の別のドアへ誤接続しないよう、管理元はInspectorで明示的に指定する。
    private void Awake()
    {
        if (highlight == null) highlight = GetComponent<InteractableHighlight>();
        if (doorInteraction == null || highlight == null)
        {
            Debug.LogWarning("DoorInteractionSide: Door Interactionと、このノブのHighlightを指定してください。", this);
            enabled = false;
        }
    }

    // Unityの有効化時に登録し、管理元から動作中の消灯も受け取る。
    private void OnEnable()
    {
        if (doorInteraction == null || highlight == null) return;
        registeredDoor = doorInteraction;
        registeredHighlight = highlight;
        registeredDoor.RegisterHandle(registeredHighlight);
    }

    // 発光には一人称・中央範囲・近接条件が含まれるため、管理元でもその状態を確認する。
    private void LateUpdate()
    {
        if (registeredDoor != null && GameOptions.Down(GameOptions.Action.Interact))
            registeredDoor.TryInteract(registeredHighlight);
    }

    // ノブが無効化されても管理元の扉の動作は続け、ノブの登録だけを解除する。
    private void OnDisable()
    {
        if (registeredDoor != null) registeredDoor.UnregisterHandle(registeredHighlight);
        registeredDoor = null;
        registeredHighlight = null;
    }
}
