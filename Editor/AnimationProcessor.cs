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
    private readonly Dictionary<string, string> _animFiles = new(); // アニメーションファイルリスト
    private readonly Logger _logger;
    private readonly CameraController _cameraController;            // カメラを扱うクラスインスタンス
    private Texture2D _texture2D;                                   // Texture2D を再利用するための変数
    public int AnimCount => _animFiles.Count;                       // アニメーションファイル数

    public AnimationProcessor(Logger logger)
    {                                                               // コンストラクタ
        _logger = logger;                                           // Loggerインスタンスを受け取り
        _cameraController = new CameraController(_logger);          // カメラを扱うクラスインスタンス生成
    }

    /// <summary>
    /// アニメーション入りフォルダ選択時の関数
    /// </summary>
    public void SelectAnimFolder()
    {
        _animFiles.Clear();                                         // アニメーションファイルリストクリア
        string folderAbs = EditorUtility.OpenFolderPanel(".animのフォルダを選択", Application.dataPath, "");    // フォルダ選択ダイアログ表示
        if (string.IsNullOrEmpty(folderAbs)) return;                // 空なら終了

        string folderRel = FileUtil.GetProjectRelativePath(folderAbs);
        string root = Directory.Exists(folderRel) ? folderRel : folderAbs;

        foreach (var f in Directory.GetFiles(root, "*.anim", SearchOption.AllDirectories))
        {                                                           // アニメーションファイル一覧
            _animFiles[Path.GetFileNameWithoutExtension(f)] = f.Replace('\\', '/');
        }

        _logger.Log($"{_animFiles.Count} 個のアニメーションクリップを検出");
    }

    public async Task ProcessAnimationsAsync(VRCAvatarDescriptor descriptor, string outputPath, int resolution)
    {
        if (descriptor == null)
        {
            _logger.Log("エラー: アバター未選択");
            return;
        }

        // RenderTexture と Texture2D を使い回す
        _cameraController.SetupCamera(resolution);
        RenderTexture rt = _cameraController.RenderTexture;
        _texture2D = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);

        foreach (var kv in _animFiles)
        {                                                           // アニメーションファイル毎の処理
            AnimationClip clip = LoadClip(kv.Value);                // アニメーションファイル読み込み
            if (clip == null)
            {                                                       // 内容無し
                _logger.Log($"読み込み失敗: {kv.Key}");
                continue;
            }

            GameObject working = Object.Instantiate(descriptor.gameObject); // アバターを生成
            working.name += "__EmoViewPreview";                             // 名前の末尾に追加
            working.SetActive(true);                                        // 有効化

            Animator animator = working.GetComponent<Animator>();           // アニメーションコンポーネント取得
            bool prevRootMotion = animator ? animator.applyRootMotion : false;
            if (animator) animator.applyRootMotion = false;                 // ルートのアニメーション無効化

            AnimationMode.StartAnimationMode();                             // アニメーションモードにする

            _logger.Log($"撮影: {kv.Key}");

            float sampleTime = RepresentativeTime(clip);
            AnimationMode.SampleAnimationClip(working, clip, sampleTime);       // アバターを生成

            EditorApplication.QueuePlayerLoopUpdate();

            await Task.Yield();

            _cameraController.AutoFrame(descriptor, working);                   // アバターを生成
            RenderAndSave(kv.Key, outputPath);                                  // 描写と保存を行う

            AnimationMode.StopAnimationMode();                                  // アニメーションモードを止める

            if (animator) animator.applyRootMotion = prevRootMotion;            // ルートのアニメーションを戻す

            Object.DestroyImmediate(working);                                   // 使用したアバターを削除
        }

        _cameraController.Cleanup();                                            // 破棄など後始末
        Object.DestroyImmediate(_texture2D);                                    // Texture2D 破棄
        _texture2D = null;

        _logger.Log("全ての処理が完了しました。");
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="path">アニメーションファイルパス</param>
    /// <returns>AnimationClip or null</returns>
    private AnimationClip LoadClip(string path)
    {
        return path.StartsWith("Assets") ? AssetDatabase.LoadAssetAtPath<AnimationClip>(path) : null;
    }

    /// <summary>
    /// 代表的な時間（代表フレーム）」を計算する処理
    /// </summary>
    /// <param name="clip">AnimationClip</param>
    /// <returns>null </returns>
    private float RepresentativeTime(AnimationClip clip)
    {
        if (clip == null || clip.length <= Mathf.Epsilon) return 0f;
        return clip.isLooping ? clip.length * 0.5f : Mathf.Max(0f, clip.length - 1f / Mathf.Max(30f, clip.frameRate));
    }

    /// <summary>
    /// 描写と保存を行う
    /// </summary>
    /// <param name="animName">アニメーションファイル名</param>
    /// <param name="outputPath">出力フォルダ</param>
    private void RenderAndSave(string animName, string outputPath)
    {
        Camera cam = _cameraController.RenderCamera;
        if (cam == null) { _logger.Log("エラー: カメラ未設定"); return; }

        RenderTexture rt = _cameraController.RenderTexture;     // カメラのRenderTexture取得
        cam.Render();                                           // RenderTextureに描写

        RenderTexture.active = rt;                              // RenderTextureをアクティブにする
        _texture2D.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);   // _texture2D に読み込み。
        _texture2D.Apply();                                     // 読み込んで反映
        RenderTexture.active = null;                            // 非アクティブ化

        if (!Directory.Exists(outputPath)) Directory.CreateDirectory(outputPath);   // 出力先のフォルダがなければ作成

        string savePath = Path.Combine(outputPath, animName + ".png");              // 出力先ファイルパス
        File.WriteAllBytes(savePath, _texture2D.EncodeToPNG());                     // 出力先に書き込む

        _logger.Log($"保存: {savePath}");
    }
}
#endif
