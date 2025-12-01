/*
 * ---------------------------------------------------------------------------
 * Description: Defines a custom attribute and property drawer to display a 
 *              dropdown menu for Unity tags in the Inspector.
 * 
 * Using:       [TagDropdown]
 * 
 * Author: Lucas Gomes Cecchini
 * Pseudonym: AGAMENOM
 * ---------------------------------------------------------------------------
*/

using UnityEngine;
using System.Linq;
using System;

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
#endif

#region === Attribute Definition ===

/// <summary>
/// Attribute used to display a dropdown menu of Unity tags for string fields
/// in the Inspector.
/// </summary>
public class TagDropdownAttribute : PropertyAttribute
{
    // This attribute is just a marker, it does not need any additional implementation.
}

#endregion

#if UNITY_EDITOR

#region === TagDropdownDrawer ===

/// <summary>
/// Custom PropertyDrawer that displays a dropdown for string fields marked with
/// <see cref="TagDropdownAttribute"/>. It handles missing tags and dynamically
/// adjusts the field height in the Inspector.
/// </summary>
[CustomPropertyDrawer(typeof(TagDropdownAttribute))]
public class TagDropdownDrawer : PropertyDrawer
{
    #region === OnGUI ===

    /// <summary>
    /// Draws the property field as a dropdown menu of Unity tags.
    /// Displays a warning if the current string value does not match any tag.
    /// Fully supports multi-object editing.
    /// </summary>
    /// <param name="position">The rect for the property field.</param>
    /// <param name="property">The serialized property being drawn.</param>
    /// <param name="label">The GUI label of the property.</param>
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Ensure the property is a string field.
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.LabelField(position, label.text, "Use [TagDropdown] with strings.");
            return;
        }

        // Begin property for prefab overrides and multi-object support.
        EditorGUI.BeginProperty(position, label, property);

        // Available Unity tags.
        var tags = UnityEditorInternal.InternalEditorUtility.tags;

        bool hasMultipleDifferentValues = property.hasMultipleDifferentValues;
        string currentString = property.stringValue;

        // Build "Tag Missing" similar to Scene version.
        string currentText = string.IsNullOrEmpty(currentString) ? "Tag Missing" : $"Tag Missing ({currentString})";
        string missingTagText = Array.Exists(tags, t => t == currentString) ? "" : currentText;

        List<string> tagList = new() { missingTagText };
        tagList.AddRange(tags);

        // Determine current index.
        int currentIndex = tagList.IndexOf(currentString);
        if (currentIndex == -1) currentIndex = 0;

        // Tooltip only.
        EditorGUI.LabelField(position, new GUIContent("", label.tooltip));

        // Mixed value visual state.
        EditorGUI.showMixedValue = hasMultipleDifferentValues;

        // Convert to GUIContent.
        var options = tagList.Select(t => new GUIContent(t)).ToArray();

        // Draw dropdown.
        int newIndex = EditorGUI.Popup(position, label, currentIndex, options);

        // Reset mixed value display.
        EditorGUI.showMixedValue = false;

        // If valid and changed → update.
        if (newIndex != 0 && (!hasMultipleDifferentValues || newIndex != currentIndex)) property.stringValue = tagList[newIndex];

        // Show warning only if the result is "Tag Missing".
        if (newIndex == 0 && !hasMultipleDifferentValues)
        {
            Rect helpBoxRect = new(position.x, position.y + EditorGUIUtility.singleLineHeight + 2, position.width, EditorGUIUtility.singleLineHeight * 2);
            EditorGUI.HelpBox(helpBoxRect, "String value does not match any tag!", MessageType.Warning);
        }

        // End property.
        EditorGUI.EndProperty();
    }

    #endregion

    #region === GetPropertyHeight ===

    /// <summary>
    /// Returns the height of the property field in the Inspector.
    /// Adds extra height for warnings if the tag is missing.
    /// </summary>
    /// <param name="property">The property being drawn.</param>
    /// <param name="label">The GUI label of the property.</param>
    /// <returns>Height of the property field.</returns>
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // If the string value does not match any tag, add space for the warning.
        var tags = UnityEditorInternal.InternalEditorUtility.tags;
        if (tags != null && !Array.Exists(tags, tag => tag == property.stringValue))
        {
            return EditorGUIUtility.singleLineHeight * 3; // Adds extra height.
        }

        return EditorGUIUtility.singleLineHeight;
    }

    #endregion
}

#endregion

#endif