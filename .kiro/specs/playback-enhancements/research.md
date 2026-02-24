# リサーチ & 設計判断ログ

---
**目的**: 技術設計に影響するディスカバリー結果、アーキテクチャ調査、および判断根拠の記録。

**利用方法**:
- ディスカバリーフェーズにおけるリサーチ活動と結果を記録する。
- `design.md` には詳細すぎる設計判断のトレードオフを文書化する。
- 将来の監査や再利用のためのリファレンスとエビデンスを提供する。
---

## サマリー
- **フィーチャー**: `playback-enhancements`
- **ディスカバリースコープ**: Extension（既存システムの拡張）
- **主要な発見**:
  1. シークバー性能問題の根本原因はハードコードされた`maxUniverseNum = 32`と、`ReadAndSend`メソッドの線形探索アルゴリズムにある
  2. ArtNet Timecodeパケット (OpCode 0x9700) の構造は19バイトで、SMPTE互換のタイムコードフィールドを持つ
  3. 入力値の永続化にはUnityの`PlayerPrefs`が最適であり、追加ライブラリは不要

## リサーチログ

### シークバー性能ボトルネックの分析

- **コンテキスト**: ユニバース数が増えると（32以上）シークバーが動かなくなる不具合が報告されている
- **調査対象**: `ArtNetPlayerApplication.cs`, `ArtNetPlayer.cs`, `DataVisualizer.cs`
- **発見**:
  - `ArtNetPlayerApplication.Initialize()` にて `const int maxUniverseNum = 32` がハードコードされており、録画データに含まれる実際のユニバース数を反映していない。32ユニバースを超えるデータでは `Buffer.BlockCopy` で範囲外アクセスが発生し、処理が停止する
  - `ArtNetPlayer.ReadAndSend()` は全パケットを先頭から線形探索（O(n)）しており、パケット数が多い場合にフレームごとの処理コストが高い。パケットのtimeフィールドは録画時のタイムスタンプで単調増加であるため、二分探索（O(log n)）が適用可能
  - `DataVisualizer.Exec()` の `ComputeShader.Dispatch` で `maxUniverseNum / 32` を計算しているため、ユニバース数が32の倍数でない場合にディスパッチグループ数の切り上げが必要
- **影響**: ユニバース数をファイルから動的に取得し、バッファサイズを適切に初期化する設計が必要。二分探索の導入により大規模データでのシーク性能を改善する

### ArtNet Timecodeプロトコル調査

