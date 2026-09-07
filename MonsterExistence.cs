using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

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

    private float currentDistance;
    private bool isChasing = false;

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

        // 追跡開始
        StartCoroutine(ChaseRoutine());
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
        // 最大20回まで安全な場所を探す
        for (int i = 0; i < 100; i++)
        { 
        
        
            // ランダムな方向を決める
            Vector2 randomDirection = Random.insideUnitCircle.normalized;

            Vector3 offset = new Vector3(randomDirection.x, 0f, randomDirection.y) * distance;

            // プレイヤーから指定距離の場所
            Vector3 newPosition = player.position + offset;

            // 敵の上から下に向かってRayを飛ばす
            Ray ray = new (newPosition + Vector3.up * 5f, Vector3.down);

            RaycastHit hit;

            // 地面を見つける
            if (Physics.Raycast(ray, out hit, 10f, groundLayer))
            {
                // 敵のColliderを取得
                Collider col = enemy.GetComponentInChildren<Collider>();

                // 出現予定位置に建物などがあるか確認
                bool blocked = Physics.CheckBox( newPosition, col.bounds.extents, enemy.transform.rotation, obstacleLayer);

                if (blocked)
                {
                    continue;
                }

                // 地面に立つようにY座標を調整
                newPosition.y = hit.point.y + col.bounds.extents.y;
            }
            // 敵を移動
            enemy.transform.position = newPosition;
        }

            
    }

    //操作キャラへ少し移動する。
    private void ApproachPlayer()
    {
        // 主人公の方向を計算
        Vector3 direction = (player.position - enemy.transform.position).normalized;

        // 敵を主人公へ少しずつ移動
        enemy.transform.position += direction * approachSpeed * Time.deltaTime;
    }

    private void CatchPlayer()
    {
        isChasing = false;

        Debug.Log("ゲームオーバー！");

        // 画面を真っ黒にする
        gameOverPanel.SetActive(true);

        // 敵を消す
        enemy.SetActive(false);
    }

}