// File: Assets/EmoviewTool/Scripts/EmoviewAvatarService.cs - Provides optimized utilities for detecting, hiding, restoring, and instantiating VRChat avatar GameObjects.
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace EmoviewTool
{
    /// 
    /// Static class providing high-performance utility methods for avatar operations in Unity Editor.
    /// 
    public static class EmoviewAvatarService
    {
        // Cached constant string for type-name checking to prevent string allocation per lookup
        private const string VrcAvatarDescriptorTypeName = "VRCAvatarDescriptor";

        /// 
        /// Checks if a given GameObject contains a VRCAvatarDescriptor component without hard VRChat SDK dependencies.
        /// 
        /// The GameObject to inspect.
        /// True if a component whose type name contains 'VRCAvatarDescriptor' is attached; otherwise, false.
        public static bool HasAvatarDescriptor(GameObject obj)
        {
            // Return false immediately if the target GameObject reference is null
            if (obj == null)
            {
                return false;
            }

            // Retrieve attached components into a recycled list to avoid array allocations from GetComponents(Type)
            List<Component> components = new List<Component>();
            obj.GetComponents(typeof(Component), components);

            // Iterate over attached components and verify type name
            int count = components.Count;
            for (int i = 0; i < count; i++)
            {
                Component component = components[i];

                // Check if component exists and its type name matches target descriptor string
                if (component != null && component.GetType().Name.Contains(VrcAvatarDescriptorTypeName))
                {
                    return true;
                }
            }

            return false;
        }

        /// 
        /// Deactivates all active GameObjects in the scene containing a VRCAvatarDescriptor, except the target avatar.
        /// 
        /// The avatar GameObject to keep active.
        /// A list of GameObjects that were deactivated by this operation.
        public static List<GameObject> HideOtherAvatars(GameObject currentTarget)
        {
            // Initialize list to hold deactivated avatar GameObjects
            List<GameObject> hiddenList = new List<GameObject>();

            // Retrieve all active MonoBehaviour instances in the scene
            UnityEngine.Object[] allObjects = Object.FindObjectsByType(typeof(MonoBehaviour), FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            MonoBehaviour[] allComponents = allObjects.Cast<MonoBehaviour>().ToArray();


            // Iterate over retrieved components to locate avatar descriptors
            int length = allComponents.Length;
            for (int i = 0; i < length; i++)
            {
                MonoBehaviour comp = allComponents[i];

                // Validate component reference and check type name against target string
                if (comp != null && comp.GetType().Name.Contains(VrcAvatarDescriptorTypeName))
                {
                    GameObject avatarObj = comp.gameObject;

                    // Ensure the GameObject is not the current target before hiding
                    if (avatarObj != currentTarget && avatarObj.activeSelf)
                    {
                        // Deactivate the avatar GameObject
                        avatarObj.SetActive(false);

                        // Track the deactivated GameObject for later restoration
                        hiddenList.Add(avatarObj);
                    }
                }
            }

            return hiddenList;
        }

        /// 
        /// Restores visibility for all GameObjects previously hidden by .
        /// 
        /// The list of avatar GameObjects to reactivate.
        public static void RestoreOtherAvatars(List<GameObject> hiddenList)
        {
            // Return early if the provided list reference is null
            if (hiddenList == null)
            {
                return;
            }

            // Reactivate each GameObject in the hidden list
            int count = hiddenList.Count;
            for (int i = 0; i < count; i++)
            {
                GameObject avatarObj = hiddenList[i];

                // Ensure object reference remains valid before setting active state
                if (avatarObj != null)
                {
                    avatarObj.SetActive(true);
                }
            }
        }

        /// 
        /// Instantiates a temporary copy of a target avatar GameObject at a specific spawn position.
        /// 
        /// The source avatar GameObject to clone.
        /// The world space position for placement.
        /// The newly created temporary avatar GameObject, or null if source is invalid.
        public static GameObject CreateTempAvatar(GameObject srcAvatar, Vector3 spawnPos)
        {
            // Return null if source avatar object is null
            if (srcAvatar == null)
            {
                return null;
            }

            // Instantiate clone of source avatar at designated position with identity rotation
            GameObject tempAvatar = Object.Instantiate(srcAvatar, spawnPos, Quaternion.identity);

            // Assign descriptive temporary name tag
            tempAvatar.name = $"{srcAvatar.name}_EmoviewTemp";

            // Ensure cloned GameObject is active in the hierarchy
            tempAvatar.SetActive(true);

            return tempAvatar;
        }

        /// 
        /// Immediately destroys a temporary avatar GameObject in Editor or Runtime mode.
        /// 
        /// The temporary avatar GameObject to destroy.
        public static void DestroyTempAvatar(GameObject tempAvatar)
        {
            // Validate target object reference before destroying
            if (tempAvatar != null)
            {
                // Destroy object immediately for Editor execution safety
                Object.DestroyImmediate(tempAvatar);
            }
        }
    }
}