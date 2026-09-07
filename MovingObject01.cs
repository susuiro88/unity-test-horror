using System.Collections;
using System.Collections.Generic;
using UnityEditor.XR;
using UnityEngine;
using UnityEngine.Rendering;
public class MovingObject01 : MonoBehaviour
{
    //ループするかしないかのBool型
    public bool Luup;
    //これから進む方向を示す
    public enum MoveDirection
    {
        right, left, forward, backward
    }
    //方向を変換する際の、角度を決める。その際、今いる車線と、反対車線の全体の本数を決める。
    public enum RoadToRoad
    {
        OneToOne, TwoToOne, ThreeToOne, OneToTwo, TwoToTwo, TwoToThree
    }

    //オブジェクトの横幅、長さを宣言

    public float loadWidth;
    public float lengthObject;

    //直進している時の速度（センサー用）
    private float straightSpeed;

    public float StraightSpeed
    {
        get { return straightSpeed; }
    }    
    //直進している時の最大速度（センサー用）
    private float straightMaxSpeed;

    public float StraightMaxSpeed
    {
        get { return straightMaxSpeed; }
    }

   //カーブ時のスピード（センサー用：現在スピードあるか、ゼロか判断するために使う）
    public float CourveSpeed
    {
        get { return couveSpeed; }
    }
    public float LengthObject
    {
        get { return lengthObject; }
    }
    float angle;
    public float Angle
    {
        get { return angle * Mathf.Rad2Deg; }//ラジアンから度に変える
    }
    private bool state = false;
    public bool State
    {
        get { return state; }
    }



    /*
    //センサーのスクリプトに受け渡し用センサーのスクリプトに受け渡し用
    public float ValueDist
    {
        get { return valueDist; }
    }
    */

    //カーブするスピードを設定
    [SerializeField] private float couveSpeed;


    //動かすオブジェクトを指定
    public GameObject movingObject;
    //車両の長さと横を指定（絶対値で）
    //[SerializeField] private float moingObjectLength;
    //[SerializeField] private float moingObjectWidth;

    //開始位置と終了位置を宣言
    private Vector3 completePosition = Vector3.zero;
    private Vector3 firstPosition = Vector3.zero;

    //繰り返し指定することができる。
    [System.Serializable]
    public class MoveStep
    {   
        public MoveDirection moveDirection;
        public RoadToRoad roadToRoad;

        public float distance;//移動したい距離
        public float MoveTime = 0.5f;//移動する時間

        public float firstTime = 0.0001f;//最高速度に到達するまでの時間
        public float secondTime = 0.0001f;//減速し始める地点から到達地点までの時間    
    }
    //インスペクターに表示。MoveStep型のstepsリストを定義
    [SerializeField] private List<MoveStep> steps;

    //第一引数はステップ
    //第二引数はMoveDirectionを参照して目的の方向を返す場合がTrue。参照せず現在地点の向いている方向を返す場合がFalse。（減速加速→true, カーブの第一段階だったら→false)
    private Vector3 CompletePosition(MoveStep step, float dist, bool type)//移動する方向によって変わる到着地点の座標を求める関数
    {
        //移動前の位置を取得
        //firstPosition = movingObject.transform.position;
        //現在のオブジェクトの方向を取得し宣言
        float rotY = movingObject.transform.eulerAngles.y;


        float addRot = 0;
        switch (step.moveDirection)
        {
            case MoveDirection.left:
                addRot = -90f;
                break;
            case MoveDirection.right:
                addRot = 90f;
                break;
            case MoveDirection.forward:
                addRot = 0;
                break;
            case MoveDirection.backward:
                addRot = -180;
                break;
        }
        if (type)
        {
            rotY += addRot;
        }

        //角度の正規化
        rotY %= 360f;
        if (rotY < 0) { rotY += 360f; }

        ////方向を変える
        if (rotY < 45f || 315f <= rotY)//0度向いている時、南にいく
        {
            Vector3 hogeLeft = new Vector3(0, 0, dist);//Z座標をマイナスに移動
            completePosition = firstPosition + hogeLeft;
        }
        else if (135f <= rotY && rotY < 225f)//180度向いている時、北に行く
        {
            Vector3 hogeRight = new Vector3(0, 0, -(dist));//Z座標をプラスに移動
            completePosition = firstPosition + hogeRight;
        }
        else if (45f <= rotY && rotY < 135f)//90度向いている時、西に行く
        {
            Vector3 hogeForward = new Vector3(dist, 0, 0);//X座標をプラスに移動
            completePosition = firstPosition + hogeForward;
        }
        else if (225 <= rotY && rotY < 315f) //-90とか270とか向いている時、東に行く
        {
            Vector3 hogeBackward = new Vector3(-(dist), 0, 0);//X座標をマイナスに移動
            completePosition = firstPosition + hogeBackward;
        }

        return completePosition;
    }

