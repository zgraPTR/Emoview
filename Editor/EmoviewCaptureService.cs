// File: Assets/EmoviewTool/Scripts/EmoviewCaptureService.cs - Provides optimized screen capturing functionality from a designated camera to PNG files.
using System;
using System.IO;
using UnityEngine;

namespace EmoviewTool
{
    /// 
    /// Static service providing optimized image capture routines for cameras within the Unity Editor.
    /// 
    public static class EmoviewCaptureService
    {
        /// 
        /// Captures the current view of the given camera and saves it to a PNG file on disk.
        /// Performs full resource cleanup for RenderTextures and Texture2D instances to prevent memory leaks.
        /// 
        /// Target camera to render from.
        /// Absolute or relative file destination path for the PNG image.
        /// Pixel width of the output image.
        /// Pixel height of the output image.
        public static void CaptureToPng(Camera camera, string outputPath, int width, int height)
        {
            // Validate that target camera reference is not null
            if (camera == null)
            {
                Debug.LogError("[Emoview] Camera is null, cannot capture image.");
                return;
            }

            // Validate that requested dimensions are positive integers
            if (width <= 0 || height <= 0)
            {
                Debug.LogError($"[Emoview] Invalid capture dimensions specified: {width}x{height}. Dimensions must be positive.");
                return;
            }

            // Ensure destination directory exists prior to file writing
            string directoryPath = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            // Store previous active RenderTexture to restore state after capture completes
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;

            // Allocate temporary RenderTexture for target resolution with a 24-bit depth buffer
            RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);

            // Allocate CPU-side Texture2D buffer for reading pixels
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);

            try
            {
                // Assign temporary render target to camera
                camera.targetTexture = renderTexture;

                // Force immediate camera render pass
                camera.Render();

                // Set temporary render target active for pixel reading
                RenderTexture.active = renderTexture;

                // Read rendered pixels into CPU texture buffer
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);

                // Apply changes to Texture2D
                texture.Apply();

                // Encode texture pixel bytes to PNG format
                byte[] bytes = texture.EncodeToPNG();

                // Write PNG byte array directly to disk
                File.WriteAllBytes(outputPath, bytes);
            }
            catch (Exception ex)
            {
                // Log detailed error exception during capture process
                Debug.LogError($"[Emoview] Failed to capture image to '{outputPath}': {ex.Message}");
            }
            finally
            {
                // Restore camera target texture state
                camera.targetTexture = previousTarget;

                // Restore active render target state
                RenderTexture.active = previousActive;

                // Release allocated temporary RenderTexture back to global pool
                RenderTexture.ReleaseTemporary(renderTexture);

                // Destroy CPU texture object immediately to prevent leak
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}