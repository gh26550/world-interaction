# World Interaction

**Unity / Meta Quest Pro 向け：VRタスクの要求から Mesh・Gaussian Splatting・Hybrid を割り当てる研究用システム。**

WorldMeshなどの生成器を上流に置き、生成済みシーンの観察・把持・配置・衝突を扱います。まず制御されたデモで要求ベースの割当てを検証し、準備・校正した実シーンへ適用する構成です。

## 実装した機能

- Observe / Transport / PrecisionPlace の3タスク。同一物体について観察要求・把持・支持面・接触精度・未観測面の必要性を評価。
- 候補の可否を検査し、要求を満たす候補から宣言コストが最小の表現を選択。未校正・欠損アセット・要求違反を明示。
- Mesh-only / Mesh+LOD / Uniform Hybrid / Category-distance / Manual Hybrid / Aggressive GS / Scene-level GS と提案手法の比較。
- Rigidbody + ConfigurableJoint による把持、重力、衝突、安定配置の判定。把持中・試行中の表現変更を禁止。
- OpenXR / Touchコントローラー、PCのキーボード・マウス操作。
- 異方性Gaussianの共分散投影、深度ソート、Gaussian不透明度による実際の描画。点群表示による代用ではありません。
- イベント・割当て・要求・候補・接触面の幾何差・フレーム間隔・CPU/GPU計測・試行結果の保存。
- WorldMesh GLBの物体ノード変換、訓練済みGaussian PLYの変換、Unityへの取り込み。

## 起動

1. **Unity Hubで `UnityProject` を開く**（検証版：Unity **6000.2.7f2**）。Androidの場合はAndroid Build Support / SDK / NDK / OpenJDKを追加。
2. `Assets/Scenes/InteractionLab.unity` を開いて Play。
3. Sceneがない場合は `World Interaction > Create Demo Scene`。
4. 緑のブロックを把持し、右側の青い目標位置へ配置。

### PC操作

| 操作 | 入力 |
|---|---|
| 移動 | WASD |
| 視点 | 右マウスを押しながら移動 |
| 把持・解放 | 対象へ視点を向けてEを押し続ける／離す |
| 把持距離 | マウスホイール |
| 試行の開始・中止 | Space |
| タスク切替 | T |
| 表現ポリシー・手動割当て | 画面左のパネル |

### Meta Quest Pro

まずPC Link / Air Linkで実行できます。PCのOpenXRランタイムをMeta Quest Linkに設定してください。単体APKは `World Interaction > Build Quest APK` で `UnityProject/Builds/Android/WorldInteraction.apk` へ生成します。開発者モードの端末へ `adb install -r` で導入できます。

| 操作 | 入力 |
|---|---|
| 移動 | 左スティック |
| 把持・解放 | 対象に向けたコントローラーのGrip |
| 試行開始・中止 | 右A |
| タスク切替 | 右B |
| ポリシー切替 | 左X |

OpenXRはMulti Pass、AndroidはVulkan / ARM64 / IL2CPPを使用。ヘッドセットの実フレームレート・左右眼の見え方・コントローラー追従は実機で別途確認が必要です。PCのテスト結果をQuest Pro性能として報告しません。

## 要求と候補を変更する

`InteractionObject` に物体ID・target/support・Mesh/GS参照・Collider・候補の能力と誤差を保持します。

`Create > World Interaction > Task Requirement Profile` でタスク別の要求アセットを作り、`ExperimentSession.taskProfiles` に登録できます。物体ID単位で、接触誤差上限(mm)、最接近距離(m)、視覚品質、把持・移動・裏面の必要性を指定します。未指定物体には既定ルールが適用されます。

**デモ候補のコスト1/2/3と視覚品質値は説明用の宣言値で、速度や知覚品質の測定結果ではありません。** 測定した候補コストをInspectorへ設定し、`costSource` に測定条件を記入してください。表現が欠ける場合や要求を満たせない場合、通常の試行開始はブロックされます。意図的な劣化ベースラインは明示的なチェックで許可でき、違反はログに残ります。

初期デモは箱なのでMesh+LODの形状削減効果はありません。実データでは `lowMeshVisual` に実際のLODを割り当ててください。

## WorldMeshを使う

WSLに専用環境を用意します。

```bash
cd '/home/dpc7/world interaction'
python3 -m venv .venv
.venv/bin/pip install -r tools/requirements.txt
.venv/bin/python tools/prepare_worldmesh.py scene \
  --scene '/path/to/worldmesh/output/scene_with_all_objects.glb' \
  --output 'UnityProject/Assets/ImportedWorlds/my-world' --source-up z
```

