using System.Collections;
using System.Transactions;
using UnityEngine;

public class DriveSenser : MonoBehaviour
{
    //車両や人などセンサーの土台を取得
    public MovingObject01 hostdevice;//MovingObject01の中のpublic変数を利用可能に。
    //センサーの幅の倍率と最大の幅
    public float SizeRate = 2.0f;
    
    private Vector3 initialSize = Vector3.zero;
    private Vector3 initialCenter = Vector3.zero;

    public BoxCollider bcol;//BoxColliderの変数利用可能に

    private void Start()
    {
        initialSize = bcol.size;
        initialCenter = bcol.center;
    }

    //ゲーム開始時にBoxColliderの取得と車両の速度と　の取得を行う。
    void Update()
    {   //センサーの最大幅
        //sensorLength = hostdevice.lengthObject * SizeRate;
        //速度によって、BoxColliderの大きさを変える
        if (!hostdevice.State )
        { 
            StartCoroutine(Sensor());
        }
        else
        {
            StartCoroutine(CouveSensor());
        }
    }
    IEnumerator Sensor()
    {
        while (true)
        {
            float hostSpeed = hostdevice.StraightSpeed;//直進進行時の移動距離／時間 スピードを表す
           
            //float hostMaxSpeed = hostdevice.StraightMaxSpeed;
            
            //hostSpeedによって、BoxColliderの幅を変える。

            /*
            //現在の速度 / 最大速度を最大BoxColiderの幅に掛け算。
            float speedRate = hostSpeed / hostMaxSpeed;
           */
            //現在速度から制動距離を求め、それをセンサーの長さとする。
            if (hostSpeed < 17.5f)
            {
                hostSpeed = 17.5f;//任意の値より下回らないようにする。
            }
            float speedRate = hostSpeed * hostSpeed / (254f * 0.7f);

            //BoxColliderの幅と中心に代入する。中心は幅の2分の１で
            bcol.size = new Vector3(bcol.size.x, bcol.size.y, speedRate * SizeRate);
            bcol.center = new Vector3(bcol.center.x, bcol.center.y, speedRate / 2 * SizeRate);
            yield return null;
        }
    }
    // Update is called once per frame
    IEnumerator CouveSensor()
    {
        while (true)
        {
            
            //基本0度から45度にかけて横幅が広がり、45度から90度にかけて横幅が元通りになる。
            float hostAngle = hostdevice.Angle;
            float hostCourveDist = hostdevice.CourveSpeed * hostdevice.CourveSpeed / (254f * 0.7f);
            //カーブ時のセンサーの最大幅、X座標が横幅になる
            float sizeCouveRate = 1 - (Mathf.Abs((hostAngle - 45)) / 45);//現時点角度から45度を減算し、45で割った値を、１から減算。これにより０度,90度が低く、45度が高くなる。

            bcol.size = new Vector3(initialSize.x + (initialSize.x * 2 * sizeCouveRate ), initialSize.y, hostCourveDist);//bcol.size.x + bcol.size.x * 1 が最大値。
            bcol.center = new Vector3(initialCenter.x + (initialSize.x  * sizeCouveRate ), initialCenter.y,initialCenter.z);//bcol.size.x + bcol.size.x * 1 が最大値。

            yield return null;
        }
    }
}
