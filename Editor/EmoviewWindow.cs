// File: Assets/EmoviewTool/Editor/EmoviewWindow.cs - Main EditorWindow controlling facial expression capture workflow, settings GUI, and batch processing loop.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EmoviewTool
{
    /// 
    /// Configuration data container holding target avatar reference, folder paths, and execution flags for facial capture.
    /// 
    [Serializable]
    public class CaptureSettings
    {
        // Target avatar GameObject containing VRCAvatarDescriptor component
        public GameObject TargetAvatar;

        // Relative path to the folder containing AnimationClip assets
        public string AnimFolderPath = "Assets";

        // Relative output path where generated facial capture PNG files will be stored
        public string ExportFolderPath = "Assets/Facial";

        // Toggles preservation of temporary camera and avatar instances after capture completion
        public bool IsDebugMode = false;
    }

    /// 
    /// Editor window interface for configuring settings and launching the batch facial capture routine.
    /// 
    public class EmoviewWindow : EditorWindow
    {
        // Internal capture configuration state instance
        private CaptureSettings settings = new CaptureSettings();

        /// 
        /// Displays or focuses the Emoview main editor window.
        /// 
        [MenuItem("Tools/RRT/Emoview")]
        public static void ShowWindow()
        {
            // Open window with custom window title
            GetWindow<EmoviewWindow>(false, "Emoview");
        }

        /// 
        /// Renders settings input fields, path selectors, and execution controls.
        /// 
        private void OnGUI()
        {
            // Render tool title header
            EditorGUILayout.LabelField("Emoview - Facial Capture Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Render object field selector for target avatar
            GameObject selectedObj = (GameObject)EditorGUILayout.ObjectField("Target Avatar", settings.TargetAvatar, typeof(GameObject), true);
            if (selectedObj != null)
            {
                // Verify if selected GameObject has required VRCAvatarDescriptor component
                if (EmoviewAvatarService.HasAvatarDescriptor(selectedObj))
                {
                    settings.TargetAvatar = selectedObj;
                }
                else
                {
                    settings.TargetAvatar = null;
                    EditorGUILayout.HelpBox("Selected GameObject does not have a VRC Avatar Descriptor component.", MessageType.Warning);
                }
            }
            else
            {
                settings.TargetAvatar = null;
            }

            // Render animation source folder selection row
            EditorGUILayout.BeginHorizontal();
            settings.AnimFolderPath = EditorGUILayout.TextField("Anim Folder Path", settings.AnimFolderPath);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                // Open OS folder dialog for animation folder selection
                string path = EditorUtility.OpenFolderPanel("Select Animation Folder", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    settings.AnimFolderPath = GetRelativePath(path);
                }
            }
            EditorGUILayout.EndHorizontal();

            // Render export destination folder selection row
            EditorGUILayout.BeginHorizontal();
            settings.ExportFolderPath = EditorGUILayout.TextField("Export Folder Path", settings.ExportFolderPath);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                // Open OS folder dialog for export destination folder selection
                string path = EditorUtility.OpenFolderPanel("Select Export Folder", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    settings.ExportFolderPath = GetRelativePath(path);
                }
            }
            EditorGUILayout.EndHorizontal();

            // Render debug mode options toggle
            settings.IsDebugMode = EditorGUILayout.Toggle("Debug Mode", settings.IsDebugMode);

            EditorGUILayout.Space();

            // Validate requirements before enabling capture execution button
            bool isValidSettings = settings.TargetAvatar != null &&
                                   !string.IsNullOrEmpty(settings.AnimFolderPath) &&
                                   !string.IsNullOrEmpty(settings.ExportFolderPath);

            GUI.enabled = isValidSettings;
            if (GUILayout.Button("Run Capture Process", GUILayout.Height(30)))
            {
                RunCaptureProcess(settings);
            }
            GUI.enabled = true;
        }

        /// 
        /// Executes batch facial capture workflow asynchronously over successive Editor application frames.
        /// 
        /// Configured settings for target avatar and folder locations.
        private void RunCaptureProcess(CaptureSettings settings)
        {
            // Query animation clip assets within designated source folder
            List<FacialAnimData> animList = EmoviewAnimationService.GetAnimationClips(settings.AnimFolderPath);
            if (animList.Count == 0)
            {
                EditorUtility.DisplayDialog("Emoview", "No animation clips found in the specified folder.", "OK");
                return;
            }

            // Calculate final target directory path formatted for current target avatar name
            string finalExportFolderPath = Path.Combine(settings.ExportFolderPath, settings.TargetAvatar.name).Replace("\\", "/");
            if (!Directory.Exists(finalExportFolderPath))
            {
                Directory.CreateDirectory(finalExportFolderPath);
                AssetDatabase.Refresh();
            }

            // Open progress tracking window
            EmoviewProgressWindow progressWindow = EmoviewProgressWindow.ShowProgress("Emoview Capture Progress");

            // Allocate operational variable references
            GameObject tempAvatar = null;
            Camera facialCamera = null;

            // Hide other avatars in scene to isolate target rendering
            List<GameObject> hiddenAvatars = EmoviewAvatarService.HideOtherAvatars(settings.TargetAvatar);

            // Deactivate target scene avatar and add to hidden list for later restoration
            if (settings.TargetAvatar != null && settings.TargetAvatar.activeSelf)
            {
                settings.TargetAvatar.SetActive(false);
                hiddenAvatars.Add(settings.TargetAvatar);
            }

            int currentIndex = 0;

            // Step process method triggered via EditorApplication.delayCall
            void ProcessNextStep()
            {
                try
                {
                    // Check cancellation or completion state
                    if (progressWindow == null || progressWindow.IsCanceled || currentIndex >= animList.Count)
                    {
                        FinishProcess();
                        return;
                    }

                    // Retrieve current clip data and update UI progress
                    FacialAnimData animData = animList[currentIndex];
                    progressWindow.UpdateProgress(currentIndex + 1, animList.Count, animData.FileNameWithoutExtension);

                    // Instantiate temporary avatar object for sampling
                    tempAvatar = EmoviewAvatarService.CreateTempAvatar(settings.TargetAvatar, Vector3.zero);

                    // Lazily initialize rendering camera instance
                    if (facialCamera == null)
                    {
                        facialCamera = EmoviewCameraService.SetupFacialCamera(tempAvatar, settings.IsDebugMode);
                    }

                    // Sample facial expression clip onto temporary avatar
                    AnimationMode.StartAnimationMode();
                    EmoviewAnimationService.SampleClipInCurrentMode(tempAvatar, animData.Clip);
                    EmoviewCameraService.UpdateCameraPosition(facialCamera, tempAvatar);

                    // Render camera view to output PNG file
                    string outputPath = Path.Combine(finalExportFolderPath, $"{animData.FileNameWithoutExtension}.png");
                    EmoviewCaptureService.CaptureToPng(facialCamera, outputPath, 512, 512);

                    // Stop sampling mode and destroy temporary avatar instance
                    AnimationMode.StopAnimationMode();
                    EmoviewAvatarService.DestroyTempAvatar(tempAvatar);
                    tempAvatar = null;

                    // Increment index and schedule next execution frame
                    currentIndex++;
                    EditorApplication.delayCall += ProcessNextStep;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Emoview] Error during capture process: {ex.Message}\n{ex.StackTrace}");
                    EditorUtility.DisplayDialog("Emoview", $"An error occurred during capture:\n{ex.Message}", "OK");
                    FinishProcess();
                }
            }

            // Cleanup method triggered on workflow termination or completion
            void FinishProcess()
            {
                // Unbind frame callback listener
                EditorApplication.delayCall -= ProcessNextStep;

                // Restore editor mode and scene avatar visibility states
                AnimationMode.StopAnimationMode();
                EmoviewAvatarService.RestoreOtherAvatars(hiddenAvatars);

                // Destroy temporary resources if debug mode is disabled
                if (!settings.IsDebugMode)
                {
                    if (facialCamera != null)
                    {
                        EmoviewCameraService.DestroyCamera(facialCamera);
                        facialCamera = null;
                    }
                    if (tempAvatar != null)
                    {
                        EmoviewAvatarService.DestroyTempAvatar(tempAvatar);
                        tempAvatar = null;
                    }
                }

                // Close progress window instance
                if (progressWindow != null)
                {
                    progressWindow.Close();
                }

                // Refresh AssetDatabase to register created PNG textures
                AssetDatabase.Refresh();

                // Display final notification on successful completion
                if (currentIndex >= animList.Count)
                {
                    EditorUtility.DisplayDialog("Emoview", $"Batch capture completed successfully!\nSaved to: {finalExportFolderPath}", "OK");
                }
            }

            // Schedule initial execution step on next editor frame delay
            EditorApplication.delayCall += ProcessNextStep;
        }

        /// 
        /// Converts an absolute disk folder path to a Unity project relative asset path.
        /// 
        /// Absolute directory path on host system.
        /// Relative path starting with 'Assets' or original path if outside project.
        private string GetRelativePath(string absolutePath)
        {
            // Normalize path separators to forward slashes
            string normalizedPath = absolutePath.Replace("\\", "/");
            string normalizedDataPath = Application.dataPath.Replace("\\", "/");

            // Convert absolute path matching project dataPath to relative Assets root path
            if (normalizedPath.StartsWith(normalizedDataPath, StringComparison.OrdinalIgnoreCase))
            {
                return "Assets" + normalizedPath.Substring(normalizedDataPath.Length);
            }

            return absolutePath;
        }
    }
}