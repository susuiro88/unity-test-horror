# 汎用イベントの設定

`EventSequenceController`は、起床演出の「ステップを順番に実行する」構成を応用した別スクリプトです。`WakeUpIntroController`の設定・処理は変更していません。

## 配置と開始方法

1. 空のGameObject（例：`LeaveHomeEvent`）に`EventSequenceController`を追加します。演出中に消さない場所へ置きます。
2. `Player`に通常操作の`PlayerMovement`、`Camera Switch`に既存の`CameraSwitch`、`Brain`に実際のMain Cameraの`CinemachineBrain`を登録します。
3. `Initial View`で`First Person`（主観）／`Third Person`（三人称）を選びます。両カメラは普段と同じようにプレイヤーを追従する設定が必要です。
4. `Steps`を追加します。種類に関係する項目だけInspectorに表示されます。
5. 調べて開始する場合は、靴などの`DoorInteraction`の**Event Sequence**に登録します。登録時はMotion Stepsと開閉イベントを使わず、このイベントへ操作を渡します。条件不足でも再調査できます。一度きりかどうかはイベント側の`Play Once`が管理します。
6. 範囲や別のイベントからは、UnityEventに`EventSequenceController.Play()`を登録できます。`Play On Start`はシーン開始時から再生したいときだけオンにします。起床演出が動いている間の要求は受け付けず、予約もしません。

元の起床コンポーネントを追加で付ける必要はありません。靴には既存の発光・近接判定を残してください。既存DoorInteractionのEvent Sequenceが空欄なら従来の動作のままです。

## ステップの種類

| Type | 動作・時間 |
|---|---|
| Wait | Duration秒待つ |
| Camera Look | 視点切替・視線変更・歩行／走行をまとめて実行。Move Playerをオンにすると移動と視線変更が同時に進む |
| Object Transform | Targetの位置・回転・拡大をDuration秒で変更。同じステップ内の3操作は同時進行 |
| Renderer Visibility | 対象のMeshRenderer／SkinnedMeshRendererを即時表示・非表示にしてDuration秒待つ |
| Play Audio | Audio SourceでClipを再生してDuration秒待つ。Wait For Audioならさらに音声終了を待つ |
| Stop Audio | Audio Sourceを停止してDuration秒待つ |
| Fade | Fade Imageを指定AlphaへDuration秒かけて変更。1が黒、0が透明 |
| Event | On StartedのUnityEventを呼び、Duration秒待つ。Animator.SetTriggerなどを接続可能 |

全種類で`Delay`は実行前の待機秒数、`On Started`は待機後の開始時、`On Completed`は動作終了時の通知です。`Duration = 0`は即時完了です。各ステップは順番に実行します。音声を動作と重ねたい場合はPlay AudioのDurationを0、Wait For Audioをオフにして、次に移動などを配置します。

## カメラの向きを変える（Camera Look）

Type=`Camera Look`を追加し、Camera ModeでFirst Person（一人称）／Third Person（三人称）／Keep Current（現在のモードを維持）を選びます。指定のモードへ切り替えて、視線変更とMove Playerによる移動を同時に開始します。Delayは両方が始まる前の待機です。Use Movement SpeedがオフならDurationが共通の動作時間で、0なら即時です。Easingが空なら等速、Ease In Outのカーブなら始めと終わりをゆっくりにできます。

旧Cameraと旧Player Moveは種類の選択肢から削除しました。既存データは読み込み時にCamera Lookへ自動変換します。旧Cameraは「視点切替のみ」、旧Player Moveは「現在のモードを維持＋Move Playerオン＋視線変更なし」とし、時間・目印・通知を引き継ぎます。既存のCamera Lookは移動なしのままです。別々の旧ステップを自動で1つにはまとめないため、同時動作にしたい場合はCamera Lookへ移動設定をまとめ、不要な後続の移動ステップを削除してください。

`Look Direction`で正面の基準を選びます。

