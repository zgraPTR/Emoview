#if UNITY_EDITOR
using UnityEngine;
using VRC.SDK3.Avatars.Components;

/// <summary>
/// カメラ操作とRenderTexture管理クラス
/// </summary>
public class CameraController
{
    private Camera _camera;                                         // カメラコンポーネント
    private RenderTexture _renderTexture;                           // 描画用RenderTexture
    private readonly Logger _logger;                                 // ロガー

    public Camera RenderCamera => _camera;                          // カメラ取得
    public RenderTexture RenderTexture => _renderTexture;           // RenderTexture取得

    public CameraController(Logger logger)
    {                                                               // コンストラクタ
        _logger = logger;                                           // Loggerを保存
    }

    /// <summary>
    /// カメラとRenderTextureのセットアップ
    /// </summary>
    /// <param name="resolution">解像度(px)</param>
    public void SetupCamera(int resolution)
    {
        if (_renderTexture != null)
        {                                                           // 既に作成済みの場合
            return;                                                 // 再利用
        }

        _renderTexture = new RenderTexture(resolution, resolution, 16, RenderTextureFormat.ARGB32)
        {                                                           // RenderTexture生成
            name = "EmoView_RT",                                    // 名前設定
            antiAliasing = 4                                        // アンチエイリアス設定
        };

        GameObject camObj = new GameObject("EmoView_PreviewCamera");    // カメラオブジェクト生成
        _camera = camObj.AddComponent<Camera>();                    // Cameraコンポーネント追加
        _camera.targetTexture = _renderTexture;                     // RenderTexture指定
        _camera.clearFlags = CameraClearFlags.SolidColor;           // 背景色でクリア
        _camera.backgroundColor = new Color(0.15f, 0.15f, 0.15f);   // 背景色指定
        _camera.nearClipPlane = 0.01f;                              // ニアクリップ設定
        _camera.farClipPlane = 10f;                                 // ファークリップ設定
        _camera.fieldOfView = 30f;                                  // FOV設定
    }

    /// <summary>
    /// 顔全体を映すカメラ配置
    /// </summary>
    /// <param name="descriptor">VRCAvatarDescriptor</param>
    /// <param name="avatar">アバターGameObject</param>
    public void AutoFrame(VRCAvatarDescriptor descriptor, GameObject avatar)
    {
        if (_camera == null || descriptor == null || avatar == null)
        {                                                           // カメラ・アバター・デスクリプタのnullチェック
            return;                                                 // 処理終了
        }

        Animator animator = avatar.GetComponent<Animator>();        // Animator取得
        if (animator == null)
        {                                                           // Animatorが存在しない場合
            return;                                                 // 処理終了
        }

        Transform[] faceParts = new Transform[]
        {                                                           // 顔パーツのTransform取得
            animator.GetBoneTransform(HumanBodyBones.Head),         // 頭
            animator.GetBoneTransform(HumanBodyBones.LeftEye),      // 左目
            animator.GetBoneTransform(HumanBodyBones.RightEye)      // 右目
        };

        Vector3 center = descriptor.transform.TransformPoint(descriptor.ViewPosition); // 顔中心の初期値
        int count = 1;                                             // 中心計算用カウンタ

        foreach (Transform part in faceParts)
        {                                                           // 顔パーツ中心計算
            if (part != null)
            {                                                       // パーツが存在する場合
                center += part.position;                            // 位置を加算
                count++;                                            // カウンタ増加
            }
        }
        center /= count;                                            // 中心座標計算

        float radius = 0f;                                          // 顔半径初期化
        foreach (Transform part in faceParts)
        {                                                           // 顔半径計算
            if (part != null)
            {                                                       // パーツが存在する場合
                radius = Mathf.Max(radius, Vector3.Distance(center, part.position)); // 中心から最大距離
            }
        }
        radius += 0.06f;                                            // マージン追加

        Transform head = faceParts[0];                              // 頭Transform取得
        Vector3 forward = head != null ? head.forward : descriptor.transform.forward; // カメラ向き
        Vector3 up = head != null ? head.up : Vector3.up;           // 上方向

        float vfovRad = _camera.fieldOfView * Mathf.Deg2Rad;        // 垂直FOVをラジアンに変換
        float distance = radius / Mathf.Tan(vfovRad / 2f);          // カメラ距離計算

        _camera.transform.position = center + forward * distance;   // カメラ位置設定
        _camera.transform.rotation = Quaternion.LookRotation(center - _camera.transform.position, up); // カメラ回転設定
    }

    /// <summary>
    /// カメラとRenderTextureを破棄
    /// </summary>
    public void Cleanup()
    {
        if (_camera != null)
        {                                                           // カメラが存在する場合
            GameObject.DestroyImmediate(_camera.gameObject);        // カメラオブジェクト破棄
            _camera = null;                                         // 参照クリア
        }

        if (_renderTexture != null)
        {                                                           // RenderTextureが存在する場合
            _renderTexture.Release();                               // メモリ解放
            Object.DestroyImmediate(_renderTexture);                // RenderTexture破棄
            _renderTexture = null;                                  // 参照クリア
        }
    }
}
#endif
