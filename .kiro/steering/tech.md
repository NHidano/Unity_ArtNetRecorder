# 技術スタック

## アーキテクチャ

MonoBehaviourベースのコンポーネント指向アーキテクチャ。`ApplicationBase` 抽象クラスによるアプリケーションモード切替パターンと、`RecorderBase` 抽象クラスによるレコーダー戦略パターンを採用。UDPネットワーク処理はバックグラウンドスレッド (`Task.Run`) で実行し、`ConcurrentQueue` でメインスレッドとデータを受け渡す。

## コア技術

- **エンジン**: Unity 2022.1.14f1
- **言語**: C# (unsafeコード使用あり)
- **レンダリング**: Universal Render Pipeline (URP) 13.1.8
- **ビルドターゲット**: Windows デスクトップ

## 主要ライブラリ

開発パターンに影響を与える重要なライブラリのみ記載:

- **UniRx**: リアクティブプログラミング。UIイベント、状態変更、アプリケーションロジックの接続に全面的に使用 (`Subscribe`, `OnClickAsObservable`, `Subject<T>`)
- **UniTask**: 非同期処理。`UniTask`, `UniTaskVoid`, `UniTask.Run()` によるファイルI/O・ネットワーク処理の非同期化
- **DOTween**: UIアニメーション (Demigiant)
- **Unity Timeline**: DMXデータをTimelineクリップとして統合 (カスタム `PlayableAsset` / `PlayableBehaviour`)
- **StandaloneFileBrowser**: ネイティブファイルダイアログ (録画データの読み込み用)

## 開発標準

### ネットワーク処理パターン
- UDPの受信ループはバックグラウンドスレッドで実行 (`Task.Run`)
- `ConcurrentQueue<T>` でスレッド間データ受け渡し
- `CancellationToken` (`GetCancellationTokenOnDestroy`) でライフサイクル管理
- `SynchronizationContext` でメインスレッドへのエラー通知

### unsafeコード
- 高速なメモリコピーに `UnsafeUtility.MemCpy` と `fixed` ステートメントを使用
- `Assets/Scripts/Core/Unsafe/` にカスタム `BitConverter` を配置

### リアクティブパターン
- UIイベントは `OnClickAsObservable()` + `Subscribe()` + `AddTo(this)` で接続
- コンポーネント間通信は `Subject<T>` / `IObservable<T>` を公開
- サブスクリプションの寿命管理は `AddTo()` でGameObjectのライフサイクルに紐付け

### バイナリデータ処理
- `ByteConvertUtility` で型ごとの byte[] 変換とJoinを一元化
- リトルエンディアンで統一 (ArtNetプロトコルのヘッダ部分はネットワークバイトオーダー)
- 独自バイナリ形式: `[Sequence(uint)][Time(double)][NumUniverses(uint)][UniverseData...]`

## 開発環境

### 必須ツール
- Unity 2022.1.x
- IDE: Rider / Visual Studio (asmdef未使用、単一コンパイルドメイン)

### 主要操作
```
Unity Editor上でシーン「ArtNetRecorder」を開いて実行
録画データはStreamingAssetsフォルダに保存される
```

## 主要な技術的決定

| 決定 | 理由 |
|------|------|
| UniRx採用 | UIとロジックの疎結合化、イベントストリームによる宣言的なデータフロー |
| バックグラウンドスレッドでUDP受信 | メインスレッドのフレームレートへの影響を回避 |
| unsafeメモリコピー | 大量のDMXデータ (最大512ユニバース x 512ch) の高速処理 |
| 独自バイナリ形式 | 汎用的で任意のソフトウェアから読み取り可能、最小限のオーバーヘッド |
| ComputeShaderによる可視化 | GPU上で512 x N のDMXテクスチャを高速生成 |
| Singletonパターン (Logger, DialogManager) | グローバルなサービスアクセス (ジェネリック `SingletonMonoBehaviour<T>`) |

---
_標準とパターンを文書化し、全依存関係の網羅ではない_
