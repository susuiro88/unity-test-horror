using UnityEngine;
using Unity.Cinemachine;
using System.Collections;

public class CameraSwitch : MonoBehaviour
{
    [SerializeField] private CinemachineCamera thirdPersonCamera;
    [SerializeField] private CinemachineCamera firstPersonCamera;
    //足音SEでイベント開始
    [SerializeField] private PlayerFootstep playerFootstep;

    private GameObject[] playerBodies;
    private bool firstPersonMode;
    public bool FirstPersonMode
    {
        get{ return firstPersonMode; }
    }
    void Start()
    {
        // PlayerBodyタグが付いたオブジェクトを全部取得
        playerBodies = GameObject.FindGameObjectsWithTag("PlayerBody");

    }

    void Update()
    {

        if (Input.GetKey(KeyCode.E))
        {
            //一人称のカメラの優先度を上げる
            FpCamera();
            //一人称視点モード オン
            firstPersonMode = true;

            //E押してすぐに向いている方向に向く
            firstPersonCamera.transform.rotation = thirdPersonCamera.transform.rotation;

            foreach (GameObject body in playerBodies)//基本、キャラクターのパーツ部分にplayerBodiesを設定
            {
                body.GetComponent<Renderer>().enabled = false;//姿を隠す。
            }
        }
        else if (Input.GetKeyDown(KeyCode.E) && playerFootstep.HearingSound)
        {
            //一人称のカメラの優先度を上げる
            FpCamera();
            //一人称視点モード オン
            firstPersonMode = true;
        }
        else
        {
            SpCamera();
            //一人称視点モード オフ
            firstPersonMode = false;
        }
        if (Input.GetKeyUp(KeyCode.E))//三秒だけ待機してから、姿を表す。
        {
            firstPersonMode=false;
            StartCoroutine(StopTime());
        }
          
    }

    
    void FpCamera()
    {
        // 一人称
        firstPersonCamera.Priority.Value = 10;//一人称のカメラの優先度をあげる
        thirdPersonCamera.Priority.Value = 0;//三人称のカメラの優先度を下げる
    }

    void SpCamera()
    {
        // 三人称
        firstPersonCamera.Priority.Value = 0;
        thirdPersonCamera.Priority.Value = 10;
    }
    IEnumerator StopTime()
    {
        yield return new WaitForSeconds(0.3f);
       foreach (GameObject body in playerBodies)
            {
                body.GetComponent<Renderer>().enabled = true;
            }
    }
}