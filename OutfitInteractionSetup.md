# ラックの服を消し、キャラクターを着替えさせる設定

表示演出と入力はDoorInteraction、キャラクターの着替えはCharacterOutfitControllerが担当します。InteractableHighlightは発光判定だけに戻しました。前のEnable Outfit Interaction等の設定は、以下へ設定し直してください。

## パーツの表示・非表示で着替える

CharacterOutfitControllerの`Switch Mode`で切り替え方式を選びます。再生を停止して設定してください。既存シーンの初期値はWhole Modelで、以前の設定を引き継ぎます。どの方式でも既存のWearSuit()／WearPajamas()／WearShoes()／WearBarefoot()を使い、スーツ着用条件も更新します。

| Switch Mode | 動作 |
|---|---|
| Whole Model | 従来どおり寝巻・スーツ・靴ありスーツをモデル全体で切り替える |
| Shoes Parts | 寝巻／スーツは従来のモデル切り替え。スーツ内の靴／素足だけGameObjectのON／OFFで切り替える |
| All Parts | Shared Styleの体・骨・Animatorを共通で使い、服も靴もパーツだけ切り替える |

### 現在のモデルで靴だけをパーツ切り替えにする

1. `Switch Mode = Shoes Parts`にします。
2. `Pajama Style`と`Suit Style`は現在の寝巻・スーツモデルを登録します。Suit Styleは裸足と靴の両パーツを含むモデルを使います。
3. `Shoe Parts`へスーツモデル内の「靴」「靴ひも」などを登録します。
4. `Barefoot Parts`へ同じスーツモデル内の「素足」を登録します。足が体と一体の場合は空欄も可能です。
5. `Suit With Shoes Style`は不要なので空欄にできます。以前の複製モデルをシーンに残す場合は、そのGameObjectを非アクティブにしてください。登録が残っている場合はスクリプトがその別モデルを非表示にします。

WearShoes()でShoe PartsをON、Barefoot PartsをOFFにし、WearBarefoot()で逆にします。スーツのAnimatorは切り替えません。寝巻に戻すと着靴状態を解除します。

### 服も含めて共通モデルにする

`Switch Mode = All Parts`にし、`Shared Style`へ体・共通の骨・Animator・全パーツを含む見た目専用の親を登録します。Pajama Style／Suit Style／Suit With Shoes Styleの参照はこの方式では使用しません。以前の別モデルは手動で非アクティブにしてください。

| リスト | 表示する状態 |
|---|---|
| Pajama Parts | 寝巻のとき |
| Suit Parts | スーツのとき（裸足・靴あり共通） |
| Shoe Parts | スーツで靴を履いたとき |
| Barefoot Parts | 裸足のとき（寝巻を含む） |

共通の頭・髪・体・骨はリストへ入れず、通常どおり有効にします。重なって隠したい体のメッシュが独立していれば、該当するリストへ入れられます。リストはGameObjectを指定し、子の描画もまとめて切り替えます。骨やAnimatorを含むオブジェクト、同一パーツの重複、リスト間で親子になる指定はエラーになります。たとえば靴がスーツの子なら、Suit Partsにはそのスーツの親全体ではなく、服のメッシュ側を登録してください。

Rendererのenabledは有効にしておき、服装による表示・非表示はGameObject側で管理します。一人称ではCameraSwitchがRendererを隠し、三人称へ戻ったときもOFFの服や靴は表示されません。起動時は寝巻・裸足になります。未登録のパーツの有効状態は変更しません。

この機能は骨の参照の付け替えやメッシュの分割は行いません。別のモデルから持ってきた服は、Shared Styleの共通の骨を使うようSkinnedMeshRendererの参照を揃える必要があります。単に親を移しただけでは骨は共有されません。パーツ登録後も、Event Sequence ControllerのOn Started等に設定済みのWearShoes()は変更せず使えます。

## 1. モデル全体を切り替える場合の設定（Whole Model）

操作キャラの共通の親（CameraSwitchと同じGameObjectを推奨）にCharacterOutfitControllerを追加します。