- `Level Current View`：今の左右方向を保ち、見下ろし・見上げだけを水平に戻す。靴から顔を上げる場合の基本設定です。
- `Player Forward`：キャラクター本体の正面へ向く。観察カメラだけが横を向いていた場合は、左右の向きも戻ります。
- `Direction Marker`：Look Direction Targetに指定した空オブジェクトの青いZ軸方向へ向く。ドアの方向を確実に向かせたい場合に使用します。目印のY回転だけを使い、目印の位置・X/Z回転は使いません。
- `Numeric Angles`：Look RotationにXYZの角度を度数で指定。Look Relativeで相対／絶対を切り替えます。
- `Look At Target`：Look Direction Targetで指定したオブジェクトの位置を見つめ、画面中央へ近づけます。移動中も追従位置を反映し、動作終了時には対象の中心を捉えます。
- `Keep Current`：新たな視線変更を行いません。切替だけ／移動だけに使います。すでに設定した注視や角度保持は継続します。

### 移動しながらドアを見る設定例

| 項目 | 設定 |
|---|---|
| Type | Camera Look |
| Camera Mode | First Person |
| Look Direction | Look At Target |
| Look Direction Target | ドアのオブジェクト、または見たい点に置いた空オブジェクト |
| Move Player | オン |
| Movement Style | Walk |
| Player Destination | ドア手前の足元位置の目印 |
| Use Movement Speed | オフ |
| Duration | 2秒 |

これで歩き出すのと同時に視線をドアへ向け、2秒で移動と回転が完了します。対象は表示中のMeshRenderer／SkinnedMeshRenderer（子も含む）のBounds中心を狙います。メッシュのない空オブジェクトならその位置を狙うため、顔やドアノブなど特定の点を中央にしたい場合は空の目印が確実です。Look Target Offsetでも対象のローカル座標基準で注視点を補正できます。

Direction Markerは目印の「向き」、Look At Targetは対象の「位置」を使います。Look At Targetは上下も回転します。最初から中心へ向いている場合を除き、移動の途中はDurationに沿って徐々に中央へ近づけます。ステップ終了後も演出中は対象を追い、次に別の視線を指定するか演出を終了すると解除されます。壁による遮蔽を除いたり対象を画面に収まる大きさへズームしたりする機能ではありません。

Numeric Anglesの場合：

| 項目 | 意味 |
|---|---|
| Look Rotation X | 上下。相対で正の値は下、負の値は上へ向ける |
| Look Rotation Y | 左右。相対で+90は右へ90度、-90は左へ90度 |
| Look Rotation Z | カメラの傾き（ロール） |
| Look Relative オン | ステップ開始時の向きから追加回転。Yはワールドの上方向、X/Zは開始視点のローカル軸を基準に回す |
| Look Relative オフ | ワールド基準の到着角度。例：(0,90,0)は水平でワールド+X方向を向く |

相対回転は+270度や360度もその回転量で実行します。絶対回転は最短の回転で指定姿勢へ向きます。複数軸を同時に回す場合、相対指定は単純なInspectorのEuler角への加算とは異なります。

靴から顔を上げるだけなら、靴の非表示の後へCamera Mode=First Person、Look Direction=Level Current View、Move Player=オフのステップを追加します。Level Current View／Player Forward／Direction Markerは最後に水平になります。

三人称は既定では通常の位置追従を維持し、視線だけを回転させます。下記の位置指定を使うと、頭上のカメラを下げたり、背後へ配置したりできます。内部のCinemachine拡張は自動で追加・解除されるため、手動で付ける必要はありません。位置指定・角度は演出中（Hold Control On Completionの保持中も含む）維持します。終了・中断後は通常のCinemachineの追従・注視へ戻り、一人称の操作に戻った後は通常の上下角度制限が適用されます。

### 三人称カメラの位置とプレイヤーの中央配置

Camera ModeがThird PersonまたはKeep Currentのとき、`Third Person Position Mode`を表示します。Keep Currentで実際の視点が一人称なら位置指定は適用しません。

| Position Mode | 動作 |
|---|---|
| Keep Current | 位置制御を変更しない。以前のステップで位置を指定していればその追従を維持し、未指定なら通常のCinemachine追従を使う |
| Player Relative | Third Person PositionのXYZで、プレイヤーの足元からの位置を指定する |
| Follow Camera | イベント用の位置指定を解除し、通常のCinemachineの位置へ戻す。視線は別なので、プレイヤーを見続けるならLook At Playerを選ぶ |

