using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    //移動速度
    public float speed = 3f;
    //ダッシュ移動速度
    public float dashSpeed = 6f;
    //アニメーションの設定
    public Animator animator;
    //回転の速度
    public float playerTurnSpeed = 10f; // 6fより少し速めが動かしや
    //重力の数値
    public float gravity = -9.81f;
    //プレイヤーのCharacterControllerを取得
    public CharacterController controller;

    private Vector3 moveDir;//移動追加の変数
    [SerializeField] private Transform cameraTransform;

    void Update()
    {
        // 1. 入力を取得（Rawを使うとキビキビ動きます）
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        float velocityY = 0; //Y軸

        //移動モードの切り替え
        float currentSpeed = speed;

        if (Input.GetKey(KeyCode.LeftShift))
        {
            currentSpeed = dashSpeed;
        }

        // 入力方向をベクトルにまとめる
        Vector3 inputDir = new Vector3(x, 0f, z).normalized;

        //重力を設定
        Vector3 gravityAdd = Vector3.zero;
        if(controller.isGrounded && velocityY < 0)//地面から離れた時
        {
            velocityY = -2f;
        }

        velocityY += gravity * Time.deltaTime;
        moveDir.y = velocityY;
        gravityAdd.y = velocityY;
        controller.Move(gravityAdd);



        // 入力がある時だけ移動と回転を処理
        if (inputDir.magnitude >= 0.1f)
        {
            // 2. カメラの向きに合わせて「進むべき方向」を計算
            // カメラのY軸の角度だけを取り出す
            float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;//Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Degで角度が求められる。

            // その角度を「方向ベクトル」に変換
            moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;


            // 3. 移動を実行
            controller.Move(moveDir * currentSpeed * Time.deltaTime);

            // 4. 回転（進む方向 moveDir を向かせる）
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, playerTurnSpeed * Time.deltaTime);

        }

        // アニメーション制御
        animator.SetFloat("Speed", inputDir.magnitude * currentSpeed);

    }
}