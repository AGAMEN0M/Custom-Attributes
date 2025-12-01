/*
 * ---------------------------------------------------------------------------
 * Description: This script defines a custom attribute and property drawer for 
 *              Unity. It allows string fields in the Inspector to display a 
 *              dropdown menu containing the names of scenes from the 
 *              Editor Build Settings. It supports warnings for missing or 
 *              disabled scenes and dynamically adjusts UI height.
 * 
 * Using:       [SceneTagDropdown]
 * 
 * Author: Lucas Gomes Cecchini
 * Pseudonym: AGAMENOM
 * ---------------------------------------------------------------------------
*/

using UnityEngine;

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using System.Linq;
using System.IO;
using System;
#endif

#region === Attribute Definition ===

/// <summary>
/// Attribute used to display a dropdown menu of scenes from the Editor Build Settings
/// for string fields in the Unity Inspector.
/// </summary>
public class SceneTagDropdownAttribute : PropertyAttribute
{
    // This attribute is just a marker, it does not need additional implementation.
}

#endregion

#if UNITY_EDITOR

#region === SceneTagDropdownDrawer ===

/// <summary>
/// Custom PropertyDrawer that displays a dropdown for string fields marked with
/// <see cref="SceneTagDropdownAttribute"/>. It handles missing scenes, disabled
/// scenes, and dynamically adjusts the field height in the Inspector.
/// </summary>
[CustomPropertyDrawer(typeof(SceneTagDropdownAttribute))]
public class SceneTagDropdownDrawer : PropertyDrawer
{
    #region === OnGUI ===

    /// <summary>
    /// Draws the property field as a dropdown menu of scenes.
    /// Displays warnings or errors if the scene is missing or disabled.
    /// Also supports multi-object editing by showing '-' if values differ.
    /// </summary>
    /// <param name="position">The rect for the property field.</param>
    /// <param name="property">The serialized property being drawn.</param>
    /// <param name="label">The GUI label of the property.</param>
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Ensure the property is a string field.
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.LabelField(position, label.text, "Use [SceneTagDropdown] with strings.");
            return;
        }

        // Begin property for prefab overrides and multi-object support.
        EditorGUI.BeginProperty(position, label, property);

        // Get the scenes from Build Settings.
        var allScenes = EditorBuildSettings.scenes.Select(scene => Path.GetFileNameWithoutExtension(scene.path)).ToArray();
        var enabledScenes = allScenes.Where(scene => EditorBuildSettings.scenes.First(s => Path.GetFileNameWithoutExtension(s.path) == scene).enabled).ToArray();

        // Check for multiple different values (multi-object selection).
        bool hasMultipleDifferentValues = property.hasMultipleDifferentValues;

        // Current string value (shared or first selection).
        string currentString = property.stringValue;

        // Handle the "Missing Scene" logic from original version.
        string currentText = string.IsNullOrEmpty(currentString) ? "Missing Scene" : $"Missing Scene ({currentString})";
        string missingScene = Array.Exists(allScenes, scene => scene == currentString) ? $"{currentString} [Disabled]" : currentText;
        string missingText = Array.Exists(enabledScenes, scene => scene == currentString) ? "" : missingScene;
        List<string> sceneList = new() { missingText };
        sceneList.AddRange(enabledScenes);

        // Determine the current index in the list.
        int currentIndex = sceneList.IndexOf(currentString);
        if (currentIndex == -1) currentIndex = 0;

        // Tooltip label remains, but we don't need to show it explicitly.
        EditorGUI.LabelField(position, new GUIContent("", label.tooltip));

        // Activate the mixed value visual state if multiple selections differ.
        EditorGUI.showMixedValue = hasMultipleDifferentValues;

        // Convert the list into GUIContent[] for the popup.
        var options = sceneList.Select(s => new GUIContent(s)).ToArray();

        // Draw the dropdown popup.
        int newIndex = EditorGUI.Popup(position, label, currentIndex, options);

        // Reset the mixed value visual.
        EditorGUI.showMixedValue = false;

        // If the user selects a valid entry and it's different from the current.
        if (newIndex != 0 && (!hasMultipleDifferentValues || newIndex != currentIndex)) property.stringValue = sceneList[newIndex];

        // Show warnings/errors only when values are consistent.
        if (!hasMultipleDifferentValues)
        {
            // Scene existence and enable status checks.
            bool existsInBuild = allScenes.Contains(currentString);
            bool isEnabled = existsInBuild && EditorBuildSettings.scenes.First(s => Path.GetFileNameWithoutExtension(s.path) == currentString).enabled;

            if (newIndex == 0 && !string.IsNullOrEmpty(currentString) && existsInBuild)
            {
                // Scene exists but is disabled.
                Rect helpBoxRect = new(position.x, position.y + EditorGUIUtility.singleLineHeight + 2, position.width, EditorGUIUtility.singleLineHeight * 2);
                EditorGUI.HelpBox(helpBoxRect, "The scene is in the build but is disabled.", MessageType.Warning);
            }
            else if (newIndex == 0)
            {
                // Scene name not found at all.
                Rect helpBoxRect = new(position.x, position.y + EditorGUIUtility.singleLineHeight + 2, position.width, EditorGUIUtility.singleLineHeight * 2);
                EditorGUI.HelpBox(helpBoxRect, "String value does not match any scenario!", MessageType.Error);
            }
        }

        // End property for proper prefab override handling.
        EditorGUI.EndProperty();
    }

    #endregion

    #region === GetPropertyHeight ===

    /// <summary>
    /// Returns the height of the property field in the Inspector.
    /// Adds extra height for warnings or errors.
    /// </summary>
    /// <param name="property">The property being drawn.</param>
    /// <param name="label">The GUI label of the property.</param>
    /// <returns>Height of the property field.</returns>
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // Get all the scenes again.
        var allScenes = EditorBuildSettings.scenes.Select(scene => Path.GetFileNameWithoutExtension(scene.path)).ToArray();

        // Checks if property value is empty or does not match any scene.
        if (string.IsNullOrEmpty(property.stringValue) || !Array.Exists(allScenes, scene => scene == property.stringValue))
        {
            return EditorGUIUtility.singleLineHeight * 3; // Adds extra height to the warning.
        }

        // Checks if the scene is disabled.
        if (allScenes.Contains(property.stringValue) && !EditorBuildSettings.scenes.First(s => Path.GetFileNameWithoutExtension(s.path) == property.stringValue).enabled)
        {
            return EditorGUIUtility.singleLineHeight * 3; // Adds extra height if the scene is disabled.
        }

        return EditorGUIUtility.singleLineHeight; // Default height if no warnings.
    }

    #endregion
}

#endregion

#endif