# 起床演出の設定

## シーンへの追加

1. 空オブジェクト `WakeUpIntro` に `WakeUpIntroController` を追加する。シーン開始時から有効にする。
2. 空オブジェクト `WakeUpStartPoint` を枕付近の目の位置に置き、`Start Point` に登録する。開始時は自動で真上（ワールドの＋Y方向）を向いて目を開く。目印のY回転で、その後に視線を下ろす正面方向を指定する。
3. 空オブジェクト `PlayerEndPoint` を立ち上がり後の足元に置き、操作開始時の向きを設定する。`Player End Point` に登録する。目印はプレイヤーの子にしない。
4. `Player` に操作キャラのPlayerMovement、`Camera Switch` に既存のCameraSwitch、`Brain` にMain CameraのCinemachineBrain、`Eye Blink` に既存のEyeBlinkを登録する。
5. `Futon Animator` に布団のAnimatorを登録する。`Futon Bool` は `FutonWakeUp` のまま。初期値false、再生クリップはループ無効にする。
6. `Pause During Intro` に通常のマウス・カメラ入力（Cinemachine Input Axis Controllerを含む）、ドア操作など、演出中に止めたいコンポーネントを登録する。PlayerMovementは自動停止する。Brain、EyeBlink、布団Animator、カメラ本体は止めない。
7. 操作キャラ配下のMeshRendererとSkinnedMeshRendererは自動で非表示になる。追加で隠すRendererは `Hide During Intro` に登録する。三人称へのブレンドが完全に終了してから元の表示状態へ戻る。
8. 操作キャラ本体は初めからPlayerEndPointに置くと、足音の距離計測や周辺判定への影響を抑えられる。ベッド内への配置は不要。

演出用Cinemachineカメラは実行時に自動生成する。レンズ設定は通常の三人称カメラからコピーする。終了すると削除する。通常カメラが複数ある場合は、三人称カメラ（優先度10）より高いカメラが演出終了時に有効にならないようにする。

## 布団の終了通知

現在は `Futon State Name`（初期値：`Base Layer.FutonWakeUp`）と `Futon Layer`（初期値：0）で指定したステートの再生完了を自動検知します。Loop Timeが無効で、最後まで再生されるステートならAnimation Eventは不要です。以下のイベント通知は任意で併用できます。完了前に別ステートへ遷移する構成ではイベント通知を使用してください。

1. 布団のAnimatorと**同じGameObject**に `FutonWakeUpEvents` を追加する。
2. `Intro` にWakeUpIntroを登録する。
3. Animationウィンドウで布団を上げるクリップの終了地点にAnimation Eventを置く。
4. 関数は `NotifyFutonFinished` を指定する。状態遷移でイベント位置より先にクリップを抜けないようにする。

通知が届かない場合、Futon Timeout秒後にConsoleへエラーを出し、操作を復元して演出を終了する。

## Stepsの編集

リストは上から順に再生する。要素を追加・削除・ドラッグで並べ替えられる。

| 項目 | 意味 |
| --- | --- |
| Name | ステップの名前 |
| Duration | 動作の秒数。0なら即時反映 |
| Easing | 加減速のカーブ。横軸0〜1が時間、縦軸0〜1が進行率 |
| Move | 移動を有効にする |
| Position Target | 移動先の目印。ステップ開始時のワールド座標を使う |
| Local Move | 移動先未指定時の相対移動。X=右、Y=上、Z=前。前後左右はステップ開始時の水平な向きが基準 |
| Rotate | 回転を有効にする |
| Rotation Target | 回転先の目印。ステップ開始時のワールド回転を使う |
| Rotation Delta | 回転先未指定時の相対角度。Y=-90で左90度、Xが正で視線を下げる。1ステップは180度未満を推奨 |
| Change Eyes | まぶたの変更を有効にする |
| Eye Open | 到達する開眼量。0=閉眼、2=全開、中間値=半開き |
| Start Futon | ステップ開始時に布団Boolをtrueにする |
| Wait For Futon | 動作時間の終了後、布団の終了通知まで待つ。既に通知済みなら即座に次へ進む |
| Futon Timeout | 終了通知を追加で待つ最大秒数 |
| Switch To Third Person | 最後のステップだけで使う。Duration秒のEaseInOutブレンドで三人称へ移行 |

移動・回転・開眼は同じDurationで**同時に**進む。別々の秒数にしたい場合はステップを分ける。何も有効にしなければ待機になる。

まばたきを追加する場合は「Change Eyes=true、Eye Open=0、Duration=0.15」の次に「Change Eyes=true、Eye Open=2、Duration=0.15」を置く。閉眼を保ちたい場合は間に待機ステップを置く。起床演出から怪物のまばたき通知は発生しない。

初期リストは閉眼待機→開眼→視線を下げる→布団→左90度→前上へ移動→左90度と三人称切替。数値は仮の値なのでベッドに合わせて調整する。三人称カメラ側の追従先・構図・向きも操作開始時に合うよう設定する。最後の旋回は演出カメラに適用され、三人称カメラの構図へブレンドされる。操作キャラの最終方向はPlayerEndPointで指定する。

## Play Modeで確認する項目

- 開始直後に閉眼し、ベッドの位置から目を開く。
- 演出中に移動・視点・ドア入力が割り込まない。
- 布団の終了後に次のステップへ進む。
- 立ち上がり時に壁・布団へ視点が入り込まない（演出カメラに衝突回避はない）。
- 三人称への移行時に体が視点を遮らず、終了後に操作できる。
- 通常のEキー＋左クリックによるまばたきが引き続き動く。

シーンへの参照登録とPlay Modeでの見た目確認は別途必要。
