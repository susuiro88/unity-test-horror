using UnityEngine;

public class PlayerFootstep : MonoBehaviour
{
    [Header("歩数設定")]
    [SerializeField] private float stepDistance = 1.0f;//一歩を数える距離

    [Header("足音")]
    [SerializeField] private AudioSource audioSource;//音声を発するオブジェクト
    [SerializeField] private AudioClip footstepSound;//音声ファイルを指定
    [SerializeField] private int shortestStartTime = 25;//最短の出現開始
    [SerializeField] private int maximumLastTime = 50;//最長の出現開始
    [SerializeField] private CameraSwitch firstPersonMode;//１人称視点かどうかの判別
    [SerializeField] private MonsterExistence monsterExistence;

    private Vector3 previousPosition;//前の位置
    private float distanceWalked = 0f;//これまで歩いた距離（一歩歩くとリセット）
    private int stepCount = 0;//これまで歩いた歩数
    private int startTime;
    private int startTimeNext = 0;

    private bool hearingSound = false;
    public bool HearingSound
    {
        get { return hearingSound; } 
    }



    void Start()
    {
        previousPosition = transform.position;//プレイヤーの初期位置
        startTime = Random.Range(shortestStartTime, maximumLastTime);//最初の出現する間隔
    }

    void Update()
    {
        StepCount();
        if (stepCount == startTime + startTimeNext)// 最大50歩以内の場合は、50超えてからカウントする形にする。
        {
            Debug.Log("今回の乱数" + (startTime + startTimeNext));
            if (!audioSource.isPlaying) //音声再生中じゃない場合真になる。
            {
                SoundOfSteps();//音声再生（足音）
            }
            if (audioSource.isPlaying && audioSource.clip == footstepSound)
            {
                hearingSound = true;
            }

            startTime = Random.Range(shortestStartTime, maximumLastTime);//乱数調整
            startTimeNext += maximumLastTime;//n回目の歩数
        }
        if (audioSource.isPlaying & firstPersonMode.FirstPersonMode == true)//1人称視点になったら停止する。
        {
            audioSource.Stop();
        }
        


    }

    private void StepCount()//何歩進んだかをカウント
    {
        // 前フレームからの移動距離
        //float distance = Vector3.Distance(transform.position, previousPosition);

        // 上下方向の移動を無視する
        float distance = Vector3.ProjectOnPlane(transform.position - previousPosition, Vector3.up).magnitude;

        // 移動距離を累積
        distanceWalked += distance;

        // 一定距離歩いたら1歩
        if (distanceWalked >= stepDistance)
        {
            Step();
            distanceWalked -= stepDistance;//ここでまた0に戻る。
        }

        previousPosition = transform.position;//初期位置を更新
    }
    private void Step()
    {
        stepCount++;//カウント行う
        Debug.Log("歩数：" + stepCount);


        

        //if (audioSource != null && footstepSound != null)
        //{
        //    audioSource.PlayOneShot(footstepSound);
        //}
    }
    private void SoundOfSteps()
    {

        if (audioSource != null && footstepSound != null)
        {
            audioSource.PlayOneShot(footstepSound);
        }
    }
}