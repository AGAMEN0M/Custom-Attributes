using UnityEngine;

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
#endif

public class TagDropdownAttribute : PropertyAttribute
{
    // This attribute is just a marker, it does not need any additional implementation.
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(TagDropdownAttribute))]
public class TagDropdownDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Make sure the property is of type string.
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.LabelField(position, label.text, "Use [TagDropdown] com strings.");
            return;
        }

        // Get the list of Unity tags.
        string[] tags = UnityEditorInternal.InternalEditorUtility.tags;

        // Add a "Tag Missing" option to the dropdown if needed.
        string currentString = property.stringValue;
        string currentText = string.IsNullOrEmpty(currentString) ? "Tag Missing" : $"Tag Missing ({currentString})";
        string missingTagText = System.Array.Exists(tags, tag => tag == currentString) ? "" : currentText;
        List<string> tagList = new() { missingTagText };
        tagList.AddRange(tags);

        // Find the index of the current property value in the list of tags.
        int currentIndex = tagList.IndexOf(currentString);
        if (currentIndex == -1) currentIndex = 0; //If the current value is not in the list, select "Missing Tag".

        // Display the dropdown in the Inspector.
        int newIndex = EditorGUI.Popup(position, label.text, currentIndex, tagList.ToArray());

        //If the selected option is not "Tag Missing", update the string with the new selection.
        if (newIndex != 0) property.stringValue = tagList[newIndex];

        // Show a warning if "Tag Missing" is selected.
        if (newIndex == 0)
        {
            // Define the rectangle for the HelpBox below the dropdown field and adjust its height.
            Rect helpBoxRect = new(position.x, position.y + EditorGUIUtility.singleLineHeight + 2, position.width, EditorGUIUtility.singleLineHeight * 2);
            EditorGUI.HelpBox(helpBoxRect, "String value does not match any tag!", MessageType.Warning);
        }
    }

    // Specifies the additional height required to display the warning.
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // If the string value does not match any tag, add space for the warning.
        string[] tags = UnityEditorInternal.InternalEditorUtility.tags;
        if (tags != null && !System.Array.Exists(tags, tag => tag == property.stringValue))
        {
            return EditorGUIUtility.singleLineHeight * 3; // Adds extra height.
        }

        return EditorGUIUtility.singleLineHeight;
    }
}
#endif