using UnityEngine;

// AnimatorのAnimation Eventを別オブジェクトの起床演出へ中継する。
public class FutonWakeUpEvents : MonoBehaviour
{
    [Tooltip("起床演出を管理するWakeUpIntroControllerを登録します。この中継スクリプトは布団のAnimatorと同じGameObjectに付けてください。布団の終了通知を登録先へ送ります。")]
    [SerializeField] private WakeUpIntroController intro;

    // 布団を上げるクリップの最後に、この名前のAnimation Eventを設定する。
    public void NotifyFutonFinished()
    {
        if (intro != null) intro.NotifyFutonFinished();
    }
}
