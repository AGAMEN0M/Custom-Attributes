/*
 * ---------------------------------------------------------------------------
 * Description: A custom attribute and editor implementation for Unity that allows the 
 *              addition of buttons in the inspector to invoke methods marked with a 
 *              custom attribute.
 * 
 * Using:       [Button(nameof(myMethod))]
 * 
 * Author: Lucas Gomes Cecchini
 * Pseudonym: AGAMENOM
 * ---------------------------------------------------------------------------
*/

using UnityEngine;
using System;

#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using System.Linq;
#endif

// Custom attribute to be used on methods for creating buttons in the inspector.
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class ButtonAttribute : PropertyAttribute
{
    public string Label { get; }

    // Constructor to set the button label, if provided.
    public ButtonAttribute(string label = null)
    {
        Label = label;
    }
}

#if UNITY_EDITOR
// Custom editor that displays buttons for methods marked with the ButtonAttribute.
[CanEditMultipleObjects]
[CustomEditor(typeof(MonoBehaviour), true)]
public class ButtonDrawerEditor : Editor
{
    // Override the OnInspectorGUI method to customize the inspector UI.
    public override void OnInspectorGUI()
    {
        DrawButtons(target); // Draw all the buttons associated with the methods of the target object.

        EditorGUILayout.Space(10); // Add some space before drawing the default inspector.

        base.OnInspectorGUI(); // Draw the default inspector fields.
    }

    // Method to find and draw buttons for methods marked with ButtonAttribute.
    private void DrawButtons(UnityEngine.Object targetObject)
    {
        // Use reflection to find methods that are marked with the ButtonAttribute and have no parameters.
        var methods = targetObject.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(m => m.GetCustomAttributes(typeof(ButtonAttribute), true).Length > 0 && m.GetParameters().Length == 0);

        // Loop through each method and create a button for it.
        foreach (var method in methods)
        {
            // Get the ButtonAttribute attached to the method (if any).
            var buttonAttr = method.GetCustomAttribute<ButtonAttribute>();

            // Use the label from the attribute or generate a default label from the method name.
            string label = buttonAttr.Label ?? ObjectNames.NicifyVariableName(method.Name);

            // Create a button in the inspector. When clicked, invoke the method.
            if (GUILayout.Button(label))
            {
                // Record the action for undo functionality.
                Undo.RecordObject(targetObject, $"Invoke {method.Name}");

                // Invoke the method on the target object.
                method.Invoke((object)targetObject, null);

                // Mark the object as dirty to ensure changes are saved.
                EditorUtility.SetDirty(targetObject);
            }
        }
    }
}
#endif