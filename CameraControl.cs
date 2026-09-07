using UnityEngine;

public class CameraControl : MonoBehaviour
{
    /*１人称視点のカメラワークを実装するスクリプト*/

    //3人称、１人称を切り替える。
    bool firstPersonMode;
    public bool FirstPersonMode
    {
        get { return firstPersonMode; }
    }
    //プレイヤーにセットして、マウスのカーソルの座標からカメラとプレイヤーの角度を変更させる。

    //マウス感度を調節
    public float mouseSensitivity = 200f;
    //カメラの位置
    public GameObject cameraTransform;
    //三人称にするためのオフセット
    public Vector3 cameraOffset = new Vector3(0f,5f,-6f);
    //カメラの角度
    float mouseX = 0f;
    float mouseY = 0f;

    float xRotation = 0f;
    float yRotation = 0f;



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //マウスを中央部に固定する
        Cursor.lockState = CursorLockMode.Locked;
        //cameraの中心座標にcameraOffsetを加算し、離れたところからプレイヤーを映す
        Vector3 cameraPosition = cameraTransform.transform.position;
        cameraPosition = cameraPosition + cameraOffset;
    }

    // Update is called once per frame
    void Update()
    {
        firstPersonMode = Input.GetKey(KeyCode.Q);
        if (firstPersonMode)
        {
            //マウスの座標を取得、右なら正、左なら負、上なら正、下なら負
            mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
            mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

            yRotation += mouseX;//さらに左右へ追加させる
            xRotation -= mouseY;//さらに上下へ追加させる。

            xRotation = Mathf.Clamp(xRotation, -60f, 60f);//XRotation > 60なら値は60を返す。XRotation < -40fなら値は-40を返す。
            yRotation = Mathf.Clamp(yRotation, -60f, 60f);//XRotation > 60なら値は60を返す。XRotation < -40fなら値は-40を返す。

            cameraTransform.transform.localRotation = Quaternion.Euler(xRotation, yRotation, 0f);

        }
        //GetAxis("MouseX")で画面中央を0として、マウスカーソルの座標を取得する。
        //mouseSensitivityはマウス感度を示す。
        //Time.deltaTimeはデバイスのフレームごとによって差があるため、統一する。これがあるとなめらかになるんじゃないかな


        //カメラのオブジェクトに代入させる



        //mouseYについて
        //マウスの座標をカメラの傾きに変換
        //xRotation = mouseX;
        //yRotation = mouseY;
        //xRotation = Mathf.Clamp(xRotation, -60f, 60f);//XRotation > 60なら値は60を返す。XRotation < -40fなら値は-40を返す。
        //yRotation = Mathf.Clamp(yRotation, -60f, 60f);//XRotation > 60なら値は60を返す。XRotation < -40fなら値は-40を返す。

        //カメラのオブジェクトの座標に代入させる。この場合縦に移動する。
        //Camera.main.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);//Camera.mainは、mainCameraのタグがついているカメラを対象とする。設定しないといけない

        //mouseXについて
        //カメラに追随してプレイヤーのオブジェクトを回転させる。
        //playerBody.Rotate(Vector3.up * mouseX);//Vector3.upはVector(0,1,0）と同じ意味

    }
}
