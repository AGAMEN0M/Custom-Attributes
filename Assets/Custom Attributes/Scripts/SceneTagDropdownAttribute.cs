/*
 * ---------------------------------------------------------------------------
 * Description: This script defines a custom attribute and property drawer for 
 *              Unity. It allows string fields in the Inspector to display a 
 *              dropdown menu containing the names of scenes from the 
 *              Editor Build Settings. It supports warnings for missing or 
 *              disabled scenes and dynamically adjusts UI height.
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

public class SceneTagDropdownAttribute : PropertyAttribute
{
    // This attribute is just a marker, it does not need additional implementation.
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(SceneTagDropdownAttribute))]
public class SceneTagDropdownDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Make sure the property is of type string.
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.LabelField(position, label.text, "Use [SceneTagDropdown] with strings.");
            return;
        }

        // Get the scene list from "Scenes In Build".
        var allScenes = EditorBuildSettings.scenes.Select(scene => Path.GetFileNameWithoutExtension(scene.path)).ToArray();
        var enabledScenes = allScenes.Where(scene => EditorBuildSettings.scenes.First(s => Path.GetFileNameWithoutExtension(s.path) == scene).enabled).ToArray();

        // Add a "Missing Scene" option to the dropdown if needed.
        string currentString = property.stringValue;
        string currentText = string.IsNullOrEmpty(currentString) ? "Missing Scene" : $"Missing Scene ({currentString})";
        string missingScene = Array.Exists(allScenes, scene => scene == currentString) ? $"{currentString} [Disabled]" : currentText;
        string missingText = Array.Exists(enabledScenes, scene => scene == currentString) ? "" : missingScene;
        List<string> sceneList = new() { missingText };
        sceneList.AddRange(enabledScenes);

        // Find the index of the current property value in the scene list.
        int currentIndex = sceneList.IndexOf(currentString);
        if (currentIndex == -1) currentIndex = 0; // If the current value is not in the list, select "Missing Scene".

        // Display the dropdown in the Inspector.
        int newIndex = EditorGUI.Popup(position, label.text, currentIndex, sceneList.ToArray());

        // If the selected option is not "Missing Scene", update the string with the new selection.
        if (newIndex != 0) property.stringValue = sceneList[newIndex];

        // Checks if the scene is in EditorBuildSettings but disabled.
        if (newIndex == 0 && !string.IsNullOrEmpty(currentString) && allScenes.Contains(currentString))
        {
            Rect helpBoxRect = new(position.x, position.y + EditorGUIUtility.singleLineHeight + 2, position.width, EditorGUIUtility.singleLineHeight * 2);
            EditorGUI.HelpBox(helpBoxRect, "The scene is in the build but is disabled.", MessageType.Warning);
        }
        else if (newIndex == 0)
        {
            // Defines the rectangle for the HelpBox below the dropdown field and adjusts its height.
            Rect helpBoxRect = new(position.x, position.y + EditorGUIUtility.singleLineHeight + 2, position.width, EditorGUIUtility.singleLineHeight * 2);
            EditorGUI.HelpBox(helpBoxRect, "String value does not match any scenario!", MessageType.Error);
        }
    }

    // Specifies the additional height required to display the warning.
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
}
#endif