1. 出力の `world-interaction.json` を確認。GLBのメッシュノードを仮の物体境界として保持します。
2. Unityの新規シーンで `World Interaction > Import Prepared WorldMesh Manifest` を実行。
3. 必要な物体の `target / support / movable`、衝突形状、操作能力、裏面の完成度、誤差・視覚品質を確認・校正。
4. インポートしたルートを選び `World Interaction > Create Experiment From Selected World`。
5. XR Originの開始位置、Placement Destination、候補とTaskProfileを設定し、シーンを保存。

**未校正の取り込み候補は `qualityVerified=false` です。** 自動で精密な衝突形状を推定したことにはしません。初期ColliderはAABBなので、壁の扉・窓を塞ぐことがあります。静的構造には必要に応じてMeshCollider、可動物には確認済みの凸形状・複合Colliderを設定してください。

### 実際のGSアセット

```bash
.venv/bin/python tools/prepare_worldmesh.py ply \
  --input '/path/to/object.ply' \
  --transform '/path/to/ply-to-unity.json' \
  --output 'UnityProject/Assets/ImportedWorlds/my-world/object.gs.bytes' \
  --max-splats 30000
```

`ply-to-unity.json` は明示的な4×4座標変換行列です。物体GSでは、その物体のUnityローカル座標へ変換します。シーン全体の学習済みGSはGLBと座標が違う場合があり、Nerfstudioのscale/transformを確認せず同一視しないでください。変換後のファイルをmanifestの対応物体の `gaussians` へ指定します。

変換はGaussianの位置・異方性スケール・回転・不透明度・SH0色を保持します。高次SHは使用せず、上限超過時は決定的な間引きです。GLBのテクスチャは頂点色へサンプリングするため、元の材質を無損失で再現するものではありません。

## 計測と結果

試行ごとに `Application.persistentDataPath/experiments/<id>/` へ保存します。

- `assignments.json`：要求・選択理由・違反・全候補・コストの出典
- `events.jsonl`：開始、把持、解放、接触、衝撃、落下、終了
- `frames.csv`：ウォームアップ後のフレーム間隔
- `consistency.json`：基準メッシュ頂点法線から有効Colliderまでのレイによる差(mm)、欠測数
- `summary.json`：成功、時間、配置誤差、p50/p95/p99、フレーム予算超過率、CPU/GPUタイミング

CPU割当てメモリをVRAMと呼びません。利用できないGPUタイミングは `-1`。CPU/GPUタイミングはUnityのFrameTimingManagerによる取得で、GPU単独の表現コストを直接同定するものではありません。

```bash
.venv/bin/python tools/analyze_results.py '/path/to/experiments' --output comparison.csv
```

`BenchmarkSweep` のコンテキストメニューで固定視点の描画比較を実行できます。これは参加者の操作実験ではなく、`render-only benchmark` と記録されます。Mesh/GSのbreak-evenは同一機器・タスク・視点・品質条件の実測から判断してください。

## 現時点の範囲

- 静的な表現割当てと剛体操作を実装。関節要求は能力チェックのみで、扉や変形物の操作は未実装。
- GS描画は小規模な検証用。CPUソート、SH0、オブジェクト内ソート。オブジェクトをまたぐ半透明の交差は完全には扱いません。SceneGS条件は対象物体のGaussianを結合してソートしますが、共通の床・壁・ガイドは実験環境として残します。
- 操作時の背景穴埋め・物体GS自動分割・学習・再照明は実装していません。個別GSは物体ローカル座標で準備し、裏面と背景を確認してください。
- `consistency.json` は幾何診断で、知覚実験やGaussianの視覚表面そのものの計測ではありません。
- Dynamic Representation Assignment、LLM/VLMによる要求抽出は将来拡張。

## テスト

Unity Test RunnerのEditMode / PlayModeを使用。変換・解析ツールは次で実行します。

```bash
.venv/bin/python -m unittest discover -s tools -p 'test_*.py' -v
```

構成と評価上の注意は [docs/RESEARCH_PROTOCOL.md](docs/RESEARCH_PROTOCOL.md)、検証結果は [docs/VALIDATION.md](docs/VALIDATION.md) を参照。

モデル重み、WorldMesh生成物、実験ログ、認証情報、ビルド出力はリポジトリに含めません。

Unityが設定を再保存した後は、公開前に `python tools/sanitize_unity_settings.py` を実行してください。本研究で使わないコンソール用の自動生成passcode欄を除去し、署名・認証関連項目が空であることを確認します。