- **コンテキスト**: 要件3でArtNet Timecodeパケットの受信と再生位置同期が求められている
- **参照ソース**:
  - [Art-Net 4 Specification (art-net.org.uk)](https://art-net.org.uk/downloads/art-net.pdf)
  - [go-artnet OpCode定義 (GitHub)](https://github.com/jsimonetti/go-artnet/blob/master/packet/code/opcode.go)
  - [ArtNode Art-Net.h (GitHub)](https://github.com/tobiasebsen/ArtNode/blob/master/src/Art-Net.h)
  - [go-artnet packet パッケージ (pkg.go.dev)](https://pkg.go.dev/github.com/jmacd/go-artnet/packet)
- **発見**:
  - OpTimeCode = 0x9700（リトルエンディアンで送信される）
  - パケット構造 (19バイト):

    | オフセット | フィールド | サイズ | 説明 |
    |-----------|-----------|--------|------|
    | 0-7 | ID | 8 bytes | "Art-Net\0" |
    | 8-9 | OpCode | 2 bytes | 0x9700 (リトルエンディアン) |
    | 10 | ProtVerHi | 1 byte | 0 |
    | 11 | ProtVerLo | 1 byte | 14 |
    | 12 | Filler1 | 1 byte | 0 |
    | 13 | Filler2 | 1 byte | 0 |
    | 14 | Frames | 1 byte | 0-29 (Typeに依存) |
    | 15 | Seconds | 1 byte | 0-59 |
    | 16 | Minutes | 1 byte | 0-59 |
    | 17 | Hours | 1 byte | 0-23 |
    | 18 | Type | 1 byte | 0=Film(24fps), 1=EBU(25fps), 2=DF(29.97fps), 3=SMPTE(30fps) |

  - タイムコード → ミリ秒変換式: `((Hours * 3600 + Minutes * 60 + Seconds) * 1000) + (Frames * 1000 / fps)`
    ここで fps は Type に依存 (24, 25, 29.97, 30)
  - 既存の `ArtNetPacketUtillity.GetOpCode()` メソッドで OpCode の判定が可能だが、現在は `ArtNetOpCodes` enum に `TimeCode` が定義されていないため追加が必要
  - 既存の `ArtNetRecorder` はポート6454でUDP受信しているため、プレーヤー側でTimecodeを受信する場合はポートの競合に注意が必要。レコーダーとプレーヤーはタブ切替で排他的に動作するため、同一ポートの再利用が可能
- **影響**: `ArtNetOpCodes` enumへの `TimeCode = 0x97` の追加、`ArtNetPacketUtillity` へのTimecodeパース関数の追加、およびバックグラウンドスレッドでのUDP受信ループの新設が必要

### PlayerPrefsによる設定永続化

- **コンテキスト**: 要件4でDstIP / DstPort / トグル状態の永続化が求められている
- **参照ソース**: [Unity公式ドキュメント - PlayerPrefs](https://docs.unity3d.com/ScriptReference/PlayerPrefs.html)
- **発見**:
  - `PlayerPrefs` はアプリケーション終了後もデータを保持する（Windowsではレジストリに保存）
  - `PlayerPrefs.GetString()` / `SetString()` でIPアドレス文字列を保存可能
  - `PlayerPrefs.GetInt()` / `SetInt()` でポート番号とトグル状態（0/1）を保存可能
  - `OnApplicationQuit()` で自動保存されるが、即時保存には `PlayerPrefs.Save()` を明示的に呼ぶ
  - セキュリティ上の暗号化は不要（IPアドレスとポートは機密データではない）
- **影響**: 既存の `ArtNetResendUI` クラスに `PlayerPrefs` の読み書きロジックを追加するのみで実装可能。新規クラスの追加は不要

## アーキテクチャパターン評価

| オプション | 説明 | 強み | リスク / 制限 | 備考 |
|-----------|------|------|--------------|------|
| 既存MonoBehaviourパターンの拡張 | 現在のコンポーネント指向パターンを維持し、新機能を既存クラスの拡張として追加 | 一貫性維持、学習コスト低、最小限の構造変更 | 個別クラスの責務が増大する可能性 | 採用。steeringのパターンに準拠 |
| 新規サービスレイヤー導入 | タイムコード受信やデータアクセスを独立サービスとして分離 | 責務分離、テスタビリティ向上 | 過剰設計、既存パターンとの乖離 | 不採用。現在のプロジェクト規模では不適切 |

## 設計判断

### 判断: シーク性能改善アプローチ

- **コンテキスト**: `ReadAndSend` の線形探索が大規模データでボトルネックとなっている
- **検討した代替案**:
  1. 二分探索の導入 — `DmxRecordData` のパケットリストが時間順にソートされている前提で二分探索を適用
  2. インデックスキャッシュ — 最後に検索したインデックスを記憶し、次回の検索開始位置とする
  3. 時間ベースのルックアップテーブル — 固定間隔のタイムスタンプに対応するパケットインデックスを事前計算
- **選択したアプローチ**: 二分探索 + インデックスキャッシュの併用
- **根拠**: パケットリストは録画時のタイムスタンプ順で単調増加であるため二分探索が適用可能。再生中はヘッダーが単調に進むためキャッシュとの併用で連続再生時のO(1)アクセスも実現できる
- **トレードオフ**: 実装の複雑性がやや増すが、性能改善効果が大きい
- **フォローアップ**: 32ユニバース以上のテストデータでの性能検証

### 判断: ユニバース数の動的取得方法

- **コンテキスト**: ハードコードされた `maxUniverseNum = 32` を実際のデータに基づく値に変更する必要がある
- **検討した代替案**:
  1. ファイル読み込み時にバイナリヘッダーからユニバース数を読み取る
  2. 全パケットを走査して最大ユニバース番号を検出する
- **選択したアプローチ**: `DmxRecordData` の読み込み時に全パケットを走査し、最大ユニバース番号+1を記録する
- **根拠**: 現在のバイナリ形式にはヘッダーレベルの最大ユニバース数フィールドが存在しない（TODOコメントで示唆されている）。ファイル全体を読み込む既存処理の中で最大値を記録するため追加I/Oコストは発生しない
- **トレードオフ**: ファイル形式の変更は行わず、後方互換性を維持する

### 判断: タイムコード受信のUDPポート戦略

- **コンテキスト**: ArtNet Timecodeはポート6454で受信されるが、レコーダーも同一ポートを使用している
- **検討した代替案**:
  1. プレーヤー専用のUDP受信ループを新設（ポート6454）
  2. 既存のレコーダーのUDP受信ループを共有する
- **選択したアプローチ**: プレーヤー専用のUDP受信ループを新設
- **根拠**: レコーダーとプレーヤーはタブ切替による排他動作であるため、ポート競合は発生しない。レコーダーは`OnDisable`でソケットを閉じ、プレーヤーはタイムコード受信モード有効時にのみソケットを開く設計とする
- **トレードオフ**: タイムコード受信モードを切り替えるたびにソケットの開閉が発生するが、頻繁な切り替えは想定されないため許容範囲

### 判断: タイムコード無受信時のタイムアウト処理

- **コンテキスト**: 要件3.5でタイムコードが一定時間受信されなかった場合の一時停止が求められている
- **選択したアプローチ**: 最後のタイムコード受信からの経過時間を監視し、閾値（2秒）を超えた場合に再生位置を保持して一時停止状態とする
- **根拠**: 一般的なタイムコードソースは毎フレーム（最低24fps）でパケットを送信するため、2秒のタイムアウトは信号喪失の検出に十分な猶予がある
- **フォローアップ**: タイムアウト値はInspectorから調整可能にする

## リスク & 対策
- **リスク1**: ComputeShaderのDispatchグループ数が32の倍数でないユニバース数で正しく動作しない可能性 — 対策: ユニバース数を32の倍数に切り上げてDispatchする
- **リスク2**: タイムコードのミリ秒変換精度によるフレーム同期のズレ — 対策: double精度で計算し、フレームレートに応じた正確な変換式を使用する
- **リスク3**: 大量のユニバースデータでの `Buffer.BlockCopy` による配列境界エラー — 対策: ユニバース番号に対する境界チェックを追加する

## リファレンス
- [Art-Net 4 Specification (PDF)](https://art-net.org.uk/downloads/art-net.pdf) — ArtNet Timecodeパケット仕様
- [go-artnet OpCode定義](https://github.com/jsimonetti/go-artnet/blob/master/packet/code/opcode.go) — OpTimeCode = 0x9700の確認
- [ArtNode Art-Net.h](https://github.com/tobiasebsen/ArtNode/blob/master/src/Art-Net.h) — ArtTimeCodeパケット構造体定義
- [Unity PlayerPrefs API](https://docs.unity3d.com/ScriptReference/PlayerPrefs.html) — 設定永続化のAPIリファレンス
- [go-artnet packet パッケージ](https://pkg.go.dev/github.com/jmacd/go-artnet/packet) — ArtTimeCodePacket構造定義の参考