`Third Person Position`はXが右、Yが上、Zが前です。例 `(0, 1.5, -3)` は足元から高さ1.5、背後3の位置です。通常カメラへの加算量やワールド座標ではありません。キャラの水平な向きを基準にするため、歩行やFace Movementによる旋回にも追従します。位置・視線・プレイヤー移動は同じ時間とEasingで進みます。即時に切り替える場合はDuration=0とし、Move PlayerがオンならUse Movement Speedをオフにします。

ステップ下部の **「プレイヤーを中央に配置（三人称）」** ボタンを押すと、そのステップを次の設定にします。

- Camera Mode = Third Person
- Third Person Position Mode = Player Relative
- Third Person Position = (0, 1.5, -3)
- Look Direction = Look At Player、Look Target Offset = (0, 0, 0)

ボタンはInspectorの設定用です。実際のカメラ移動はイベント再生時に行います。時間・移動先・Move Playerは変更しません。Undoで戻せます。`Look At Player`はControllerのPlayerを自動で参照し、有効なメッシュの描画範囲の中心（メッシュがなければ足元）を画面中央へ向けます。初期設定値からXYZを調整しても中央への注視は続きます。Look Target Offsetで顔などへ注視点をずらせます。

位置指定はCinemachineの最終段階で適用するため、指定した位置に対する壁回避は行いません。壁の中へ入らない距離を指定してください。中央に合わせる機能はズームや全身の自動収まり調整ではなく、全身を入れるにはZをより負にして距離を取ります。既存のPlayer Forwardは水平を向く機能のままなので、キャラクターを中央に映す用途にはLook At Playerを使います。

## オブジェクトの位置・回転・拡大

- `Relative`オン：位置は対象の開始時の向きを基準とする移動量、回転は開始角度からの追加角度、Scale Valueは開始拡大率に掛ける倍率です。例：`(2,2,2)`で2倍。
- `Relative`オフ：位置・角度はワールド座標の到着値、Scale Valueはローカル拡大率の到着値です。
- `Move`／`Rotate`／`Scale`で変更する項目を選びます。Easingで速度の変化を調整します。
- `Rotation Pivot`に蝶番位置の空オブジェクトを指定すると、その点を中心に回します。拡大は通常のTransform原点が基準です。Pivotを使った回転と絶対移動を組み合わせる場合、回転による位置補正も加わるため、絶対座標だけへの移動は別ステップに分けてください。
- 開始位置はシーン起動時でなく**各ステップ開始時**に読み取ります。
- AnimatorやRigidbodyが同じTransformを書き換える対象は、別途その制御を止める必要があります。Cinemachineが駆動するカメラの操作とプレイヤー本体・見た目の制御にはCamera Lookを使用します。

Renderer Visibilityは描画だけを変えるため、靴自身にDoorInteractionが付いていても消した後の演出が停止しません。Colliderやスクリプトは残ります。物理的にも消す場合は、On Startedへ対象Colliderのenabled=false等を追加してください。

床の靴を消すのと同時に操作キャラを靴ありモデルへ切り替える場合は、そのステップのOn Startedへシーン上のwalkingを登録し、`CharacterOutfitController.WearShoes()`を選びます。walkingのCharacterOutfitControllerに裸足のSuit Styleと、靴を履いたSuit With Shoes Style（それぞれAnimatorを含むモデル全体）を事前に登録してください。スーツ姿でのみ実行します。イベント中のAnimator切り替えと一人称での体の非表示にも対応しています。詳しい配置は`OutfitInteractionSetup.md`の「靴を履くイベントからモデルを切り替える」を参照してください。

## 歩行・走行

### リュックの揺れを移動中だけ再生する

リュック専用AnimatorのあるGameObjectへ`BackpackMovementAnimation`を追加します。`Player`にはシーンのwalkingのPlayerMovement、`Backpack Animator`にはリュック自身のAnimatorを指定します。キャラクター本体のAnimatorは指定しません。キャラの子ならPlayerを自動取得できます。リュックのAnimator Controllerには`Assets/Objects/Animator/BackPackAnimator.controller`を指定します。このControllerはBackPackActive.fbxの`Armature.002|Armature.002Action`（0〜40フレーム、Loop Timeオン）を使用します。

