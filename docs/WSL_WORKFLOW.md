# WSL保存先とWindows Unityの使い分け

ソースの保存先はWSLの `/home/dpc7/world interaction`。Unityプロジェクトはその直下の `UnityProject` です。

Windowsからは次のパスで参照できます。

```text
\\wsl.localhost\Ubuntu-22.04\home\dpc7\world interaction\UnityProject
```

Windows版Unityは、ネットワーク扱いのUNCパスでPackage ManagerやAndroidビルドの制約に遭遇する場合があります。その場合はWindowsのローカルドライブにプロジェクトをコピーして開いてください。今回のコンパイル・テスト・APK生成もWindowsローカルの検証用コピーで行い、ソースと成果物をWSL保存先へ戻しています。

`scripts/sync_to_wsl.py` はPythonでソースファイルをコピーする補助です。宛先の既存ファイルは削除せず、Library / Temp / Logs / Buildなどは除外します。

```bash
python3 scripts/sync_to_wsl.py \
  '/mnt/c/path/to/world-interaction' \
  '/home/dpc7/world interaction'
```

同期は単方向です。Windows側とWSL側で同時編集せず、作業側を決めてから実行してください。削除は同期しないため、不要ファイルを除く場合はGit差分を確認して手動で整理します。

APKとresultsはこのスクリプトの対象外なので別途コピーします。`Assets/ImportedWorlds` はGit対象外ですが、コピーの対象には含まれます。公開するコードと、ローカルのシーンデータ・実験結果を区別してください。
