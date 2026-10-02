// File: Assets/EmoviewTool/Editor/EmoviewProgressWindow.cs - EditorWindow implementation for displaying and managing facial capture progress in Unity Editor.
using UnityEditor;
using UnityEngine;

namespace EmoviewTool
{
    /// 
    /// Modal utility editor window displaying real-time progress and status updates for facial capture processes.
    /// 
    public class EmoviewProgressWindow : EditorWindow
    {
        // Public auto-properties for tracking capture step state and item metadata
        public string CurrentItemName { get; set; } = string.Empty;
        public int CurrentStep { get; set; } = 0;
        public int TotalSteps { get; set; } = 0;
        public bool IsCanceled { get; private set; } = false;

        /// 
        /// Instantiates, positions, and displays the progress utility window with fixed dimensions.
        /// 
        /// Title text displayed on the window frame header.
        /// Created or focused instance of EmoviewProgressWindow.
        public static EmoviewProgressWindow ShowProgress(string title)
        {
            // Retrieve existing instance or instantiate a new utility editor window
            EmoviewProgressWindow window = GetWindow<EmoviewProgressWindow>(true, title, true);

            // Define fixed window dimensions to prevent layout warping
            Vector2 windowSize = new Vector2(400, 120);
            window.minSize = windowSize;
            window.maxSize = windowSize;

            // Display as a floating utility window over the Unity Editor interface
            window.ShowUtility();

            return window;
        }

        /// 
        /// Renders the progress bar, current step metrics, item name, and user interaction controls.
        /// 
        private void OnGUI()
        {
            // Add vertical top padding space
            EditorGUILayout.Space(10);

            // Display current operation header label with bold styling
            EditorGUILayout.LabelField("Processing Facial Capture...", EditorStyles.boldLabel);

            // Add vertical spacing before rendering the progress bar
            EditorGUILayout.Space(5);

            // Calculate normalized progress value bounded safely between 0.0 and 1.0
            float progress = TotalSteps > 0 ? Mathf.Clamp01((float)CurrentStep / TotalSteps) : 0f;

            // Reserve layout rectangle with designated height for standard progress bar rendering
            Rect progressRect = EditorGUILayout.GetControlRect(false, 20);

            // Construct progress bar label text containing step numbers and target item name
            string progressText = $"{CurrentStep} / {TotalSteps} - {CurrentItemName}";

            // Render Editor GUI progress bar using calculated metrics
            EditorGUI.ProgressBar(progressRect, progress, progressText);

            // Add vertical spacing before action button row
            EditorGUILayout.Space(10);

            // Begin horizontal layout block for right-aligning action buttons
            EditorGUILayout.BeginHorizontal();

            // Push controls to the right side of the window layout
            GUILayout.FlexibleSpace();

            // Render cancellation button and flag request when user clicks
            if (GUILayout.Button("Cancel", GUILayout.Width(100), GUILayout.Height(25)))
            {
                IsCanceled = true;
            }

            // End horizontal layout block
            EditorGUILayout.EndHorizontal();
        }

        /// 
        /// Programmatically updates step state and forces GUI repaint for visual feedback.
        /// 
        /// Current process iteration index.
        /// Total process iteration count.
        /// Name or path of the item currently being processed.
        public void UpdateProgress(int currentStep, int totalSteps, string itemName)
        {
            // Assign current step index
            CurrentStep = currentStep;

            // Assign total steps count
            TotalSteps = totalSteps;

            // Assign current item display name
            CurrentItemName = itemName ?? string.Empty;

            // Request immediate window redraw to display updated state
            Repaint();
        }
    }
}