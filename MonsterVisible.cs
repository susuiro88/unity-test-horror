using UnityEngine;

public class MonsterVisible : MonoBehaviour
{
    [SerializeField] private CameraSwitch cameraSwitch;
    [SerializeField] private MonsterExistence monsterExistence;
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
    }


    private void Update()
    // ゲーム中、毎フレーム繰り返し実行される
    {
        // 具現化後は、一人称・三人称のどちらでも敵を表示する
        if (monsterExistence.IsMaterialized)
        {
            SetVisible(true);
            return;
        }
        // 幽霊状態では、一人称視点のときだけ敵を表示する
        SetVisible(cameraSwitch.FirstPersonMode);
    }


    private void SetVisible(bool visible)
    // 敵を表示するか非表示にするかを決めるためのメソッド
    {
        foreach (Renderer r in renderers)
        // renderersに保存されているRendererを1つずつ取り出す
        {
            r.enabled = visible;
            // Rendererの表示・非表示を切り替える

        }
    }
}