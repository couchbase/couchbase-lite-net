### Couchbase Lite for Unity3D

このプロジェクトはUnity3Dの使用のためにCouchbase Liteをビルドします。しかし、正しく動作するように注意点があります。

- **UnityEngine.dllの入手**
  - 理由不明ですが、.NET 3.5（Unity3Dの条件）に関してXamarin StudioはMSBuildターゲットを無視するようです。xbuildならちゃんと動きますので、以下の１つかやってください。
    - Couchbase.Lite.Unity.csprojに対してxbuildを実行
    - Unityアプリケーションの中からCouchbase.Lite.Unity/vendor/UnityフォルダーにUnityEngine.dllをコピー
    
- **プラットフォーム固有のDLLファイルを用意**
  - Couchbase Liteはプラットフォーム固有であるSQLiteを使用しますので、正しいネイティブライブラリーを使用することが必要です（スタンドアロン・iOS・Android）。それぞれのDLLファイルが用意されています。Couchbase.Lite.Net35/vendor/SQLitePCLの中にスタンドアロンのSQLitePCL.raw.dllとiOS/SQLitePCL.raw.dllとAndroid/SQLitePCL.raw.dllが存在します。Unityにインポートし、適切なプラットフォームに設定する必要があります。なお、x64とx86にWindowsのネイティブライブラリーがあります（WindowsだけOSと一緒にインストールされていないため）。これらもUnityにインポートする必要があります。
  
- **プロジェクトの設定**
  - 互換性設定をUnity .NET 2.0にする必要があります（.NET 2.0 Subsetではコンパイルできません。TypeInitializationExceptionなどが発生します。）。それぞれのプレイヤーの設定でこの設定を変更することができます（共通設定なので、１つのプレイヤーで変えたら全部変わります）。そして、iOSプレイヤーの設定でAOT Compilation Optionsにこれを追加する必要もあります：nimt-trampolines=8096,ntrampolines=8096。なお、Unityのフォルダー構成のためにWindowsの場合はスクリプトのこの行が必要になってきます。

```c#
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    SQLitePCL.SQLite3Provider.SetDllDirectory (Path.Combine(Application.dataPath, "Plugins"));
#endif
```
