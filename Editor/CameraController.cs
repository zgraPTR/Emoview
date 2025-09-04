#if UNITY_EDITOR
using UnityEngine;
using VRC.SDK3.Avatars.Components; // VRCAvatarDescriptor を簡素化

public class CameraController
{
    private Camera _camera;
    private RenderTexture _renderTexture;
    private readonly Logger _logger;

    // Getter は上部にまとめる
    public Camera RenderCamera => _camera;
    public RenderTexture RenderTexture => _renderTexture;

    public CameraController(Logger logger)
    {                                               // コンストラクタ
        _logger = logger;
    }

    public void SetupCamera(int resolution)
    {
        if (_renderTexture != null) return;         // 作成済みなら再利用

        _renderTexture = new RenderTexture(resolution, resolution, 16, RenderTextureFormat.ARGB32)
        {
            name = "EmoView_RT",
            antiAliasing = 4
        };                                          // RenderTexture 作成

        GameObject camObj = new GameObject("EmoView_PreviewCamera");        // Camera オブジェクト作成
        _camera = camObj.AddComponent<Camera>();                            // カメラコンポーネント追加
        _camera.targetTexture = _renderTexture;                             // RenderTexture指定
        _camera.clearFlags = CameraClearFlags.SolidColor;                   // 背景透過用
        _camera.backgroundColor = new Color(0.15f, 0.15f, 0.15f);           // 背景色
        _camera.nearClipPlane = 0.01f;
        _camera.farClipPlane = 10f;
        _camera.fieldOfView = 30f;
    }

    /// <summary>
    /// 顔全体を映すカメラ配置
    /// </summary>
    public void AutoFrame(VRCAvatarDescriptor descriptor, GameObject avatar)
    {
        // カメラ・アバター・デスクリプタの null チェック
        if (_camera == null || descriptor == null || avatar == null) return;

        Animator animator = avatar.GetComponent<Animator>();
        if (animator == null) return;

        Transform[] faceParts = new Transform[]
        {
            animator.GetBoneTransform(HumanBodyBones.Head),
            animator.GetBoneTransform(HumanBodyBones.LeftEye),
            animator.GetBoneTransform(HumanBodyBones.RightEye)
        };                                                                                  // 顔に関するトランスフォームを取得

        Vector3 center = descriptor.transform.TransformPoint(descriptor.ViewPosition);      // 顔中心の初期値は ViewPosition
        int count = 1;

        foreach (Transform part in faceParts)
        {                                                                                   // 顔パーツが存在する場合、中心計算に加算
            if (part != null)
            {
                center += part.position;
                count++;
            }
        }
        center /= count;

        float radius = 0f;
        foreach (Transform part in faceParts)
        {
            if (part != null)
            {
                radius = Mathf.Max(radius, Vector3.Distance(center, part.position));
            }
        }                                                                                   // 顔半径計算（中心から最も離れたパーツまでの距離 + マージン）
        radius += 0.06f;

        Transform head = faceParts[0];
        Vector3 forward = head != null ? head.forward : descriptor.transform.forward;
        Vector3 up = head != null ? head.up : Vector3.up;                                   // カメラ向きは頭の正面 or デスクリプタ基準

        float vfovRad = _camera.fieldOfView * Mathf.Deg2Rad;
        float distance = radius / Mathf.Tan(vfovRad / 2f);                                  // 顔全体が映る距離計算

        _camera.transform.position = center + forward * distance;
        _camera.transform.rotation = Quaternion.LookRotation(center - _camera.transform.position, up);  // カメラ配置
    }

    /// <summary>
    /// カメラと RenderTexture をクリーンアップ
    /// </summary>
    public void Cleanup()
    {
        if (_camera != null)
        {
            GameObject.DestroyImmediate(_camera.gameObject);
            _camera = null;
        }

        if (_renderTexture != null)
        {
            _renderTexture.Release();
            Object.DestroyImmediate(_renderTexture);
            _renderTexture = null;
        }
    }

}
#endif
