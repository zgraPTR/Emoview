#if UNITY_EDITOR
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

public class EmoViewEditorWindow : EditorWindow
{
    private VRCAvatarDescriptor _avatarDescriptor;
    private string _outputPath = "Assets";
    private int _renderResolution = 1024;

    private AnimationProcessor _animProcessor;
    private Logger _logger;

    private Vector2 _logScroll;

    [MenuItem("Tools/RRT/EmoView")]
    public static void ShowWindow()
    {
        GetWindow<EmoViewEditorWindow>("EmoView").minSize = new Vector2(560, 800);
    }

    private void OnEnable()
    {
        _logger = new Logger();
        _animProcessor = new AnimationProcessor(_logger);
    }

    private void OnGUI()
    {
        DrawHeader();               // ヘッダー表示
        DrawAvatarField();          // アバターフィールド表示
        DrawSettings();             // アニメーション、出力先フォルダ選択
        DrawControls();             // 画像生成、ログクリアボタン表示
        DrawLogArea();              // ログエリア表示
    }

    /// <summary>
    /// ヘッダー表示
    /// </summary>
    private void DrawHeader()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("EmoView - Avatar Expression Capture", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("VRCAvatarDescriptor を持つオブジェクトを選択してください。", EditorStyles.miniLabel);
        EditorGUILayout.Space();
    }

    /// <summary>
    /// アバターフィールド表示
    /// </summary>
    private void DrawAvatarField()
    {
        EditorGUILayout.LabelField("アバター選択", EditorStyles.boldLabel);
        _avatarDescriptor = (VRCAvatarDescriptor)EditorGUILayout.ObjectField(
            new GUIContent("VRC Avatar Descriptor"),
            _avatarDescriptor,
            typeof(VRCAvatarDescriptor),
            true
        );
    }

    /// <summary>
    /// アニメーション、出力先フォルダ選択
    /// </summary>
    private void DrawSettings()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("撮影設定", EditorStyles.boldLabel);

        using (new EditorGUI.IndentLevelScope())
        {
            _renderResolution = Mathf.Max(256, EditorGUILayout.IntField("画像の解像度 (px)", _renderResolution));

            using (new EditorGUILayout.HorizontalScope())
            {                                                                               // 出力フォルダ選択
                EditorGUILayout.PrefixLabel("画像出力フォルダ");
                EditorGUILayout.SelectableLabel(_outputPath, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button("選択", GUILayout.Width(90)))
                {
                    string selected = EditorUtility.OpenFolderPanel("出力フォルダを選択", _outputPath, "");
                    if (!string.IsNullOrEmpty(selected))
                    {
                        _outputPath = selected;
                        _logger.Log($"保存先フォルダ: {_outputPath}");
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {                                                                               // .animフォルダ選択
                EditorGUILayout.LabelField($"読み込み済み: {_animProcessor.AnimCount} クリップ");         // 左側ラベルは固定幅にする（これでボタンが右に押される）
                GUILayout.FlexibleSpace(); // 余白を入れてボタンを右端へ
                if (GUILayout.Button(".anim入りフォルダを選択", GUILayout.Height(24)))
                {
                    _animProcessor.SelectAnimFolder();
                }
                GUILayout.FlexibleSpace();
            }
        }
    }

    private void DrawControls()
    {
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = _avatarDescriptor != null && _animProcessor.AnimCount > 0;

            if (GUILayout.Button("表情一覧画像を作成", GUILayout.Height(40)))
            {
                SafeRun(async () =>
                {
                    bool wasActive = _avatarDescriptor.gameObject.activeSelf;               // 元の表示状態を保持
                    if (wasActive) _avatarDescriptor.gameObject.SetActive(false);           // 表示中なら非表示に

                    await _animProcessor.ProcessAnimationsAsync(_avatarDescriptor, _outputPath, _renderResolution); // 撮影処理

                    if (wasActive) _avatarDescriptor.gameObject.SetActive(true);            // 元々表示されていた場合のみ再表示
                });
            }

            GUI.enabled = true;

            if (GUILayout.Button("ログをクリア", GUILayout.Height(40), GUILayout.Width(140)))
            {
                _logger.Clear();
            }
        }
    }

    /// <summary>
    /// ログエリア表示
    /// </summary>
    private void DrawLogArea()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("ログ", EditorStyles.boldLabel);

        _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.MinHeight(420));
        foreach (var line in _logger.Lines)
        {
            EditorGUILayout.SelectableLabel(line, GUILayout.Height(16));
        }
        EditorGUILayout.EndScrollView();
    }

    /// <summary>
    /// 例外キャッチ用
    /// </summary>
    /// <param name="taskFunc"></param>
    private void SafeRun(System.Func<Task> taskFunc)
    {
        _ = RunInternal();
        async Task RunInternal()
        {
            try
            {
                await taskFunc();
            }
            catch (System.Exception e)
            {
                _logger.Log("例外: " + e);
            }
        }
    }
}
#endif
