// File: Assets/EmoviewTool/Editor/FacialAnimData.cs - Data structure holding animation clip reference and path metadata for facial capture processing.
using UnityEngine;

namespace EmoviewTool
{
    /// 
    /// Data transfer object holding asset path metadata and reference for a facial AnimationClip.
    /// 
    public class FacialAnimData
    {
        // Relative database path to the animation asset
        public string AssetPath;

        // Clean filename without directory prefix or file extension
        public string FileNameWithoutExtension;

        // Loaded AnimationClip asset instance used for sampling
        public AnimationClip Clip;
    }
}