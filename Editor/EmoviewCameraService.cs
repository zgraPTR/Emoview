// File: Assets/EmoviewTool/Scripts/EmoviewCameraService.cs - Provides camera setup, placement, and cleanup algorithms optimized for facial framing.
using System.Collections.Generic;
using UnityEngine;

namespace EmoviewTool
{
    /// 
    /// Static service providing optimized facial camera creation, head detection, and positioning functionality.
    /// 
    public static class EmoviewCameraService
    {
        // Default fallbacks and limits for head height calculation
        private const float DefaultHeadHeight = 0.22f;
        private const float DefaultHeadTopOffset = 0.14f;
        private const float FallbackYOffset = 1.5f;

        /// 
        /// Instantiates and initializes a target camera configured specifically for rendering avatar facial expressions.
        /// 
        /// Target avatar GameObject.
        /// Flag to specify if debug naming conventions should apply.
        /// Configured Camera component instance.
        public static Camera SetupFacialCamera(GameObject avatar, bool isDebugMode)
        {
            // Determine camera GameObject name based on debug configuration
            string cameraName = isDebugMode ? "EmoviewFacialCamera_Debug" : "EmoviewFacialCamera";

            // Create new GameObject dedicated for camera setup
            GameObject cameraObj = new GameObject(cameraName);

            // Add Camera component to the new GameObject
            Camera camera = cameraObj.AddComponent<Camera>();

            // Configure rendering flags for clean background isolation
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;

            // Set frustum clipping planes suitable for close-up facial framing
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;

            // Set narrow field of view to minimize perspective distortion on faces
            camera.fieldOfView = 20f;

            // Align camera transform to face the avatar's head
            UpdateCameraPosition(camera, avatar);

            return camera;
        }

        /// 
        /// Calculates optimal positioning and framing for the facial camera relative to target avatar's head.
        /// 
        /// Camera instance to position.
        /// Target avatar GameObject.
        public static void UpdateCameraPosition(Camera camera, GameObject avatar)
        {
            // Early exit if required dependencies are missing
            if (camera == null || avatar == null)
            {
                return;
            }

            // Retrieve target head bone transform
            Transform headBone = GetHeadBoneTransform(avatar);
            Vector3 basePosition = headBone.position;
            Quaternion baseRotation = headBone.rotation;

            // Compute head dimensions and top position
            float headHeight = CalculateHeadHeightAndTop(headBone, avatar, out float topY);

            // Determine vertical midpoint target for framing focus
            float targetCenterY = Mathf.Lerp(basePosition.y, topY, 0.5f);
            Vector3 targetLookAtPosition = new Vector3(basePosition.x, targetCenterY, basePosition.z);

            // Target occupancy ratio (higher value means tighter framing)
            float targetOccupancy = Mathf.Max(0.1f, 1.05f);

            // Compute framing distance using trigonometric FOV calculation
            float halfFovRad = camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float distance = (headHeight * 0.5f) / (Mathf.Tan(halfFovRad) * targetOccupancy);

            // Clamp camera distance within safe operational boundaries
            distance = Mathf.Clamp(distance, 0.05f, 3.0f);

            // Calculate forward direction vector using head rotation
            Vector3 forwardDir = baseRotation * Vector3.forward;

            // Offset camera transform away from head center along forward vector
            camera.transform.position = targetLookAtPosition + (forwardDir * distance);

            // Align camera orientating toward focus point using head up direction
            camera.transform.LookAt(targetLookAtPosition, baseRotation * Vector3.up);
        }

        /// 
        /// Locates head bone transform from Humanoid Animator or fallback hierarchy search.
        /// 
        /// Target avatar GameObject.
        /// Head bone Transform or root transform if not found.
        private static Transform GetHeadBoneTransform(GameObject avatar)
        {
            // Attempt to retrieve Head bone via Animator or fallback string name
            Transform head = FindBoneTransform(avatar, HumanBodyBones.Head, "Head");
            if (head != null)
            {
                return head;
            }

            // Fallback to avatar root transform if head bone lookup fails
            return avatar.transform;
        }

