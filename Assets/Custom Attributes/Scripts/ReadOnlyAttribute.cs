/*
 * ---------------------------------------------------------------------------
 * Description: This script defines a custom attribute, ReadOnlyAttribute, 
 *              and a corresponding PropertyDrawer. When applied to a field, 
 *              the attribute makes it read-only in the Unity Inspector. This 
 *              is useful for displaying information without allowing edits, 
 *              ensuring data integrity.
 * 
 * Using:       [ReadOnly]
 * 
 * Author: Lucas Gomes Cecchini
 * Pseudonym: AGAMENOM
 * ---------------------------------------------------------------------------
*/

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

#region === Attribute Definition ===

/// <summary>
/// Attribute used to make fields read-only in the Unity Inspector.
/// </summary>
public class ReadOnlyAttribute : PropertyAttribute
{
    // This attribute is just a marker, it doesn't need any additional implementation.
}

#endregion

#if UNITY_EDITOR

#region === ReadOnlyDrawer ===

/// <summary>
/// Custom PropertyDrawer that renders fields marked with <see cref="ReadOnlyAttribute"/>
/// as read-only in the Inspector.
/// </summary>
[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer
{
    /// <summary>
    /// Draws the property in the Inspector as read-only.
    /// </summary>
    /// <param name="position">The rect for the property field.</param>
    /// <param name="property">The property being drawn.</param>
    /// <param name="label">The GUI label of the property.</param>
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        GUI.enabled = false; // Disables editing of the field, making it read-only.
        EditorGUI.PropertyField(position, property, label); // Renders the field in the Inspector without the possibility of editing.
        GUI.enabled = true; // Restores the GUI state to allow future normal interactions.
    }
}
#endregion

#endif