    /* ----二つのコルーチンを１ステップで呼び出す。---- */
    private void Start()
    {
        StartCoroutine(ProcessSteps());
    }

    IEnumerator ProcessSteps()
    {
        while (true) 
        {
            for (int i = 0; i < steps.Count; i++)
            {                   
                //加速減速の直線運動
                yield return StartCoroutine(MoveOverTime(steps, i));
                //左折、右折あれば、カーブする
                if (steps[i].moveDirection == MoveDirection.left || steps[i].moveDirection == MoveDirection.right)
                {
                    yield return StartCoroutine(Couve(steps[i]));
                }

            }
            if(Luup == false)
            {
                break;
            }

        }
    }

    //カーブを行う関数の宣言:引数に今のステップ数
    IEnumerator Couve(MoveStep step)
    {
        //カーブ始まるとON
        state = true;
        
         //最初の座標を宣言
        Vector3 startCouvePos = movingObject.transform.position;

        //距離 ／ 速さ　→ 車両の長さの半分　／　1フレームに進む距離（速さ）＝　一段階目にかかるフレーム数。そして、Time.deltaTimeをかければ時間になる
        float firstCouveTime = lengthObject / 2 / couveSpeed;

        //カーブする時間　＋　一段階目の時間　を定義
        //時間　＝　距離　／　速さ　→　円周の４分の１　／　速さ　でいいのかな？
        float secondCouveTime = (loadWidth * 2) * 3.14f / 4 / couveSpeed + firstCouveTime;
        //トータルでカーブするのにかかる時間
        float totalCouveTime = secondCouveTime + firstCouveTime;//最初の車両半分進む時間と、最後の車両半分進む時間が等しい場合
        //追加する座標を格納するための変数
        Vector3 couveDist;
        //カーブの半径の円を定義
        float couveRadius = loadWidth;

        float addPos = 0f;
        //第二段階に必要なカーブの座標を求める変数
        float addPosX = 0f, addPosZ = 0f;//それぞれ、sin,cosで求める
        Vector3 couveDistX = new Vector3(0, 0, 0);
        Vector3 couveDistZ = new Vector3(0, 0, 0);

        //座標がマイナスかプラスか、一時保存しつづけておく
        Vector3 firstCouveDistZ = new Vector3(0, 0, 0);
        Vector3 firstCouveDistX = new Vector3(0, 0, 0);

        //経過時間を宣言
        float elapsedCov = 0f;

        //カーブ前のYの角度を取得
        float startY = transform.eulerAngles.y;
        //第一段階終了時点の座標を取得

        //第二段階終了時点の座標を入れる変数を宣言
        Vector3 secondCouvePos = new Vector3(0, 0, 0);

        //第二段階開始の位置が初期位置からどれぐらいずれているのか。進んだ座標以外は0になる。
        Vector3 firstCouvePos = new Vector3(0, 0, 0);

        //二段階終了時点の条件分岐を一度だけにしたい。
        bool secondActivated = true;
        bool firstActivated = true;

        //カーブ前の向き
        Vector3 couveDire = new Vector3(0, 0, 0);

        while (elapsedCov < totalCouveTime)
        {
            elapsedCov += Time.deltaTime;//フレームから次のフレームに進む間のｓ時間　

            //第一段階終了時点の向きを取得
            if (firstCouveTime <= elapsedCov && firstActivated == true)
            {
                firstCouvePos = transform.position;

                //現在正面向いている座標を取得
                couveDistZ = CompletePosition(step, 1, false);//ここで第三引数が必要になってくる
                couveDistX = CompletePosition(step, -1, true);
                firstCouveDistZ = couveDistZ;//一時保存
                firstCouveDistX = couveDistX;//一時保存

                firstActivated = false;//一度きりの処理にする
            }


            //第二段階終了時点 の座標を取得
            if (secondCouveTime <= elapsedCov && secondActivated == true)
            {
                secondCouvePos = movingObject.transform.position;//一時保存

                secondActivated = false;//一度きりの処理にする
            }

            //第一段階、第二段階、第三段階の条件分岐
            if (elapsedCov < firstCouveTime)
            {//第一段階：真っすぐ前に出る
                addPos = elapsedCov * couveSpeed;//時間　×　速さ
                couveDist = CompletePosition(step, addPos, false);//現在車両が向いている方向に進む
                transform.position = startCouvePos + couveDist;//最初の座標　＋　追加する分の座標
            }
            else if (firstCouveTime <= elapsedCov && elapsedCov <= secondCouveTime)
            {//第二段階：角度を変える。座標を変える


                /* 角度を変える。*/
                float completeRotation = (elapsedCov - firstCouveTime) / (secondCouveTime - firstCouveTime);//全体のどこまでの割合進んだか
                completeRotation = Mathf.Clamp01(completeRotation); // 0〜1に制限

                // 現在の角度を線形補間（Lerp）で設定
                float newY = 0;

                if (step.moveDirection == MoveDirection.right)
                {
                    newY = Mathf.Lerp(startY, startY + 90f, completeRotation);
                }
                else 
                {
                    newY = Mathf.Lerp(startY, startY - 90f, completeRotation);
                }

                transform.rotation = Quaternion.Euler(0f, newY, 0f);//角度を代入



                /*座標を変える*/
                //カーブにかかる時間から、現在の時間が何分の１なのかを求め、0 ~ 90のうち現在どこまで進んだかを導き出す。
                angle = (elapsedCov - firstCouveTime) / (secondCouveTime - firstCouveTime) * 90 * Mathf.Deg2Rad;//現在の時間　／　全体の時間　＊　９０（最大角度）

                //各x座標、y座標を求める。角度はangle、ベクトルの長さはcouveRadius、

                    addPosX = couveRadius * Mathf.Cos(angle) - couveRadius * Mathf.Cos(0 * Mathf.Deg2Rad);
                    addPosZ = couveRadius * Mathf.Sin(angle) - couveRadius * Mathf.Sin(0 * Mathf.Deg2Rad);

                //追加する座標　＝　追加する距離　＊　1か-1が入っている座標。
                /*
                 * X座標とZ座標の両方に「１」か「-1」が入っている。
                 * sin(angle)が90度、また270度の方向なら、右折ならCos(angle)は（-1,0,?）でZ座標の「-1」に掛ける。左折ならCos(angle)は（1,0,?）でZ座標の「1」に掛ける
                 * sin(angle)が0度、また180度の方向なら、右折ならCos(angle)は（?,0,1）でZ座標の「1」に掛ける。左折ならCos(angle)は（?,0,-1）でZ座標の「-1」に掛ける
                 * *「？」はsin()がどっち向いているかを示している。「1」か「-1」が入る。
                 */

                if (Mathf.Abs(firstCouveDistZ.z) == 1)
                {
                    couveDistX.x = addPosX * firstCouveDistX.x;
                    couveDistZ.z = addPosZ * firstCouveDistZ.z;
                }
                else 
                {
                    couveDistX.z = addPosX * firstCouveDistX.z;
                    couveDistZ.x = addPosZ * firstCouveDistZ.x;
                }

             

                couveDist = couveDistX + couveDistZ;//追加したい座標をcouveDistに統合する。（addPosX,0,addZ) 
                transform.position = couveDist + firstCouvePos;//最初の座標　＋　追加する分の座標
            }
            else if (secondCouveTime < elapsedCov)
            {
                //第一段階と移動する距離は同じ。向きが違う。
                addPos = (elapsedCov - secondCouveTime) * couveSpeed;//時間　×　速さ


                //二段階最終の座標に、前に進んだ距離を加算
                transform.position = CompletePosition(step, addPos, false) + secondCouvePos;//Vector3に変換、この現在オブジェクトが向いている向きとステップの中にある方向を参照
                
            }


            yield return null;
        }
    }



