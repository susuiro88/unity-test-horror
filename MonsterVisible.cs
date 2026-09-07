using UnityEngine;

public class MonsterVisible : MonoBehaviour
{
    [SerializeField] private CameraSwitch cameraSwitch;
    // InspectorからCameraSwitchを指定
    // 今回はPlayerについているCameraSwitchをここに指定する

    private Renderer[] renderers;
    // 敵のRendererを複数まとめて保存するための変数
    // Rendererは「3Dモデルを画面に表示するための部品」
    // []が付いているので、複数のRendererを入れられる


    private void Start()
    {
        // 子オブジェクトも全部取得
        renderers = GetComponentsInChildren<Renderer>();
        // このMonsterVisibleが付いているオブジェクトと、
        // その子オブジェクトに付いているRendererを全部取得する
        //
        // 例えば、
        //
        // enemy_test
        // ├─ Body       ← Renderer
        // ├─ Head       ← Renderer
        // └─ Arm        ← Renderer
        //
        // のような構造なら、Body・Head・ArmのRendererを
        // まとめてrenderersに保存する
    }


    private void Update()
    // ゲーム中、毎フレーム繰り返し実行される
    {
        // 一人称なら表示
        if (cameraSwitch.FirstPersonMode)
        // CameraSwitchのFirstPersonModeがtrueか確認する
        //
        // true  → 一人称視点
        // false → 三人称視点
        {
            SetVisible(true);
            // 敵のRendererを表示する
        }

        // 三人称なら非表示
        else
        // FirstPersonModeがfalseだった場合
        // つまり三人称視点の場合
        {
            SetVisible(false);
            // 敵のRendererを非表示にする
        }
    }


    private void SetVisible(bool visible)
    // 敵を表示するか非表示にするかを決めるためのメソッド
    // visibleにはtrueまたはfalseが入る
    {
        foreach (Renderer r in renderers)
        // renderersに保存されているRendererを1つずつ取り出す
        {
            r.enabled = visible;
            // Rendererの表示・非表示を切り替える
            //
            // visible = true
            // → 表示する
            //
            // visible = false
            // → 非表示にする
        }
    }
}