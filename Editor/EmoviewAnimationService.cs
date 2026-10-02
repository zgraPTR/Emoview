// File: Assets/EmoviewTool/Scripts/EmoviewAnimationService.cs - Service for managing and sampling facial animation clips in Unity Editor.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EmoviewTool
{
    /// 
    /// Service providing helper methods for animation clip retrieval and editor sampling.
    /// 
    public static class EmoviewAnimationService
    {
        /// 
        /// Retrieves all AnimationClip assets located under the specified folder path.
        /// 
        /// Relative project path to target folder.
        /// A list of FacialAnimData objects containing metadata and clip references.
        public static List<FacialAnimData> GetAnimationClips(string folderPath)
        {
            // Validate input path to ensure safe asset database query
            if (string.IsNullOrEmpty(folderPath))
            {
                return new List<FacialAnimData>();
            }

            // Normalize folder path by trimming trailing directory separators
            string trimmedPath = folderPath.TrimEnd('/', '\\');

            // Query AssetDatabase for AnimationClip type GUIDs within specified folder
            string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { trimmedPath });

            // Pre-allocate list capacity to avoid re-allocation overhead during iteration
            List<FacialAnimData> animList = new List<FacialAnimData>(guids.Length);

            // Iterate over retrieved asset GUIDs
            foreach (string guid in guids)
            {
                // Convert GUID to project-relative asset path
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                // Load AnimationClip asset from path
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);

                // Add to list if clip was successfully loaded
                if (clip != null)
                {
                    animList.Add(new FacialAnimData
                    {
                        AssetPath = assetPath,
                        FileNameWithoutExtension = Path.GetFileNameWithoutExtension(assetPath),
                        Clip = clip
                    });
                }
            }

            return animList;
        }

        /// 
        /// Samples an animation clip onto a target GameObject within Editor AnimationMode.
        /// 
        /// Target GameObject to sample animation on.
        /// AnimationClip to sample.
        public static void SampleClipInCurrentMode(GameObject avatar, AnimationClip clip)
        {
            // Early exit if required parameters are missing
            if (avatar == null || clip == null)
            {
                return;
            }

            // Begin animation sampling scope
            AnimationMode.BeginSampling();

            // Sample animation clip at time 0
            AnimationMode.SampleAnimationClip(avatar, clip, 0f);

            // End animation sampling scope
            AnimationMode.EndSampling();

            // Force Animator update to apply sampled changes immediately
            if (avatar.TryGetComponent(out Animator animator))
            {
                animator.Update(0f);
            }

            // Synchronize physics transforms to reflect animated changes in hierarchy
            Physics.SyncTransforms();
        }
    }
}