キャラクターのAnimatorのFloatパラメーター`Speed`が0なら、その時点のリュックの姿勢で停止し、0以外なら等速で続きを再生します。停止時に初期姿勢へ戻す処理ではありません。歩行・走行で揺れの再生速度は変えません。通常操作とEvent Sequence ControllerのWalk/Runの両方に対応し、着替え後も新しいAnimatorを参照します。メニュー・時間停止中は再生を止めます。判定は現在の移動アニメーション用Speedを使用し、壁に押し付けたときの実際の移動距離を測る処理ではありません。

このコンポーネントを使う間はリュックのRoot Motionを無効化し、コンポーネント無効化時に元の設定を戻します。表示・装着位置・リュック所持条件は別の設定です。Animatorを含むリュック全体はCharacterOutfitControllerの服や靴のパーツリストへ登録せず、装備用のオブジェクトとして管理してください。

走行の前傾姿勢で背中から離れる場合は、同じコンポーネントの`Adjust Position When Running`をオンにします。`Running Local Offset`に通常位置からの追加移動量をXYZで指定します。親のローカル軸が基準なので、前に寄せる方向が必ずZのプラスとは限りません。小さい値から調整してください。`Position Transition Time`は歩行・停止位置と走行位置を切り替える時間で、既定0.15秒、0なら瞬時です。

位置補正はこのコンポーネントを付けたリュックのGameObjectへ適用します。通常位置は有効になった時点で保存し、走行中だけ補正、歩行・停止で通常位置へ戻します。メニュー中は補正の進行を止め、無効化時も通常位置へ戻すので再表示時に補正が重なりません。走行判定はPlayerMovementのspeedとdashSpeedの中間値を境界にAnimatorのSpeedから求め、イベント中のRunにも対応します。歩行・走行速度が同じなら判別できないため補正しません。肩ひもの変形や回転補正は行いません。Playモードで調整した値はメモして、再生停止後にInspectorへ設定し直してください。

### プレイヤーの移動設定

- Camera Lookの`Move Player`をオンにして移動を有効にします。移動だけならLook Direction=Keep Current、切替も不要ならCamera Mode=Keep Currentを選びます。
- `Movement Style`でWalk／Runを選びます。既存AnimatorのFloatパラメーター`Speed`へ、PlayerMovementの`speed`／`dashSpeed`を渡します。対応する歩行・走行アニメーションがAnimatorに必要です。
- `Player Destination`に**足元の到着地点**を置いた空オブジェクトを登録します。到着地点はプレイヤーの子にしません。目印の回転は使用しません。
- 目印が空欄なら`Player Offset`を使用します。`(0,0,2)`なら前に2m。Use View Directionがオンなら現在の視点の水平な向き、オフならキャラの向きが基準です。
- `Y座標を参照しない`をオンにすると、ステップ開始時のワールドY座標を保ってXZだけ移動します。Player DestinationとPlayer Offsetの両方に適用し、Use Movement Speedの所要時間もXZの距離で計算します。オフなら従来どおりYも反映します。既存ステップではオフが初期値です。高さを変えずに位置リセットしたいステップでオンにしてください。これはプレイヤーの移動の設定で、カメラ独自の位置指定・視線は別に設定します。
- `Face Movement`でキャラを進行方向へ滑らかに向けます。同じステップ内のLook Directionで視線も同時に制御できます。
- `Use Movement Speed`オンなら「距離÷歩行／走行速度」を移動と視線の共通の時間にし、Durationは使用しません。オフなら両方ともDuration秒で完了します。同じ距離・同じDurationではWalkとRunの到着時間は同じで、アニメーションだけが変わります。
- EasingはLinearを基本にし、必要に応じて加減速させます。キーが空の場合も等速で動きます。極端な時間・カーブでは足滑りが発生します。
- 起床演出と同様、CharacterControllerとAnimatorのRoot Motionを一時停止して経路を確定します。**障害物回避・接地補正・階段追従はありません。** 高低差のある床では足元の目印を複数配置し、ドアを開けてから通過させます。
- 既存PlayerFootstepは背後のホラー演出用処理を含みます。イベント中に動かしたくない場合はPause During Eventへ登録し、歩行音には別AudioSourceを用意してください。

