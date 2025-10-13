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

#region === Attribute Definition ===

/// <summary>
/// Attribute used to conditionally hide properties in the Unity Inspector
/// based on the value of other boolean properties.
/// </summary>
public class ConditionalHideAttribute : PropertyAttribute
{
    /// <summary>
    /// Fields that determine the condition for hiding the property.
    /// </summary>
    public string[] ConditionalSourceFields { get; private set; }

    /// <summary>
    /// If true, the property will be hidden if any of the conditions are false.
    /// </summary>
    public bool HideIfAnyFalse { get; private set; }

    /// <summary>
    /// Constructor accepting an array of conditional source fields.
    /// The property will be hidden if there are multiple conditions and any are false.
    /// </summary>
    /// <param name="conditionalSourceFields">Names of the source fields to check.</param>
    public ConditionalHideAttribute(params string[] conditionalSourceFields)
    {
        ConditionalSourceFields = conditionalSourceFields;
        HideIfAnyFalse = conditionalSourceFields.Length > 1; // Hide if more than one condition and any are false.
    }

    /// <summary>
    /// Constructor allowing the option to hide if any of the conditions are false.
    /// </summary>
    /// <param name="hideIfAnyFalse">If true, hides the property if any condition is false.</param>
    /// <param name="conditionalSourceFields">Names of the source fields to check.</param>
    public ConditionalHideAttribute(bool hideIfAnyFalse, params string[] conditionalSourceFields)
    {
        ConditionalSourceFields = conditionalSourceFields;
        HideIfAnyFalse = conditionalSourceFields.Length > 1 && hideIfAnyFalse; // Hide based on multiple conditions and the provided boolean.
    }
}

#endregion

#if UNITY_EDITOR

#region === ConditionalHidePropertyDrawer ===

/// <summary>
/// Custom PropertyDrawer to control how properties with ConditionalHideAttribute
/// are drawn in the Unity Inspector.
/// </summary>
[CustomPropertyDrawer(typeof(ConditionalHideAttribute))]
public class ConditionalHidePropertyDrawer : PropertyDrawer
{
    #region === OnGUI ===

    /// <summary>
    /// Determines whether the property should be drawn based on the attribute's conditions.
    /// </summary>
    /// <param name="position">Position rect in the inspector.</param>
    /// <param name="property">The property being drawn.</param>
    /// <param name="label">GUI label for the property.</param>
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Get the ConditionalHide attribute applied to the property.
        var hideAttribute = (ConditionalHideAttribute)attribute;

        // Determine if the property should be hidden based on its conditions.
        bool shouldHide = hideAttribute.ConditionalSourceFields.Length > 1
            ? (hideAttribute.HideIfAnyFalse ? CheckAllConditions(property, hideAttribute.ConditionalSourceFields) : CheckAnyCondition(property, hideAttribute.ConditionalSourceFields))
            : CheckSingleCondition(property, hideAttribute.ConditionalSourceFields[0]);

        if (shouldHide) return; // If the condition is met, don't draw the property.

        EditorGUI.PropertyField(position, property, label, true); // Otherwise, draw the property as usual.
    }

    #endregion

    #region === GetPropertyHeight ===

    /// <summary>
    /// Returns the height of the property. Returns 0 if the property should be hidden.
    /// </summary>
    /// <param name="property">The property being drawn.</param>
    /// <param name="label">GUI label for the property.</param>
    /// <returns>Height of the property field.</returns>
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // Get the ConditionalHide attribute applied to the property.
        var hideAttribute = (ConditionalHideAttribute)attribute;

        // Determine if the property should be hidden based on its conditions.
        bool shouldHide = hideAttribute.ConditionalSourceFields.Length > 1
            ? (hideAttribute.HideIfAnyFalse ? CheckAllConditions(property, hideAttribute.ConditionalSourceFields) : CheckAnyCondition(property, hideAttribute.ConditionalSourceFields))
            : CheckSingleCondition(property, hideAttribute.ConditionalSourceFields[0]);

        // Return height 0 if hidden, otherwise use the default property height.
        if (shouldHide) return 0;

        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    #endregion

    #region === Condition Checks ===

    /// <summary>
    /// Checks a single condition to determine if the property should be hidden.
    /// </summary>
    private bool CheckSingleCondition(SerializedProperty property, string conditionalSourceField)
    {
        var conditionProperty = GetConditionProperty(property, conditionalSourceField);

        // If the condition property is null or not a boolean, treat it as false, otherwise check its value.
        return conditionProperty == null || conditionProperty.propertyType != SerializedPropertyType.Boolean || !conditionProperty.boolValue;
    }

    /// <summary>
    /// Checks all conditions and returns true if any condition fails.
    /// </summary>
    private bool CheckAllConditions(SerializedProperty property, string[] conditionalSourceFields)
    {
        foreach (string condition in conditionalSourceFields)
        {
            var conditionProperty = GetConditionProperty(property, condition);

            // Return true (hide the property) if any condition is false or not a boolean.
            if (conditionProperty == null || conditionProperty.propertyType != SerializedPropertyType.Boolean || !conditionProperty.boolValue)
            {
                return true;
            }
        }
        return false; // All conditions are true, don't hide the property.
    }

    /// <summary>
    /// Checks if any condition is true; returns true if all are false.
    /// </summary>
    private bool CheckAnyCondition(SerializedProperty property, string[] conditionalSourceFields)
    {
        foreach (string condition in conditionalSourceFields)
        {
            var conditionProperty = GetConditionProperty(property, condition);

            // If any condition is true, don't hide the property.
            if (conditionProperty != null && conditionProperty.propertyType == SerializedPropertyType.Boolean && conditionProperty.boolValue)
            {
                return false;
            }
        }
        return true; // All conditions are false, hide the property.
    }

    #endregion

    #region === Property Retrieval ===

    /// <summary>
    /// Retrieves the condition property based on the given field name, handling nested properties.
    /// </summary>
    private SerializedProperty GetConditionProperty(SerializedProperty property, string propertyName)
    {
        // Attempt to find the property directly by name.
        var conditionProperty = property.serializedObject.FindProperty(propertyName);

        // If the property isn't found, handle nested properties by splitting the path.
        if (conditionProperty == null)
        {
            string[] pathParts = propertyName.Split('.');
            var currentProperty = property.serializedObject.FindProperty(pathParts[0]);

            // Traverse through the path to find the nested property.
            for (int i = 1; i < pathParts.Length; i++)
            {
                if (currentProperty != null) currentProperty = currentProperty.FindPropertyRelative(pathParts[i]);
            }

            conditionProperty = currentProperty;
        }

        return conditionProperty; // Return the found condition property or null if not found.
    }

    #endregion
}

#endregion

#endif