        /// 
        /// Estimates head height and highest point using child bone transforms and SkinnedMeshRenderer bounds.
        /// 
        /// Transform of the head bone.
        /// Target avatar GameObject.
        /// Calculated Y position representing top of head.
        /// Calculated height of the head region.
        private static float CalculateHeadHeightAndTop(Transform headBone, GameObject avatar, out float topY)
        {
            // Fallback calculation when head bone reference is absent
            if (headBone == null)
            {
                topY = avatar.transform.position.y + FallbackYOffset;
                return DefaultHeadHeight;
            }

            float headY = headBone.position.y;
            topY = headY;

            // Use non-allocating List parameter to retrieve child transforms without GC allocations
            List<Transform> childrenList = new List<Transform>();
            headBone.GetComponentsInChildren(true, childrenList);

            // Scan child hierarchy transforms for maximum elevation
            int childCount = childrenList.Count;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = childrenList[i];
                if (child != null && child.position.y > topY)
                {
                    topY = child.position.y;
                }
            }

            float hierarchyHeight = topY - headY;

            // Validate if hierarchy elevation falls within reasonable head scale
            if (hierarchyHeight >= 0.05f && hierarchyHeight <= 0.40f)
            {
                return hierarchyHeight * 1.5f;
            }

            // Use non-allocating List for SkinnedMeshRenderer lookup
            List<SkinnedMeshRenderer> rendererList = new List<SkinnedMeshRenderer>();
            avatar.GetComponentsInChildren(true, rendererList);

            float meshMaxY = headY;
            bool foundMesh = false;

            // Scan mesh bounding boxes to detect top head geometry
            int rendererCount = rendererList.Count;
            for (int i = 0; i < rendererCount; i++)
            {
                SkinnedMeshRenderer smr = rendererList[i];
                if (smr == null || smr.sharedMesh == null)
                {
                    continue;
                }

                Bounds bounds = smr.bounds;

                // Validate if mesh bounds center is near the head bone vertical plane
                if (Mathf.Abs(bounds.center.y - headY) < 0.4f)
                {
                    if (bounds.max.y > meshMaxY)
                    {
                        meshMaxY = bounds.max.y;
                        foundMesh = true;
                    }
                }
            }

            // Return mesh bounds derived height if valid elevation offset found
            if (foundMesh && (meshMaxY - headY) > 0.05f)
            {
                topY = meshMaxY;
                return (meshMaxY - headY) * 1.4f;
            }

            // Default fallback calculation when elevation measurement is uncertain
            topY = headY + DefaultHeadTopOffset;
            return DefaultHeadHeight;
        }

        /// 
        /// Finds specific bone transform using Animator Humanoid mapping or direct path child lookup.
        /// 
        /// Target avatar GameObject.
        /// Humanoid bone classification.
        /// Fallback object name to search in hierarchy.
        /// Matching Transform if found; otherwise, null.
        private static Transform FindBoneTransform(GameObject avatar, HumanBodyBones boneType, string fallbackName)
        {
            // Attempt to resolve bone reference via Animator Humanoid definition
            if (avatar.TryGetComponent(out Animator animator))
            {
                Transform bone = animator.GetBoneTransform(boneType);
                if (bone != null)
                {
                    return bone;
                }
            }

            // Attempt fallback lookup by child transform path name
            return avatar.transform.Find(fallbackName);
        }

        /// 
        /// Instantly cleans up and destroys the created camera GameObject.
        /// 
        /// Camera reference to destroy.
        public static void DestroyCamera(Camera camera)
        {
            // Validate camera reference and destroy attached GameObject immediately
            if (camera != null)
            {
                Object.DestroyImmediate(camera.gameObject);
            }
        }
    }
}