## 靴を調べて出発する例

`Initial View = First Person`、`Play Once = オン`にします。自動前進は靴を見た方向に左右されないよう、到着目印を使うと確実です。

| 順番 | Type | 主な設定 |
|---|---|---|
| 1 | Renderer Visibility | Target = 靴、Visible = オフ、Include Children = オン、Duration = 0 |
| 2 | Camera Look | First Person、Look At Target = ドア、Move Player = オン、Walk、Player Destination = ドア手前、Use Movement Speed = オフ、Duration = 1.5 |
| 3 | Play Audio | Audio Source = ドア用、Audio Clip = 開く音、Duration = 0、Wait For Audio = オフ |
| 4 | Object Transform | Target = ドア、Relative = オン、Rotate = オン、Rotation = (0,90,0)、Rotation Pivot = 蝶番、Duration = 1 |
| 5 | Camera Look | First Person、Direction Marker = 外へ向く目印、Move Player = オン、Walk、Player Destination = ドアの外、Use Movement Speed = オフ、Duration = 2 |
| 6 | Fade | Fade Alpha = 1、Duration = 2 |

ドアが反対側へ開いた場合はYを-90にします。Fade用にScreen Space OverlayのCanvasと画面全体を覆う黒いImageを用意します。RectTransformは縦横ストレッチ、上下左右の余白は0にしてください。Hide Fade On Awakeがオンなら起動時にAlphaを0にし、Fade時にImageを自動で有効にします。Raycast Targetも自動でオフにします。Image自体のチェックが外れていても再生できますが、親Canvasは有効にしてください。最初から暗い演出ではHide Fade On Awakeをオフにします。既存ScreenFadeと同じImageを同時に制御しないでください。

## 止まって見えるときの確認

実行中はコンポーネント下部のPlayback Statusで、何番のステップで待機・実行しているか確認できます。Delayは動き出す前の待機なので、その間は入力がロックされたまま動きません。完了後に「操作を保持中」と表示される場合はHold Control On Completionによる意図した保持です。開始できない場合はPlayback StatusとConsoleに設定不足を表示します。

暗転後も操作させない場合は`Hold Control On Completion`をオンにします。`On Completed`へ次の場面を始める処理を登録するか、後で`ReleaseControl()`を呼びます。**暗転だけではシーン遷移しません。** 画面を戻すには同じ演出内にFade Alpha=0のステップを追加するか、後続処理で戻します。

## 条件と終了時の扱い

- スーツ：Require Suitをオン、OutfitにCharacterOutfitControllerを登録。
- リュック：Require External Conditionをオン。所持処理から`SetExternalCondition(true)`、手放すならfalseを呼びます。これは接続用のフラグで、リュックの取得システム自体は追加していません。動作確認ではExternal Condition MetをInspectorで切り替えられます。
- 条件不足：On Conditions Not Metへ台詞・メッセージ表示を接続可能。再生済みになりません。
- 演出中は通常移動・視点入力・調査・新たなまばたき入力を停止。オプション画面では進行と演出で再生した音源を一時停止します。
- 終了後は移動・回転・拡大・メッシュ表示・暗転の最終状態を残し、通常操作を復帰します。長押し観察方式では、操作復帰後の視点は観察キーの状態に従います。
- `Cancel()`または管理オブジェクトの無効化で中断すると、キャラ・対象をその時点の位置に残して操作を戻します。暗転は演出開始時の色へ戻し、演出が再生した音源は停止します。途中まで変えたTransform／メッシュを巻き戻す機能はありません。
- 完了状態・外部条件はこのインスタンス内のみ保持し、セーブデータには保存しません。

## 今後追加すると便利な機能

現在の構成で出発例は組めます。用途が増えた段階で、複数対象を同時に動かすグループ、字幕、スキップ時の終端状態への確定、Animatorの終了通知待ち、シーン遷移、セーブ連携を追加できます。シーン遷移や独自処理は当面On CompletedなどのUnityEventから接続できます。
