using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class MonsterExistence : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private Transform player;
    [SerializeField] private PlayerFootstep playerFootstep;
    [SerializeField] private GameObject enemy;
    [SerializeField] private LayerMask groundLayer;

    [Header("追跡設定")]
    [SerializeField] private float startDistance = 10f;
    [SerializeField] private float moveDistance = 2f;
    [SerializeField] private float teleportInterval = 3f;
    [SerializeField] private float catchDistance = 0.5f;
    [SerializeField] private LayerMask obstacleLayer;

    [Header("接近速度")]
    [SerializeField] private float approachSpeed = 0.5f;

    [Header("ゲームオーバー")]
    [SerializeField] private GameObject gameOverPanel;

    [Header("まばたきの攻撃")]
    [SerializeField] private EyeBlink eyeBlink;
    [SerializeField] private CameraSwitch cameraSwitch;
    [SerializeField] private Camera firstPersonCamera;

    [SerializeField] private float materializedChaseSpeed = 4f;
    //[SerializeField] private float turnSpeed = 540f;
    [SerializeField] private LayerMask sightObstacleLayer = ~0;

    [Header("NavMesh追跡")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private float navMeshSearchRadius = 3f;

    // 捕捉成功後、暗転が終わるのを待っている状態
    private bool isMaterializationPending;

    // 暗転後に敵を置く予定のNavMesh上の座標
    private Vector3 pendingNavMeshPosition;

    private bool isMaterialized;
    public bool IsMaterialized
    {
        get { return isMaterialized; }
    }
    private Coroutine chaseRoutine;

    private float currentDistance;
    private bool isChasing = false;

    //private bool ChasingStart = false;


    private void Start()
    {
        // ゲーム開始時は敵を非表示
        enemy.SetActive(false);

        // 足音を待つ
        StartCoroutine(WaitForFootstep());
    }

    private IEnumerator WaitForFootstep()
    {
        // 足音が鳴るまで待つ
        yield return new WaitUntil(() => playerFootstep.HearingSound);

        Debug.Log("足音を検知！");

        StartChase();
    }

    private void StartChase()
    {
        // すでに追跡中なら何もしない
        if (isChasing)
            return;

        isChasing = true;

        // 最初はプレイヤーから10m離れた場所
        currentDistance = startDistance;

        // 敵を出現させる
        enemy.SetActive(true);

        // 敵を10m地点に移動
        MoveEnemy(currentDistance);

        Debug.Log("敵が出現しました！");

        // NavMeshAgentの追跡をまだ開始しないよう停止する
        //agent.isStopped = true;

        // 追跡開始
        // 後で具現化時に止められるよう、コルーチンを保存する
        chaseRoutine = StartCoroutine(ChaseRoutine());
    }

    private IEnumerator ChaseRoutine()
    {
        while (isChasing)
        { // 敵が少しずつ主人公へ近づく
            float elapsedTime = 0f;

            while (elapsedTime < teleportInterval)
            {
                elapsedTime += Time.deltaTime;

                // 敵を主人公へ少しずつ近づける
                ApproachPlayer();

                // 敵と主人公の距離を確認
                float distance = Vector3.Distance(enemy.transform.position, player.position);

                // 近づきすぎたらゲームオーバー
                if (distance <= catchDistance)
                {
                    CatchPlayer();
                    yield break;
                }

                yield return null;
            }
            // 一定時間経過したら2m詰める
            currentDistance = Mathf.Floor(currentDistance / moveDistance) * moveDistance;

            // プレイヤーとの距離を2m縮める
            currentDistance -= moveDistance;

            // 捕まる距離まで来たらゲームオーバー
            if (currentDistance <= catchDistance)
            {
                currentDistance = 0f;

                MoveEnemy(currentDistance);

                CatchPlayer();

                yield break;
            }

            // 新しい位置へ瞬間移動
            MoveEnemy(currentDistance);

            Debug.Log("敵がテレポート！ 距離：" + currentDistance);
        }
    }

    //瞬間移動
    private void MoveEnemy(float distance)
    {
        Collider col = enemy.GetComponentInChildren<Collider>();

        //出現可能な位置を探す
        for (int i = 0; i < 100; i++)
        {
            // プレイヤーの周囲からランダムな方向を選ぶ
            Vector2 randomDirection = Random.insideUnitCircle.normalized;

            Vector3 offset = new Vector3(randomDirection.x,0f,randomDirection.y) * distance;

            Vector3 newPosition = player.position + offset;

            // 地面がない場所は使わない
            if (!Physics.Raycast(newPosition + Vector3.up * 5f,Vector3.down,out RaycastHit hit,10f,groundLayer))
            {
                continue;
            }

            // 建物・壁・車などと重なる場所は使わない
            if (Physics.CheckBox(newPosition,col.bounds.extents,enemy.transform.rotation,obstacleLayer))
            {
                continue;
            }
            //// テレポート候補の近くにあるNavMeshを探す
            //if (!NavMesh.SamplePosition(newPosition,out NavMeshHit navHit,navMeshSearchRadius,NavMesh.AllAreas))
            //{
            //    // NavMeshが近くにない候補は使わず、次のランダム位置を試す
            //    continue;
            //}

            //// 幽霊状態でも、テレポート先はNavMesh上にしておく
            //newPosition = navHit.position;

            // 幽霊状態の瞬間移動
            enemy.transform.position = newPosition;
            return;
        }

        Debug.LogWarning("敵が出現できる場所を見つけられませんでした。");
    }

    //操作キャラへ少し移動する。
    private void ApproachPlayer()
    {
        // 主人公の方向を計算
        Vector3 direction = (player.position - enemy.transform.position).normalized;

        // 敵を主人公へ少しずつ移動
        enemy.transform.position += direction * approachSpeed * Time.deltaTime;
    }
    //敵に捕まる
    private void CatchPlayer()
    {
        isChasing = false;

        Debug.Log("ゲームオーバー！");

        // 画面を真っ黒にする
        gameOverPanel.SetActive(true);


        if (agent.enabled)
        {
            agent.enabled = false;
        }

        enemy.SetActive(false);
    }

    /* ---- まばたきの判定 ---- */

    // スクリプトが有効になったとき、まばたき完了イベントを受け取る
    private void OnEnable()
    {
        eyeBlink.BlinkClosed += OnBlinkClosed;
        eyeBlink.LongBlinkFinished += StartMaterializedChase;
        //eyeBlink.ChaseStart += ChaseStart;
    }

    // スクリプトが無効になったとき、イベント登録を解除する
    private void OnDisable()
    {
        eyeBlink.BlinkClosed -= OnBlinkClosed;
        eyeBlink.LongBlinkFinished -= StartMaterializedChase;
    }

    // まばたきで目を閉じた瞬間に呼ばれる
    // 敵をまばたき内に収めることが成功か、失敗かの判断をここで行う。
    private void OnBlinkClosed()
    {
        // 追跡開始前、具現化済み、または暗転待機中なら何もしない
        if (!isChasing || isMaterialized || isMaterializationPending)
            return;

        // 一人称視点で敵を見ていなければ具現化しない
        if (!IsEnemyVisibleToPlayer())
            return;

        /*
        // テレポート処理を停止する
        if (chaseRoutine != null)
        {
            StopCoroutine(chaseRoutine);
            chaseRoutine = null;
        }
        */

        // 現在位置付近のNavMeshを探す
        if (!NavMesh.SamplePosition(enemy.transform.position,out NavMeshHit hit,navMeshSearchRadius,NavMesh.AllAreas))
        {
            Debug.LogWarning("敵の現在位置の近くに NavMesh がありません。");
            return;
        }

        // 敵をNavMesh上へ置いてから、Agentを有効化する

        //
        pendingNavMeshPosition = hit.position;

        // NavMesh上へ置けた後だけAgentを有効にする
        agent.enabled = true;

        // ここから暗転待機状態にする
        isMaterializationPending = true;

        // Agentが有効かつNavMesh上にいることを確認する
        //if (!agent.isOnNavMesh)
        //{
        //    Debug.LogWarning("敵を NavMesh 上へ配置できませんでした。");
        //    agent.enabled = false;
        //    return;
        //}

        // ここまで成功して初めて具現化・追跡状態にする
        // 暗転の演出が終了次第具現化・追跡状況にする。

        if (chaseRoutine != null)
        {
            StopCoroutine(chaseRoutine);
            chaseRoutine = null;
        }
        // EyeBlinkへ「今回だけ長く暗転して」と伝える
        eyeBlink.RequestLongBlink();
        /*
        agent.speed = materializedChaseSpeed;
        agent.stoppingDistance = 0f;
        agent.isStopped = false;
        */
    }


    // EyeBlinkの長い暗転が終わり、目を開いた後に呼ばれる
    private void StartMaterializedChase()
    {
        // 捕捉成功したまばたき以外では何もしない
        if (!isMaterializationPending)
            return;

        isMaterializationPending = false;



        // 保存しておいたNavMesh上の座標へ敵を移動する
        enemy.transform.position = pendingNavMeshPosition;

        // NavMeshAgentを有効にする
        agent.enabled = true;

        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning("敵を NavMesh 上へ配置できませんでした。");
            agent.enabled = false;
            return;
        }

        // ここで初めて具現化・追跡状態にする

        isMaterialized = true;
        //NavMeshAgentを有効化して追跡を開始
        agent.enabled = true;
        agent.speed = materializedChaseSpeed;
        agent.stoppingDistance = 0f;
        agent.isStopped = false;
     

    }

    // 敵が画面内にあり、壁などで隠れていないか調べる
    private bool IsEnemyVisibleToPlayer()
    {
        // 一人称視点でない場合は、敵を見たことにしない
        if (!cameraSwitch.FirstPersonMode || firstPersonCamera == null)
            return false;

        // 敵のCollider中心を、画面内判定に使う
        Collider enemyCollider = enemy.GetComponentInChildren<Collider>();
        Vector3 target = enemyCollider != null
            ? enemyCollider.bounds.center
            : enemy.transform.position;

        // 敵の位置を、画面上の座標へ変換する
        Vector3 viewport = firstPersonCamera.WorldToViewportPoint(target);

        // x・yが0～1なら画面内、zが正ならカメラの前方にいる
        bool isInScreen =
            viewport.z > 0f &&
            viewport.x >= 0f && viewport.x <= 1f &&
            viewport.y >= 0f && viewport.y <= 1f;

        if (!isInScreen)
            return false;

        // カメラから敵へRayを飛ばして、間に遮蔽物があるか調べる
        Vector3 origin = firstPersonCamera.transform.position;
        Vector3 direction = target - origin;
        float distance = direction.magnitude;

        // 最初に当たったColliderが敵自身なら、壁などに隠れていない
        if (Physics.Raycast(origin,direction.normalized,out RaycastHit hit,distance,sightObstacleLayer,QueryTriggerInteraction.Ignore))
        {
            return hit.collider.transform.IsChildOf(enemy.transform);
        }

        return false;
    }
    private void Update()
    {
        // 敵が出現済みで、まばたきにより具現化した後だけ追跡する
        if (!isChasing || !isMaterialized || isMaterializationPending)
            return;

        ChasePlayer();

        // 敵と主人公が近すぎたらゲームオーバー
        if (IsPlayerCaught())
        {
            CatchPlayer();
            return;
        }
        ChasePlayer();
    }

    // NavMeshを使って、壁や障害物を避けながら主人公を追跡する
    private void ChasePlayer()
    {
        // Agentが有効かつNavMesh上にいるときだけ追跡する
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        agent.speed = materializedChaseSpeed;
        agent.stoppingDistance = catchDistance;

        // プレイヤーまでの経路を計算して追跡する
        agent.SetDestination(player.position);
    }
    /*
    private void ChaseStart()
    {
        ChasingStart = true;
        return;
    }
    */

    // 高低差ではなく、地面上での敵と主人公の距離を調べる
    private bool IsPlayerCaught()
    {
        Vector3 difference = player.position - enemy.transform.position;

        // 坂・段差などのY座標差は無視する
        difference.y = 0f;

        return difference.sqrMagnitude <= catchDistance * catchDistance;
    }
}