#if UNITY_EDITOR
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

/// <summary>
/// EmoView 用 EditorWindow
/// </summary>
public class EmoViewEditorWindow : EditorWindow
{
    private VRCAvatarDescriptor _avatarDescriptor;                   // 選択されたアバター
    private string _outputPath = "Assets";                           // 出力フォルダパス初期値
    private int _renderResolution = 1024;                            // 画像解像度初期値

    private AnimationProcessor _animProcessor;                       // アニメーション処理クラス
    private Logger _logger;                                           // ロガークラス

    private Vector2 _logScroll;                                       // ログスクロール位置

    [MenuItem("Tools/RRT/EmoView")]
    public static void ShowWindow()
    {
        GetWindow<EmoViewEditorWindow>("EmoView").minSize = new Vector2(560, 800); // ウィンドウ生成
    }

    private void OnEnable()
    {
        _logger = new Logger();                                       // Logger生成
        _animProcessor = new AnimationProcessor(_logger);             // AnimationProcessor生成
    }

    private void OnGUI()
    {
        DrawHeader();               // ヘッダー表示
        DrawAvatarField();          // アバター選択フィールド表示
        DrawSettings();             // 解像度・出力先・.animフォルダ選択
        DrawControls();             // 画像生成ボタン、ログクリアボタン表示
        DrawLogArea();              // ログエリア表示
    }

    /// <summary>
    /// ヘッダー表示
    /// </summary>
    private void DrawHeader()
    {
        EditorGUILayout.Space();                                         // 空白スペース
        EditorGUILayout.LabelField("EmoView - Avatar Expression Capture", EditorStyles.boldLabel); // ヘッダータイトル
        EditorGUILayout.LabelField("VRCAvatarDescriptor を持つオブジェクトを選択してください。", EditorStyles.miniLabel); // 説明文
        EditorGUILayout.Space();                                         // 空白スペース
    }

    /// <summary>
    /// アバターフィールド表示
    /// </summary>
    private void DrawAvatarField()
    {
        EditorGUILayout.LabelField("アバター選択", EditorStyles.boldLabel); // ラベル表示
        _avatarDescriptor = (VRCAvatarDescriptor)EditorGUILayout.ObjectField(
            new GUIContent("VRC Avatar Descriptor"),                      // フィールドラベル
            _avatarDescriptor,                                             // 現在の値
            typeof(VRCAvatarDescriptor),                                   // 許可する型
            true                                                            // シーン内オブジェクト許可
        );
    }

    /// <summary>
    /// アニメーション、出力先フォルダ選択
    /// </summary>
    private void DrawSettings()
    {
        EditorGUILayout.Space();                                         // 空白スペース
        EditorGUILayout.LabelField("撮影設定", EditorStyles.boldLabel);   // 設定ラベル

        using (new EditorGUI.IndentLevelScope())
        {                                                               // インデント調整
            _renderResolution = Mathf.Max(256, EditorGUILayout.IntField("画像の解像度 (px)", _renderResolution)); // 解像度設定

            using (new EditorGUILayout.HorizontalScope())
            {                                                           // 出力フォルダ選択行
                EditorGUILayout.PrefixLabel("画像出力フォルダ");       // 左側ラベル
                EditorGUILayout.SelectableLabel(_outputPath, GUILayout.Height(EditorGUIUtility.singleLineHeight)); // 選択表示
                if (GUILayout.Button("選択", GUILayout.Width(90)))
                {                                                       // ボタン押下時
                    string selected = EditorUtility.OpenFolderPanel("出力フォルダを選択", _outputPath, ""); // フォルダ選択
                    if (!string.IsNullOrEmpty(selected))
                    {                                                   // 選択された場合
                        _outputPath = selected;                        // 出力先更新
                        _logger.Log($"保存先フォルダ: {_outputPath}"); // ログ出力
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {                                                           // .animフォルダ選択行
                EditorGUILayout.LabelField($"読み込み済み: {_animProcessor.AnimCount} クリップ"); // 左側ラベル
                GUILayout.FlexibleSpace();                              // ボタンを右端に寄せる余白
                if (GUILayout.Button(".anim入りフォルダを選択", GUILayout.Height(24)))
                {                                                       // ボタン押下時
                    _animProcessor.SelectAnimFolder();                 // フォルダ選択処理
                }
                GUILayout.FlexibleSpace();                              // 余白
            }
        }
    }

    /// <summary>
    /// 画像生成・ログクリアボタン表示
    /// </summary>
    private void DrawControls()
    {
        EditorGUILayout.Space();                                         // 空白スペース
        using (new EditorGUILayout.HorizontalScope())
        {                                                               // 横並びレイアウト
            GUI.enabled = _avatarDescriptor != null && _animProcessor.AnimCount > 0; // 条件付きで有効化

            if (GUILayout.Button("表情一覧画像を作成", GUILayout.Height(40)))
            {                                                           // 画像生成ボタン押下時
                SafeRun(async () =>
                {                                                       // 例外処理付き非同期呼び出し
                    bool wasActive = _avatarDescriptor.gameObject.activeSelf; // 元の表示状態保存
                    if (wasActive)
                    {                                                   // 表示中なら
                        _avatarDescriptor.gameObject.SetActive(false); // 非表示
                    }

                    await _animProcessor.ProcessAnimationsAsync(_avatarDescriptor, _outputPath, _renderResolution); // 撮影処理

                    if (wasActive)
                    {                                                   // 元々表示されていた場合
                        _avatarDescriptor.gameObject.SetActive(true);  // 再表示
                    }
                });
            }

            GUI.enabled = true;                                         // ボタン状態戻す

            if (GUILayout.Button("ログをクリア", GUILayout.Height(40), GUILayout.Width(140)))
            {                                                           // ログクリアボタン押下時
                _logger.Clear();                                        // ログクリア
            }
        }
    }

    /// <summary>
    /// ログエリア表示
    /// </summary>
    private void DrawLogArea()
    {
        EditorGUILayout.Space();                                         // 空白スペース
        EditorGUILayout.LabelField("ログ", EditorStyles.boldLabel);      // ラベル表示

        _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.MinHeight(420)); // スクロール開始
        foreach (var line in _logger.Lines)
        {                                                               // 各ログ行表示
            EditorGUILayout.SelectableLabel(line, GUILayout.Height(16)); // 選択可能ラベル表示
        }
        EditorGUILayout.EndScrollView();                                 // スクロール終了
    }

    /// <summary>
    /// 例外キャッチ用非同期ラッパー
    /// </summary>
    /// <param name="taskFunc">非同期処理</param>
    private void SafeRun(System.Func<Task> taskFunc)
    {
        _ = RunInternal();                                               // 非同期実行
        async Task RunInternal()
        {                                                               // 内部非同期処理
            try
            {
                await taskFunc();                                        // 処理実行
            }
            catch (System.Exception e)
            {                                                           // 例外発生時
                _logger.Log("例外: " + e);                               // ログ出力
            }
        }
    }
}
#endif
