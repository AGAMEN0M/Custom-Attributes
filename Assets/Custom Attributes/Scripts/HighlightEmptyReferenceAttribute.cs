/*
 * ---------------------------------------------------------------------------
 * Description: This script defines a custom attribute, HighlightEmptyReferenceAttribute, 
 *              and its corresponding PropertyDrawer. It is used to highlight fields 
 *              with null references in the Unity Inspector. The script visually marks 
 *              such fields with a red background and provides a warning message, 
 *              improving error detection and debugging.
 * 
 * Using:       [HighlightEmptyReference]
 * 
 * Author: Lucas Gomes Cecchini
 * Pseudonym: AGAMENOM
 * ---------------------------------------------------------------------------
*/

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

// Attribute to mark fields that should be highlighted if their reference is null.
public class HighlightEmptyReferenceAttribute : PropertyAttribute
{
    // This attribute is just a marker, it does not need additional implementation.
}

#if UNITY_EDITOR
// Custom property drawer for the HighlightEmptyReferenceAttribute.
[CustomPropertyDrawer(typeof(HighlightEmptyReferenceAttribute))]
public class HighlightEmptyReferenceDrawer : PropertyDrawer
{
    // Draws the property in the Inspector.
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        Color previousColor = GUI.backgroundColor; // Save the previous background color.

        // Check if the property is an object reference and if it's null.
        bool isEmpty = property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null;

        GUI.backgroundColor = isEmpty ? Color.red : previousColor; // Set background color to red if the property is empty.

        // Define the rect for the property field.
        Rect propertyRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

        EditorGUI.PropertyField(propertyRect, property, label); // Draw the property field.
        GUI.backgroundColor = previousColor; // Restore the previous background color.

        // If the property is empty, show an error message.
        if (isEmpty)
        {
            string typeName = "Unknown";

            // If there is a reference, get its type name.
            if (property.objectReferenceValue != null)
            {
                typeName = property.objectReferenceValue.GetType().Name;
            }
            // If there is no reference, get the field type from the target object.
            else if (property.serializedObject.targetObject != null)
            {
                var targetObject = property.serializedObject.targetObject;
                var fieldInfo = targetObject.GetType().GetField(property.name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                if (fieldInfo != null) typeName = fieldInfo.FieldType.Name;
            }

            // Define the rect for the help box.
            Rect helpBoxRect = new(position.x, position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing, position.width, EditorGUIUtility.singleLineHeight * 2);

            // Draw the help box with an error message.
            EditorGUI.HelpBox(helpBoxRect, $"Put an item of type '{typeName}' here!", MessageType.Error);
        }
    }

    // Returns the height of the property in the Inspector.
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // If the property is empty, return height to accommodate the help box.
        bool isEmpty = property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null;
        return isEmpty ? EditorGUIUtility.singleLineHeight * 3 + 4 : EditorGUIUtility.singleLineHeight;
    }
}
#endif