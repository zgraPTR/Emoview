#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

/// <summary>
/// アニメーションの操作を行うクラス
/// </summary>
public class AnimationProcessor
{
    private readonly Dictionary<string, string> _animFiles = new();     // アニメーションファイル名とパスの辞書
    private readonly Logger _logger;                                    // ロガーインスタンス
    private readonly CameraController _cameraController;                // カメラ操作用クラス
    private Texture2D _texture2D;                                       // Texture2Dを再利用する変数
    public int AnimCount => _animFiles.Count;                           // アニメーションファイル数取得

    public AnimationProcessor(Logger logger)
    {                                                                   // コンストラクタ
        _logger = logger;                                               // Loggerインスタンスを設定
        _cameraController = new CameraController(_logger);              // カメラコントローラ生成
    }

    /// <summary>
    /// アニメーション入りフォルダを選択する処理
    /// </summary>
    public void SelectAnimFolder()
    {
        _animFiles.Clear();                                             // 既存のアニメーションリストをクリア
        string folderAbs = EditorUtility.OpenFolderPanel(".animのフォルダを選択", Application.dataPath, ""); // フォルダ選択ダイアログ
        if (string.IsNullOrEmpty(folderAbs))
        {                                                               // フォルダが選択されなかった場合
            return;                                                     // 処理終了
        }

        string folderRel = FileUtil.GetProjectRelativePath(folderAbs);  // プロジェクト相対パス取得
        string root = Directory.Exists(folderRel) ? folderRel : folderAbs; // 実在フォルダをrootに設定

        foreach (var f in Directory.GetFiles(root, "*.anim", SearchOption.AllDirectories))
        {                                                               // フォルダ内の.animファイル全てを取得
            _animFiles[Path.GetFileNameWithoutExtension(f)] = f.Replace('\\', '/'); // 辞書に追加（ファイル名→パス）
        }

        _logger.Log($"{_animFiles.Count} 個のアニメーションクリップを検出"); // 検出結果をログ出力
    }

    /// <summary>
    /// アニメーションを順次処理してレンダリングし、保存する
    /// </summary>
    /// <param name="descriptor">VRCAvatarDescriptor</param>
    /// <param name="outputPath">出力フォルダパス</param>
    /// <param name="resolution">解像度</param>
    public async Task ProcessAnimationsAsync(VRCAvatarDescriptor descriptor, string outputPath, int resolution)
    {
        if (descriptor == null)
        {                                                               // アバターが未設定の場合
            _logger.Log("エラー: アバター未選択");                      // エラーログ
            return;                                                     // 処理終了
        }

        _cameraController.SetupCamera(resolution);                      // カメラセットアップ
        RenderTexture rt = _cameraController.RenderTexture;             // カメラのRenderTextureを取得
        _texture2D = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); // Texture2Dを生成

        foreach (var kv in _animFiles)
        {                                                               // 各アニメーションファイル処理
            AnimationClip clip = LoadClip(kv.Value);                    // アニメーション読み込み
            if (clip == null)
            {                                                           // 読み込み失敗の場合
                _logger.Log($"読み込み失敗: {kv.Key}");                 // エラーログ
                continue;                                               // 次のアニメーションへ
            }

            GameObject working = Object.Instantiate(descriptor.gameObject); // アバターのコピー生成
            working.name += "__EmoViewPreview";                     // 名前を変更
            working.SetActive(true);                                // アクティブ化

            Animator animator = working.GetComponent<Animator>();   // Animator取得
            bool prevRootMotion = false;                            // 元のルートモーション保存用
            if (animator != null)
            {                                                       // Animatorが存在する場合
                prevRootMotion = animator.applyRootMotion;          // 元のルートモーションを保存
                animator.applyRootMotion = false;                   // 処理中は無効化
            }

            AnimationMode.StartAnimationMode();                     // AnimationMode開始

            _logger.Log($"撮影: {kv.Key}");                         // 撮影開始ログ

            float sampleTime = RepresentativeTime(clip);            // 代表フレーム時間計算
            AnimationMode.SampleAnimationClip(working, clip, sampleTime); // アニメーション適用

            EditorApplication.QueuePlayerLoopUpdate();              // エディタ更新キュー追加
            await Task.Yield();                                     // 非同期で待機

            _cameraController.AutoFrame(descriptor, working);       // カメラ自動フレーム
            RenderAndSave(kv.Key, outputPath);                      // 描写と保存処理

            AnimationMode.StopAnimationMode();                      // AnimationMode停止

            if (animator != null)
            {                                                       // Animatorが存在する場合
                animator.applyRootMotion = prevRootMotion;          // 元の状態に戻す
            }

            working.SetActive(false);                               // アバターを非表示にする
            await Task.Yield();                                     // 次フレーム待機
            Object.DestroyImmediate(working);                       // アバターコピー破棄
        }