| 項目 | 登録するもの |
| --- | --- |
| Camera Switch | 操作キャラのCameraSwitch |
| Player Movement | 操作キャラのPlayerMovement |
| Pajama Style | 寝巻姿のモデル全体（骨・体・服・Animatorを含む） |
| Suit Style | スーツ姿のモデル全体（骨・体・服・Animatorを含む） |
| Suit With Shoes Style | 任意。靴を履いたスーツ姿のモデル全体（骨・体・服・Animatorを含む） |

寝巻姿とスーツ姿のモデルは同じPlayerの子に兄弟として置き、ローカル位置・向き・大きさを合わせます。操作スクリプト・CharacterController・カメラ追従用の目印は、切り替えるモデルの外の共通の親側に置きます。

両モデルのAnimatorに移動用Controllerを指定します。既存のPlayerMovementが使うFloatパラメーターSpeedが必要です。Avatarは各モデルに対応するものを使用してください。ラックの展示用の服をSuit Styleに登録するのではありません。

開始時は寝巻になり、WearSuitでスーツ、WearPajamasで寝巻に切り替わります。PlayerMovementのAnimator参照も自動で切り替わり、一人称中の体の非表示も維持します。

CameraSwitchに以前保存した服装参照は、専用コンポーネントを追加した際に未設定項目へ引き継ぎます。念のためInspectorの登録先を確認してください。1つの骨を共有して服メッシュだけを差し替える場合は、上記のAll Parts方式を使用します。

### 靴を履くイベントからモデルを切り替える

`walking`のCharacterOutfitControllerで、`Suit Style`を裸足のスーツ、`Suit With Shoes Style`を靴を履いたスーツにします。3つのモデルは共通の操作元の子に兄弟として置き、位置・向き・大きさを合わせてください。靴だけのオブジェクトや、操作スクリプト・カメラまで含むwalking全体は登録しません。追加モデルのAnimatorにも移動用Controller（FloatのSpeed）と対応するAvatarを設定します。Rendererは有効にし、開始時のモデルの表示・非表示はスクリプトに任せます。

1. Event Sequence Controllerの靴を履き終わるタイミングのステップを開きます。
2. **ステップのOn Started()**に「＋」で項目を追加します。
3. シーン上の`walking`（CharacterOutfitControllerが付いているオブジェクト）を登録します。
4. 関数は **CharacterOutfitController → WearShoes()** を選びます。

スーツ姿で呼ぶと、裸足モデルを無効化し、靴を履いたモデルとそのAnimatorへ切り替えます。一人称では引き続き体を隠し、三人称へ切り替えると新しいモデルが見えます。イベント中も切り替え可能で、後続のWalk/Runは新しいAnimatorを使います。各AnimatorのRoot Motionはイベント終了・中断時に元の設定へ戻します。モデルの選択自体は終了・中断後も維持します。

On Startedはそのステップの開始時に実行されます。靴を履く音が終わった後なら、その後にEventステップ（Duration=0）を置いて登録するか、音声ステップをWait For AudioにしてOn Completedへ登録します。床の靴を消すRenderer Visibilityは別途必要です。裸足に戻す関数は`WearBarefoot()`です。`WearPajamas()`は靴の状態も解除します。靴モデル未設定・Animator不備・寝巻姿の場合、切り替えずConsoleへ理由を表示します。起床演出中は切り替えできません。

## 2. ラックと発光

消えないラックの親にDoorInteractionとProximityHintTargetを付けます。スーツ・ズボン・ハンガーは別々に表示を切り替えられる子オブジェクトにします。

- スーツにInteractableHighlightを設定し、Target RendererにスーツのRendererを指定します。DoorInteractionのHighlightにこのコンポーネントを登録します。
- ProximityHintTargetのDetection Pointには、服が消えても残るラックの子の空オブジェクトを指定します。服・ハンガーの近くに置いてください。
- 各InteractableHighlightのProximity Targetにラック共通のProximityHintTarget、Camera SwitchとPlayer Proximity Hintに操作キャラのもの、View Cameraに実際の描画カメラを登録します。
- DoorInteractionのProximity Targetにも、このラック専用のProximityHintTargetを登録します。一度きりの場合、完了後にこの「？」が無効になります。
- Colliderは発光対象自身に付けます。ラック全体のColliderで対象への視線を遮らないようにしてください。発光対応マテリアルを使用します。

## 3. DoorInteractionのステップ

### スーツとズボンをまとめて発光させる場合

