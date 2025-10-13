/*
 * ---------------------------------------------------------------------------
 * Description: A custom attribute and editor implementation for Unity that allows the 
 *              addition of buttons in the inspector to invoke methods marked with a 
 *              custom attribute.
 * 
 * Using:       [Button(nameof(MyMethod))]
 * 
 * Author:      Lucas Gomes Cecchini
 * Pseudonym:   AGAMENOM
 * ---------------------------------------------------------------------------
*/

using UnityEngine;
using System;

#if UNITY_EDITOR
using System.Text.RegularExpressions;
using System.Reflection;
using UnityEditor;
using System.Linq;
#endif

#region === Attribute Definition ===

/// <summary>
/// Attribute used to create a button in the Unity Inspector that invokes a method.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class ButtonAttribute : PropertyAttribute
{
    /// <summary>
    /// Optional label for the button. If not provided, the method name will be formatted automatically.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Constructor that allows an optional label for the button.
    /// It is recommended to use 'nameof(MethodName)' when specifying the label,
    /// because it is safer and prevents typos if the method name changes.
    /// </summary>
    /// <param name="label">
    /// Optional label to display on the button. Use 'nameof(MethodName)' for safety.
    /// </param>
    public ButtonAttribute(string label = null)
    {
        Label = label;
    }
}

#endregion

#if UNITY_EDITOR

#region === Custom Inspector ===

/// <summary>
/// Custom editor that displays buttons in the Unity Inspector for methods
/// marked with the <see cref="ButtonAttribute"/>.
/// </summary>
[CanEditMultipleObjects]
[CustomEditor(typeof(MonoBehaviour), true)]
public class ButtonDrawerEditor : Editor
{
    /// <summary>
    /// Draws the inspector GUI, including custom buttons for marked methods.
    /// </summary>
    public override void OnInspectorGUI()
    {
        // Draw all buttons for methods that use the ButtonAttribute.
        DrawButtons(target);

        // Add spacing between custom buttons and the default Inspector content.
        EditorGUILayout.Space(10);

        // Draw the default Inspector fields.
        base.OnInspectorGUI();
    }

    /// <summary>
    /// Finds all methods marked with the <see cref="ButtonAttribute"/> and creates
    /// corresponding buttons in the Unity Inspector.
    /// </summary>
    /// <param name="targetObject">The target MonoBehaviour being inspected.</param>
    private void DrawButtons(UnityEngine.Object targetObject)
    {
        // Find all instance methods (public or private) that have the ButtonAttribute and contain no parameters, since buttons cannot pass arguments.
        var methods = targetObject
            .GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m.GetCustomAttributes(typeof(ButtonAttribute), true).Length > 0 && m.GetParameters().Length == 0);

        // Iterate through each valid method found.
        foreach (var method in methods)
        {
            // Retrieve the ButtonAttribute instance attached to this method.
            var buttonAttr = method.GetCustomAttribute<ButtonAttribute>();

            // Create a button in the Inspector using the formatted method name.
            // If clicked, it will execute the associated method.
            if (GUILayout.Button(FormatMethodName(method.Name)))
            {
                // Record the operation for Unity’s Undo system.
                Undo.RecordObject(targetObject, $"Invoke {method.Name}");

                // Invoke the method on the target instance.
                method.Invoke(targetObject, null);

                // Mark the object as dirty so Unity recognizes it as changed.
                EditorUtility.SetDirty(targetObject);
            }
        }
    }

    /// <summary>
    /// Formats a method name to be more human-readable for button labels.
    /// Example: "GetIDText" → "Get ID Text".
    /// It handles both lowercase-to-uppercase transitions and acronym separation.
    /// </summary>
    /// <param name="methodName">The original method name.</param>
    /// <returns>A formatted string suitable for display as a button label.</returns>
    private string FormatMethodName(string methodName)
    {
        // Insert a space before uppercase letters that follow lowercase ones.
        string result = Regex.Replace(methodName, @"([a-z])([A-Z])", "$1 $2");

        // Insert a space between groups of uppercase letters followed by lowercase letters.
        result = Regex.Replace(result, @"([A-Z]+)([A-Z][a-z])", "$1 $2");

        // Remove any leading or trailing whitespace and return the final label.
        return result.Trim();
    }
}

#endregion

#endif