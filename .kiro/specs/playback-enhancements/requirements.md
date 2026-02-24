# Requirements Document

## Project Description (Input)
ユニバース数が増えるとシークバーが動かなくなる不具合の修正、ループ再生の実装、タイムコードを受信して再生する機能の実装、DstIP、DstPortの入力値の保存機能を実装したいです。

## Introduction
本仕様は、ArtNet / UDP Recorderのプレーヤー機能における品質改善と機能拡張を定義する。対象は以下の4つの領域である:
1. ユニバース数増加時のシークバー性能問題の修正
2. ループ再生機能の実装
3. ArtNet タイムコード受信による再生制御機能の実装
4. ArtNet再送信先 (DstIP / DstPort) 入力値の永続化

## Requirements

### Requirement 1: シークバー性能問題の修正
**Objective:** 照明エンジニアとして、多数のユニバースを含む録画データでもシークバーが正常に動作してほしい。録画データのプレビューやタイミング確認が妨げられないようにするためである。

#### Acceptance Criteria
1. When ユーザーが32ユニバース以上を含む録画データを読み込んだ場合, the ArtNetPlayer shall シークバーの操作を遅延なく受け付ける
2. When ユーザーがシークバーをドラッグした場合, the ArtNetPlayer shall 対応するタイムスタンプのDMXデータを取得し、ビジュアライザーを更新する
3. While 録画データの再生中, the ArtNetPlayer shall フレームレートを著しく低下させることなくシークバーの位置を更新する
4. The ArtNetPlayer shall 録画データに含まれる実際のユニバース数に基づいてバッファを初期化する

### Requirement 2: ループ再生機能
**Objective:** 照明エンジニアとして、録画データをループ再生できるようにしたい。照明シーケンスの繰り返し確認や長時間のデモ再生に利用するためである。

#### Acceptance Criteria
1. The PlayerUI shall ループ再生のON/OFFを切り替えるトグルボタンを提供する
2. While ループ再生が有効な状態で, when 再生位置が録画データの終端に到達した場合, the ArtNetPlayer shall 再生位置を先頭にリセットして自動的に再生を継続する
3. While ループ再生が無効な状態で, when 再生位置が録画データの終端に到達した場合, the ArtNetPlayer shall 再生を停止してシークバーを終端位置に保持する
4. When ループ再生の設定が変更された場合, the ArtNetPlayer shall 再生中であっても即座に新しい設定を反映する

### Requirement 3: タイムコード受信による再生制御
**Objective:** 照明エンジニアとして、外部のタイムコードソースからArtNet Timecodeを受信し、それに同期して録画データを再生したい。照明卓やショーコントローラーとの連携で正確な同期再生を実現するためである。

#### Acceptance Criteria
1. The PlayerUI shall タイムコード受信モードのON/OFFを切り替えるUIコントロールを提供する
2. When タイムコード受信モードが有効な状態でArtNet Timecodeパケットを受信した場合, the ArtNetPlayer shall 受信したタイムコード値に対応する再生位置へシークし、DMXデータを送信する
3. While タイムコード受信モードが有効な状態で, the ArtNetPlayer shall 通常の再生/一時停止ボタンによる手動再生制御を無効化する
4. While タイムコード受信モードが有効な状態で, the PlayerUI shall 受信中のタイムコード値をリアルタイムで表示する
5. If タイムコード受信モードが有効な状態でタイムコードパケットが一定時間受信されなかった場合, the ArtNetPlayer shall 最後に受信したタイムコード位置で再生を一時停止する
6. When タイムコード受信モードが無効化された場合, the ArtNetPlayer shall 通常の手動再生制御を復元する

### Requirement 4: ArtNet再送信先設定の永続化
**Objective:** 照明エンジニアとして、ArtNet再送信先のIPアドレスとポート番号の入力値がアプリケーション終了後も保存されるようにしたい。毎回同じ値を手入力する手間を省くためである。

#### Acceptance Criteria
1. When ユーザーがDstIPフィールドに有効なIPアドレスを入力した場合, the ArtNetResendUI shall 入力値をローカルストレージに保存する
2. When ユーザーがDstPortフィールドに有効なポート番号を入力した場合, the ArtNetResendUI shall 入力値をローカルストレージに保存する
3. When アプリケーションが起動した場合, the ArtNetResendUI shall 前回保存されたDstIPおよびDstPortの値を読み込んでフィールドに表示する
4. If 保存されたデータが存在しない場合, the ArtNetResendUI shall デフォルト値を表示する
5. When 再送信トグルのON/OFF状態が変更された場合, the ArtNetResendUI shall そのトグル状態もローカルストレージに保存する
