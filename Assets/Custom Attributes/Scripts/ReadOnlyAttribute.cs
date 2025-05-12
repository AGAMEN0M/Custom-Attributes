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

// Defines a custom attribute called ReadOnlyAttribute, which will be used to make fields read-only in the Inspector.
public class ReadOnlyAttribute : PropertyAttribute
{
    // This attribute is just a marker, it doesn't need any additional implementation.
}

#if UNITY_EDITOR
// Defines a PropertyDrawer that controls how the ReadOnlyAttribute will be rendered in the Inspector.
[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer
{
    // Override the OnGUI method to control field rendering with the ReadOnlyAttribute.
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        GUI.enabled = false; // Disables editing of the field, making it read-only.
        EditorGUI.PropertyField(position, property, label); // Renders the field in the Inspector without the possibility of editing.
        GUI.enabled = true; // Restores the GUI state to allow future normal interactions.
    }
}
#endif