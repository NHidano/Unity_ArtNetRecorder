# プロジェクト構造

## 構成方針

レイヤー指向の構成。`Core` 層がドメインロジックとプロトコル処理を担い、`UI` 層がプレゼンテーションを担当する。サードパーティライブラリは `Packages/` および `Plugins/` に分離。

## ディレクトリパターン

### アプリケーション管理
**場所**: `Assets/Scripts/Core/Application/`
**目的**: アプリケーション全体のライフサイクルとモード切替
**パターン**: `ApplicationManager` がタブ選択に応じて `ApplicationBase` サブクラスを開閉する。各アプリケーションモード (レコーダー、プレーヤー) は独立した `ApplicationBase` 派生クラスとして実装。
**例**: `DmxRecorderApplication`, `ArtNetPlayerApplication`

### ArtNetプロトコル
**場所**: `Assets/Scripts/Core/ArtNet/`
**目的**: ArtNetプロトコルのパケット構造、OpCode定義、パース処理
**パターン**: `ArtNetPacket` 基底クラスからプロトコル種別ごとのパケットクラスを派生。`ArtNetPacketUtillity` で受信バッファからの低レベルデータ抽出を行う。

### レコーダー
**場所**: `Assets/Scripts/Core/Recorder/`
**目的**: 信号の受信と録画処理
**パターン**: `RecorderBase` 抽象クラスを共通インターフェースとし、プロトコル別の具象クラスが録画ロジックを実装。受信ループ、バッファリング、ファイル書き出しの構造は各レコーダーで共通。
**例**: `ArtNetRecorder`, `UdpRecorder`, `AcnRecorder`

### プレーヤー
**場所**: `Assets/Scripts/Core/Player/`
**目的**: 録画データの読み込み、再生、ArtNet再送信
**パターン**: `ArtNetPlayer` がバイナリファイルを `DmxRecordData` に読み込み、タイムライン上のヘッダー位置に基づきフレーム単位でデータ取得とUDP送信を行う。`AudioPlayer` が音声の同期再生を担当。

### データ構造
**場所**: `Assets/Scripts/Core/DataStructure/`
**目的**: 録画データのメモリ内表現とファイル読み込み
**パターン**: `DmxRecordData` がファイルパスからのデシリアライズと、パケットリスト (`DmxRecordPacket`) + Universe別データ (`UniverseData`) の保持を担当。

### ユーティリティ
**場所**: `Assets/Scripts/Core/Utility/`
**目的**: プロジェクト横断の汎用機能
**パターン**: `ByteConvertUtility` (型変換)、`SingletonMonoBehaviour<T>` (ジェネリックシングルトン)、`AsyncExtensions` (Task拡張) など、静的クラスまたはジェネリック基底クラスとして提供。

### UI
**場所**: `Assets/Scripts/UI/`
**目的**: uGUIベースのユーザーインターフェース
**パターン**: 各UI要素は個別の `MonoBehaviour` として実装し、`SerializeField` でInspector上から接続。UniRxの `OnClickAsObservable` でイベントを上位コンポーネントに公開。`Tab` コンポーネントが `CanvasGroup` の表示/非表示切替を管理。
**例**: `RecorderUI`, `PlayerUI`, `DialogUI`, `Tab`

### Timeline
**場所**: `Assets/Scripts/Timeline/`
**目的**: Unity Timeline上でDMXデータを扱うカスタムトラック (実験的)
**パターン**: `TrackAsset` / `PlayableAsset` / `PlayableBehaviour` のUnity Timeline拡張パターンに従う。`DmxTrack` > `DmxClip` > `DmxBehaviour` / `DmxMixerBehaviour` の階層構造。
**名前空間**: `inc.stu.SyncSystem`

### サードパーティ
**場所**: `Assets/Packages/` (StandaloneFileBrowser), `Assets/Plugins/` (DOTween)
**目的**: 外部ライブラリの配置場所。UPMパッケージは `Packages/manifest.json` で管理。

## 命名規則

- **ファイル / クラス**: PascalCase (`ArtNetRecorder`, `DmxRecordData`)
- **プライベートフィールド**: camelCase、`[SerializeField]` 付きのものは `private` + camelCase
- **メソッド**: PascalCase (`RecordStart`, `OnClose`)
- **定数**: PascalCase (`MaxUniverse`, `ArtNetServerPort`)
- **名前空間**: ドット区切りPascalCase (`ProjectBlue.ArtNetRecorder`, `inc.stu.SyncSystem`)

## 名前空間の使い分け

```csharp
// Core層のレコーダー・UI
namespace ProjectBlue.ArtNetRecorder { ... }

// ユーティリティ
namespace ProjectBlue { ... }

// Timeline拡張 (独立した名前空間)
namespace inc.stu.SyncSystem { ... }

// グローバル (一部のApplication/UI/ユーティリティクラス)
// 名前空間なし
public class ApplicationManager : MonoBehaviour { ... }
```

**注意**: 名前空間の適用は統一されておらず、一部のクラスはグローバル名前空間に存在する。新規コードでは `ProjectBlue.ArtNetRecorder` を使用することを推奨。

## コード構成の原則

- **UI層とロジック層の分離**: UIクラスはSerializeFieldで子UIを保持し、ロジックは `Application` クラスが担当
- **リアクティブ接続**: コンポーネント間の結合はUniRxの `IObservable<T>` を通じて行い、直接的なメソッド呼び出しを避ける
- **スレッド安全性**: ネットワークI/Oはバックグラウンドスレッドで処理し、`ConcurrentQueue` でメインスレッドと同期
- **unsafeコードの局所化**: unsafe操作は `Core/Unsafe/` と `Core/ArtNet/` に限定

---
_パターンを文書化し、ファイルツリーの網羅ではない。パターンに従う新規ファイルは更新不要_