    IEnumerator MoveOverTime(List<MoveStep> steps, int i)//1フレームごとに呼び出される
    {

        //直線始まるとOFF
        state = false;


        MoveStep step = steps[i];

  

        //初期位置から移動した距離
        float valueDist = 0;

        //エラー回避：MoveTime　< FirstTime + SecondTime の時、エラーが発生してしまう。
        //加速する時間と、減速する時間の合計が全体移動時間を超えてしまうと、後のVector3.Lerpの第三引数で負の値を入れることになりエラー発生する
        if (step.MoveTime < (step.firstTime + step.secondTime))
        {
            step.firstTime = step.MoveTime / 2;
            step.secondTime = step.MoveTime / 2;
        }


        /* 直線に走るように、オブジェクトを真っすぐにする。 */
        float rotationY = Mathf.Round(transform.eulerAngles.y / 10f) * 10f;
        transform.rotation = Quaternion.Euler(0f, rotationY, 0f);//角度を代入

        //加速減速する関数
        Vector3 targetPos = CompletePosition(step,step.distance, false) + movingObject.transform.position;//オブジェクトの最後の位置。step.MoveDirectionからカーブ後の向きに距離を加算したいため、第三引数はTrueに

        //初速度をあらわす変数を宣言
        float nextFirstSpeed = 0f;
        if((i + 1 < steps.Count) && (steps[i + 1].moveDirection == MoveDirection.right || steps[i + 1].moveDirection == MoveDirection.left)) 
        {
            nextFirstSpeed = couveSpeed;
        }

        //ステップを進める前の最初の座標
        Vector3 startPos = movingObject.transform.position;


        //初速度を表す変数
        float completeFirstSpeed = 0f;

        if(step.moveDirection == MoveDirection.right || step.moveDirection == MoveDirection.left)
        {
            completeFirstSpeed = couveSpeed;
        }

        //移動時間の間、処理をし続けるループ
        float elapsed = 0f;

        //時間から最高速度を求める
        //最高速度
        //float maxSpeed = step.distance * 2 / (step.MoveTime * 2 - step.firstTime - step.secondTime);
        float maxSpeed = (step.distance - ((step.secondTime * nextFirstSpeed) / 2) - ((step.firstTime * completeFirstSpeed) / 2)) / (step.MoveTime - (step.firstTime / 2f) - (step.secondTime / 2f));

        //直進時の最大速度(センサー用）
        straightMaxSpeed = maxSpeed;


        //最高速度　＊　時間　/ ２　で加速・減速している間の距離
        float firstDist = (maxSpeed + completeFirstSpeed) * step.firstTime / 2;
        float secondDist = (maxSpeed + nextFirstSpeed) * step.secondTime / 2;

        while (elapsed < step.MoveTime)
        {

            //stepの間の経過した時間
            elapsed += Time.deltaTime;//フレームから次のフレームに進む間のｓ時間


            //条件分岐について加速するゾーン、等速運動するゾーン、減速するゾーンにわけて考える
            if (elapsed < step.firstTime)//加速するゾーン
            {
                //valueDist = elapsed * elapsed * (maxSpeed / step.firstTime / 2);
                //valueDist = elapsed * elapsed * (maxSpeed / step.firstTime / 2);
                valueDist = completeFirstSpeed * elapsed + elapsed * elapsed * ((maxSpeed - completeFirstSpeed) / step.firstTime / 2);
                //現在速度も求める（センサーで使用する）
                straightSpeed = completeFirstSpeed + ((maxSpeed - completeFirstSpeed) / step.firstTime) * elapsed;//現在速度を求める。初速度＋速度変化/時間
            }
            else if (step.firstTime <= elapsed && elapsed <= (step.MoveTime - step.secondTime))//等速運動するゾーン
            {
                valueDist = firstDist + maxSpeed * (elapsed - step.firstTime);
                straightSpeed = maxSpeed;//現在速度を求める
            }
            else if ((step.MoveTime - step.secondTime) < elapsed)//減速するゾーン
            {
                float elapsed2 = elapsed - (step.MoveTime - step.secondTime);
                valueDist = (step.distance - secondDist) + maxSpeed * elapsed2 + (elapsed2 * elapsed2 * ((nextFirstSpeed - maxSpeed) / step.secondTime) / 2);//次のステップ次第で、加速度が変化する。加速度　＝　(最終速度 - 初速度) /(終了時間 - 開始時間)
                straightSpeed = maxSpeed + (nextFirstSpeed - maxSpeed) / step.secondTime * elapsed2;//現在速度を求める
            }

            
            //全体の距離から、現地点の進んだ距離の割合から、Vector3の座標を求める。
            movingObject.transform.position = Vector3.Lerp(startPos, targetPos, valueDist / step.distance);



            yield return null;
        }
        movingObject.transform.position = targetPos;//ワンステップの処理終了



    }
}
