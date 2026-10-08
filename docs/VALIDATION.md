# 検証記録

検証日：2026-10-08。Unity **6000.2.7f2** / Windows Editor、Python **3.10** / Ubuntu 22.04 WSL。

| 検証 | 結果 |
|---|---|
| Unity EditMode | **12 / 12 PASS** |
| Unity PlayMode | **7 / 7 PASS** |
| Python変換・解析テスト | **6 / 6 PASS** |
| WorldMesh実データ変換 | **28メッシュノード**を変換 |
| Unityへの実データ取り込み | **28 InteractionObject**の生成を確認 |
| Android ARM64 / Vulkan APK | **ビルド成功** |
| GPUによるGS描画 | テスト内で緑色GS物体の描画を確認し、プレビュー画像を保存 |
| Meta Quest Pro実機 | **未実施**（追従・両眼表示・操作感・FPSは未測定） |

## Unityテストの範囲

- 同一物体の要求が観察・運搬・精密配置で変わると選択結果が変わる。
- 支持面への接触要求、未校正候補、裏面不足、欠損アセット、ベースラインの要求違反を検出。
- コスト同値の決定性、Gaussianバイナリの不正入力拒否、異方性の保持、パーセンタイル計算。
- 把持中の表現・タスク・ポリシー変更禁止、表現変更時の位置保持。
- 重力と支持面、Jointによる把持・解放、SceneGSからMeshへの復帰。
- 配置目標での安定化による成功判定、試行ログファイル生成。
- Gaussianを実際に描画して画像を取得。

## データ変換の範囲

- 軸変更後の単位・メッシュ寸法。
- Gaussianの位置、スケール、不透明度、**鏡映を含む軸変換後の共分散**。
- 不正な非一様変換・途切れたPLYの拒否。
- 集計時に異なるタスクを混ぜない。

実データ取り込みは、内容と座標を校正して操作可能にしたことを意味しない。取り込んだ物体候補は未校正として登録し、確認前の試行をブロックする。

## 成果物と再実行

- APK：`UnityProject/Builds/Android/WorldInteraction.apk`（ローカルのみ）
- NUnit結果・ビルドログ・プレビュー：`results/`（ローカルのみ）
- 再実行：Windows PowerShellで `scripts/verify-all.ps1`
- Python：`python -m unittest discover -s tools -p 'test_*.py' -v`

これらは実装の動作検証であり、提案手法の優位性、Mesh/GSのbreak-even、Quest Proの性能を示す研究結果ではない。研究比較には、校正済みアセット・同一実行条件・参加者による操作実験が必要。
