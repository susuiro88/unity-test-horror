using UnityEngine;

// 既存Prefabの参照を維持し、カーソルをロックする。
// 視点切替はCameraSwitch、回転は既存のCinemachine構成に任せて二重制御を防ぐ。
public class CameraControl : MonoBehaviour
{
    [SerializeField] private CameraSwitch cameraSwitch;

    // 既存シーンの保存済み設定との互換性のため残す。カメラの回転には使用しない。
    [HideInInspector] public float mouseSensitivity = 200f;
    [HideInInspector] public GameObject cameraTransform;
    [HideInInspector] public Vector3 cameraOffset = new Vector3(0f, 5f, -6f);

    public bool FirstPersonMode => cameraSwitch != null && cameraSwitch.FirstPersonMode;

    // カーソルの初期化のみ行い、Qキーによる角度の上書きや制限は行わない。
    private void Start()
    {
        if (cameraSwitch == null) cameraSwitch = GetComponentInParent<CameraSwitch>();
        if (cameraSwitch == null) cameraSwitch = FindFirstObjectByType<CameraSwitch>();
        Cursor.lockState = CursorLockMode.Locked;
    }
}