        _cameraController.Cleanup();                                // カメラ後始末
        Object.DestroyImmediate(_texture2D);                        // Texture2D破棄
        _texture2D = null;                                          // 参照クリア

        _logger.Log("全ての処理が完了しました。");                      // 完了ログ
    }

    /// <summary>
    /// AnimationClipをロードする
    /// </summary>
    /// <param name="path">アニメーションパス</param>
    /// <returns>AnimationClipまたはnull</returns>
    private AnimationClip LoadClip(string path)
    {
        if (string.IsNullOrEmpty(path))
        {                                                           // パスが空の場合
            return null;                                           // nullを返す
        }

        return path.StartsWith("Assets") ? AssetDatabase.LoadAssetAtPath<AnimationClip>(path) : null; // アセット読み込み
    }

    /// <summary>
    /// 代表的な時間（代表フレーム）を計算
    /// </summary>
    /// <param name="clip">AnimationClip</param>
    /// <returns>代表時間</returns>
    private float RepresentativeTime(AnimationClip clip)
    {
        if (clip == null || clip.length <= Mathf.Epsilon)
        {                                                           // 無効なクリップの場合
            return 0f;                                             // 0を返す
        }

        if (clip.isLooping)
        {                                                           // ループする場合
            return clip.length * 0.5f;                              // 途中の時間を返す
        }
        else
        {                                                           // ループしない場合
            return Mathf.Max(0f, clip.length - 1f / Mathf.Max(30f, clip.frameRate)); // 最後のフレーム付近の時間を返す
        }
    }

    /// <summary>
    /// RenderTexture描写とPNG保存処理
    /// </summary>
    /// <param name="animName">アニメーション名</param>
    /// <param name="outputPath">出力フォルダ</param>
    private void RenderAndSave(string animName, string outputPath)
    {
        Camera cam = _cameraController.RenderCamera;               // カメラ取得
        if (cam == null)
        {                                                           // カメラが未設定の場合
            _logger.Log("エラー: カメラ未設定");                  // エラーログ
            return;                                                 // 処理終了
        }

        RenderTexture rt = _cameraController.RenderTexture;        // RenderTexture取得
        cam.Render();                                               // 描画実行

        RenderTexture.active = rt;                                  // RenderTextureをアクティブ化
        _texture2D.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); // ピクセル読み込み
        _texture2D.Apply();                                         // Texture2Dに反映
        RenderTexture.active = null;                                // 非アクティブ化

        if (!Directory.Exists(outputPath))
        {                                                           // 出力フォルダが存在しない場合
            Directory.CreateDirectory(outputPath);                 // 作成
        }

        string savePath = Path.Combine(outputPath, animName + ".png"); // 出力ファイルパス生成
        File.WriteAllBytes(savePath, _texture2D.EncodeToPNG());       // PNG書き込み

        _logger.Log($"保存: {savePath}");                          // 保存ログ
    }
}
#endif