InteractableHighlightを消えないラックの親に1つ付け、次のどちらかで指定できます。

- Target Renderers：スーツ・ズボンのRendererをリストに登録します。
- Target Root：スーツ・ズボンだけをまとめた親Transformを指定します。その親自身とすべての子孫のRendererを取得します。

従来のTarget Rendererも使えます。3項目の対象は合算され、重複登録は1つにまとめられます。親指定だけならTarget RendererをNone、Target Renderersを空にします。

どれかの表示中のパーツが一人称の中央範囲に入り、距離・遮蔽物の条件を満たすと、グループ全体が発光します。Focus Pointを別途指定した場合はその共通位置で中央判定します。Proximity TargetのDetection Pointも従来どおりグループ共通の距離・遮蔽物判定に使います。

DoorInteractionのHighlightには、この1つのInteractableHighlightを登録します。この構成ではズボン側に別の発光コンポーネントやDoorInteractionSideは不要です。同じRendererを複数のInteractableHighlightから制御しないよう、以前付けた重複コンポーネントは外してください。

ハンガーを最初から発光させたくない場合は、Target Rootをラック全体にせず、服だけの親を指定してください。ハンガーは従来どおり別の発光コンポーネントをOpen State Highlightに指定します。

対象は再生開始時に収集します。非アクティブな子も登録されますが、表示状態は変更しません。再生中に新しく追加したRendererやInspectorで差し替えた参照は、再生し直すと反映されます。

Motion Stepsに次の2ステップを登録します。

| ステップ | Step Type | Target | Active When Open | Delay |
| --- | --- | --- | --- | --- |
| 1 | Set Active | 展示用スーツ | オフ | 0 |
| 2 | Set Active | 展示用ズボン | オフ | 0 |

ハンガーはTargetに含めません。服だけをまとめた親がある場合は、その親を1ステップで非表示にしても構いません。DoorInteraction自身やその親は非表示対象にできません。

Set ActiveはDelayの後に即時切り替えます。GameObjectを切り替えるためRendererだけでなくColliderも無効になります。Open Duration、Close Duration、Position Offset、Rotation Offset、Rotation PivotはSet Activeでは使いません。

Move Rotateを選ぶと従来どおり移動・回転します。既存のステップはこの種類として読み込まれます。移動→非表示など、異なる種類を同じリストで順に実行できます。

## 4. 着替えのイベントをつなぐ

DoorInteractionのイベントを次のように登録します。対象欄へCharacterOutfitControllerを付けた操作キャラをドラッグし、関数を選びます。

| DoorInteractionのイベント | 呼び出す関数 |
| --- | --- |
| On Opened | CharacterOutfitController → WearSuit() |
| On Closed | CharacterOutfitController → WearPajamas() |

On Opening StartedではなくOn Openedを使うことで、途中で演出が中断した場合に服だけ切り替わることを避けます。

## 5. 寝巻に戻せるか

- Allow Closeがオフ：一度だけ実行。完了後は再調査できません。
- Allow Closeがオン：再調査でステップを逆順に実行し、表示は各ステップ前の状態へ戻ります。On Closedで寝巻に戻ります。

戻せる場合は、ハンガーにもInteractableHighlightを設定し、DoorInteractionのOpen State Highlightに登録します。ハンガーは最初は発光せず、服を取った後に調べる対象になります。元の服が消えても、DoorInteractionは消えない親に残してください。

ズボンもEキーで調べる対象にしたい場合は、ズボンにInteractableHighlightとDoorInteractionSideを付け、DoorInteractionSideの管理元へラックのDoorInteractionを登録します。

## 確認する動作

1. 三人称で近づくと「？」、一人称で服を中央に捉えると発光する。
2. Eキーで服・ズボンが消え、ハンガーが残る。調べたフレームはまばたきを抑止するため、同時に左クリックしてもまばたきが始まらない。将来まばたきもEキーに割り当てた場合も、調べる操作を優先する。
3. 三人称に戻るとスーツ姿で移動・アニメーションする。
4. Allow Closeがオンならハンガーの再調査で寝巻と展示用の服が戻る。
5. Allow Closeがオフなら再調査できず、このラックの「？」も消える。
6. 既存のドアの移動・回転、通常のまばたきも動作する。

シーンの参照登録とUnity上での再生確認は未実施です。
