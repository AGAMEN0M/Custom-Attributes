/*
 * ---------------------------------------------------------------------------
 * Description: This script defines a custom attribute, ConditionalHideAttribute, 
 *              which allows properties in the Unity Inspector to be conditionally 
 *              hidden based on the values of other properties. It also includes a 
 *              custom PropertyDrawer to handle the attribute's logic and rendering.
 * 
 * Using:       [ConditionalHide("myReference")]
 *              [ConditionalHide("myClass.myReference")]
 *              [ConditionalHide("myReference1", "myReference2")]
 *              [ConditionalHide(false, "myReference1", "myReference2")]
 * 
 * Author: Lucas Gomes Cecchini
 * Pseudonym: AGAMENOM
 * ---------------------------------------------------------------------------
*/

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

// Custom attribute to hide properties based on the value of other properties.
public class ConditionalHideAttribute : PropertyAttribute
{
    public string[] ConditionalSourceFields { get; private set; } // Fields that determine the condition for hiding the property.
    public bool HideIfAnyFalse { get; private set; } // If true, the property will be hidden if any of the conditions are false.

    // Constructor accepting an array of conditional source fields.
    public ConditionalHideAttribute(params string[] conditionalSourceFields)
    {
        ConditionalSourceFields = conditionalSourceFields;
        HideIfAnyFalse = conditionalSourceFields.Length > 1; // Hide if more than one condition and any are false.
    }

    // Constructor allowing the option to hide if any of the conditions are false.
    public ConditionalHideAttribute(bool hideIfAnyFalse, params string[] conditionalSourceFields)
    {
        ConditionalSourceFields = conditionalSourceFields;
        HideIfAnyFalse = conditionalSourceFields.Length > 1 && hideIfAnyFalse; // Hide based on multiple conditions and the provided boolean.
    }
}

#if UNITY_EDITOR
// Custom PropertyDrawer to control how properties with ConditionalHideAttribute are drawn in the Inspector.
[CustomPropertyDrawer(typeof(ConditionalHideAttribute))]
public class ConditionalHidePropertyDrawer : PropertyDrawer
{
    // Override the OnGUI method to determine whether the property should be drawn.
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Get the ConditionalHide attribute applied to the property.
        ConditionalHideAttribute hideAttribute = (ConditionalHideAttribute)attribute;

        // Determine if the property should be hidden based on its conditions.
        bool shouldHide = hideAttribute.ConditionalSourceFields.Length > 1
            ? (hideAttribute.HideIfAnyFalse ? CheckAllConditions(property, hideAttribute.ConditionalSourceFields) : CheckAnyCondition(property, hideAttribute.ConditionalSourceFields))
            : CheckSingleCondition(property, hideAttribute.ConditionalSourceFields[0]);

        if (shouldHide) return; // If the condition is met, don't draw the property.

        EditorGUI.PropertyField(position, property, label, true); // Otherwise, draw the property as usual.
    }

    // Override the GetPropertyHeight method to adjust the height based on the hide condition.
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // Get the ConditionalHide attribute applied to the property.
        ConditionalHideAttribute hideAttribute = (ConditionalHideAttribute)attribute;

        // Determine if the property should be hidden based on its conditions.
        bool shouldHide = hideAttribute.ConditionalSourceFields.Length > 1
            ? (hideAttribute.HideIfAnyFalse ? CheckAllConditions(property, hideAttribute.ConditionalSourceFields) : CheckAnyCondition(property, hideAttribute.ConditionalSourceFields))
            : CheckSingleCondition(property, hideAttribute.ConditionalSourceFields[0]);

        // Return height 0 if hidden, otherwise use the default property height.
        if (shouldHide) return 0;

        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    // Check a single condition to determine if the property should be hidden.
    private bool CheckSingleCondition(SerializedProperty property, string conditionalSourceField)
    {
        SerializedProperty conditionProperty = GetConditionProperty(property, conditionalSourceField);

        // If the condition property is null or not a boolean, treat it as false, otherwise check its value.
        return conditionProperty == null || conditionProperty.propertyType != SerializedPropertyType.Boolean || !conditionProperty.boolValue;
    }

    // Check all conditions and return true if any condition fails (for multiple conditions).
    private bool CheckAllConditions(SerializedProperty property, string[] conditionalSourceFields)
    {
        foreach (string condition in conditionalSourceFields)
        {
            SerializedProperty conditionProperty = GetConditionProperty(property, condition);

            // Return true (hide the property) if any condition is false or not a boolean.
            if (conditionProperty == null || conditionProperty.propertyType != SerializedPropertyType.Boolean || !conditionProperty.boolValue)
            {
                return true;
            }
        }
        return false; // All conditions are true, don't hide the property.
    }

    // Check if any condition is true; return true if all are false (for multiple conditions).
    private bool CheckAnyCondition(SerializedProperty property, string[] conditionalSourceFields)
    {
        foreach (string condition in conditionalSourceFields)
        {
            SerializedProperty conditionProperty = GetConditionProperty(property, condition);

            // If any condition is true, don't hide the property.
            if (conditionProperty != null && conditionProperty.propertyType == SerializedPropertyType.Boolean && conditionProperty.boolValue)
            {
                return false;
            }
        }
        return true; // All conditions are false, hide the property.
    }

    // Retrieve the condition property based on the given field name.
    private SerializedProperty GetConditionProperty(SerializedProperty property, string propertyName)
    {
        // Attempt to find the property directly by name.
        SerializedProperty conditionProperty = property.serializedObject.FindProperty(propertyName);

        // If the property isn't found, handle nested properties by splitting the path.
        if (conditionProperty == null)
        {
            string[] pathParts = propertyName.Split('.');
            SerializedProperty currentProperty = property.serializedObject.FindProperty(pathParts[0]);

            // Traverse through the path to find the nested property.
            for (int i = 1; i < pathParts.Length; i++)
            {
                if (currentProperty != null) currentProperty = currentProperty.FindPropertyRelative(pathParts[i]);
            }

            conditionProperty = currentProperty;
        }

        return conditionProperty; // Return the found condition property or null if not found.
    }
}
#endif