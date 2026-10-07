# 音声と範囲イベントの設定

## Motion Steps内の音声（順番・待機時間を指定）

Motion StepsのStep TypeにPlay Audio（再生）とStop Audio（停止）を追加。
Audio Sourceに対象のAudioSourceを登録する。音声ステップではTargetは不要。
Play AudioのAudio Clipは空欄なら音源の既存Clipを使用し、Audio Loopで繰り返しを指定する。
Delay秒待って再生・停止し、音が終わるのを待たず次のステップへ進む。Open Duration／Close Durationは音声には使用しない。

Audio TimingはOpening Only（最初の調査のみ、初期値）／Closing Only（戻す調査のみ）／Both（両方）。閉じる場合はリストの逆順に進むが、再生と停止は自動で反転しない。対象外の方向ではその音声ステップのDelayも飛ばす。
ドア開閉と同時に鳴らすなら、開く音を移動ステップより前へOpening Onlyで、閉じる音を移動ステップより後ろへClosing Onlyで配置する。
スマホのアラーム停止はStop Audio、Audio Sourceにスマホの音源、Audio TimingはOpening Only、Delayは0、Allow Closeはオフにする。

Opening Audio／Closing AudioはInspectorから非表示にし、Motion Stepsへ統合した。旧設定は読み込み時のOnValidateまたは起動時のAwakeで自動移行する。開く音声は先頭、閉じる音声は末尾に逆順で移し、各方向で元の再生順を維持する。移行後のシーン／Prefabは編集モードで保存する。既に手動で同じ音をMotion Stepsへ重複登録していた場合は、移行後に不要なステップを削除する。
実行途中でDoorInteractionを無効化しても音声の状態は巻き戻さない。継続音を止める場合は別途Stopを呼ぶ。

## DoorInteraction：音声と移動を交互に実行

Motion Stepsに次の順番で登録する。

1. Play Audio：音声Aを再生。
2. Move Rotate：移動A。Open Duration秒かけて動き、完了を待つ。
3. Stop Audio：音声AのAudioSourceを停止。
4. Play Audio：音声Bを再生。
5. Move Rotate：移動B。

同じAudioSourceに別のAudio Clipを指定して切り替えることも可能。音を重ねたい場合は別々のAudioSourceを使う。音声は再生終了を待たず次のステップに進むため、移動中も鳴り続ける。開始を遅らせたいステップではDelayを指定する。
一度きりの演出ならAllow Closeをオフにする。オンの場合は戻すときにリストを逆順に進み、音声はAudio Timingで指定した方向だけ実行する。

- Highlightと発光の設定は従来どおり必要。
- AudioSourceは有効なオブジェクトに配置する。服と一緒に非表示になる子へ置くと音も止まるため、着替え音は有効な親などへ置く。

## AreaEventTrigger：範囲に入ると実行

1. 常に有効な空のオブジェクトを作り、AreaEventTriggerを追加する。BoxColliderも自動追加される。
2. BoxColliderのCenterとSizeで範囲を設定。Is Triggerは起動時に自動的にオンになる。
3. PlayerにプレイヤーのTransformを登録する。足元を基準にする場合はその位置まで範囲に含める。未指定なら起動時にPlayerMovementを探す。
4. Trigger On Start Insideをオンにすると、開始時から範囲内にいても実行する。
5. Trigger Onceをオンにすると、そのシーンの実行中は一度だけ。オフなら範囲から出て再び入るたびに実行する。
6. Audio Actionsへ音声を登録。On EnteredにはGameObject.SetActive(bool)などを登録できる。表示はtrue、非表示はfalseを指定する。

判定はプレイヤーのTransform位置が箱の内側にあるかで行う。体のColliderの一部が触れただけでは実行しない。Rigidbodyや物理のTrigger通知は不要。オプション表示中・時間停止中は判定を保留する。
範囲の外へ出ても音声・表示を自動で元に戻さない。範囲管理オブジェクト自体は有効にしておく。
一度だけの記録はセーブデータには保存せず、シーンの再読み込みで初期化する。

## ベッドからスマホのアラームを鳴らす

1. スマホにAudioSourceを用意し、アラームのClipを登録。Play On Awakeはオフ。位置から音を出すならSpatial Blendを3D側へ設定。
2. ベッドに空の範囲オブジェクトを置き、プレイヤーの開始位置を含むよう調整。
3. AreaEventTriggerのTrigger On Start InsideとTrigger Onceをオン。
4. Audio ActionsにPlayを追加し、SourceにスマホのAudioSource、Loopをオンにする。
5. スマホのDoorInteractionのMotion StepsにStop Audioを追加。同じAudioSourceを指定する。Audio TimingはOpening Only、Allow Closeはオフ。

これで起床演出と並行して鳴り始め、スマホを調べると停止する。既存のWakeUpIntroControllerの自動開始は変更していないため、On Enteredから起床を重複して開始する必要はない。
スマホ通知画面の表示処理は今回の実装に含まない。

## ドア・着替え・驚かせる演出

- ドア：Motion Stepsの移動前に開く音をPlay Audio／Opening Only、移動後に閉じる音をPlay Audio／Closing Onlyで登録。
- スーツ：Motion Stepsの表示切替前に着替え音をPlay Audio／Opening Onlyで登録。Audio Loopはオフ。
- 人影：AreaEventTriggerのOn Enteredに、非表示の人影のGameObject.SetActive(true)を登録。音も付けるならAudio Actionsへ追加。

スクリプトのコンパイルを確認済み。シーンへのオブジェクト配置、AudioClip指定、UnityのPlay Modeでの実音・範囲確認は別途必要。
