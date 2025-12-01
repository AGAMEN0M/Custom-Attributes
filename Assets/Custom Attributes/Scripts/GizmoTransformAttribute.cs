/*
 * ---------------------------------------------------------------------------
 * Description: Attribute + PropertyDrawer that allows any Vector3 field to
 *              display gizmo editing tools in Scene View, plus optional 
 *              rotation editing when linked to a Quaternion property.
 * 
 * Usage:       [GizmoTransform]
 *              public Vector3 position;
 * 
 *              [GizmoTransform(nameof(rotation))]
 *              public Vector3 position;
 *              public Quaternion rotation;
 * 
 * Author: Lucas Gomes Cecchini
 * Pseudonym: AGAMENOM
 * ---------------------------------------------------------------------------
*/

using UnityEngine;

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
#endif

#region === Attribute Definition ===

/// <summary>
/// Attribute that marks a Vector3 property to be editable using custom Scene View gizmos.
/// If a rotation property name is provided, rotation editing will also be available.
/// </summary>
public class GizmoTransformAttribute : PropertyAttribute
{
    /// <summary>
    /// Name of the associated Quaternion property to enable rotation editing.
    /// </summary>
    public readonly string rotationPropertyName;

    /// <summary>
    /// Creates a position-only GizmoTransform attribute.
    /// </summary>
    public GizmoTransformAttribute() { }

    /// <summary>
    /// Creates a GizmoTransform attribute linked to a Quaternion for rotation editing.
    /// </summary>
    public GizmoTransformAttribute(string rotationPropertyName)
    {
        this.rotationPropertyName = rotationPropertyName;
    }
}

#endregion

#if UNITY_EDITOR

#region === GizmoTransformDrawer ===

/// <summary>
/// Custom drawer that renders position/rotation editing buttons in the inspector
/// and handles gizmo-based transformations in the Scene View.
/// </summary>
[CustomPropertyDrawer(typeof(GizmoTransformAttribute))]
public class GizmoTransformDrawer : PropertyDrawer
{
    #region === Per-Property Editing State ===

    // Dictionaries storing per-property editing states.
    private static readonly Dictionary<string, bool> editingPosition = new();
    private static readonly Dictionary<string, bool> editingRotation = new();

    // References to the currently active objects and properties.
    private static SerializedObject activeObject;
    private static SerializedProperty activePosition;
    private static SerializedProperty activeRotation;

    // GUI button content with tooltips.
    private static readonly GUIContent editPositionButtonContent = new(EditorGUIUtility.IconContent("MoveTool")) { tooltip = "Edit Position" };
    private static readonly GUIContent editRotationButtonContent = new(EditorGUIUtility.IconContent("RotateTool")) { tooltip = "Edit Rotation" };

    // Inspector button dimensions.
    private const float buttonWidth = 35f;
    private const float buttonHeight = 25f;
    private const float buttonSpacing = 10f;

    #endregion

    #region === Scene View GUI ===

    /// <summary>
    /// Called every time the Scene View repaints and handles gizmo interaction.
    /// </summary>
    private static void OnSceneGUI(SceneView view)
    {
        // Cancel editing if the user deselects everything.
        if (Selection.activeObject == null)
        {
            StopEditing();
            return;
        }

        // Ensure the target serialized object is still valid.
        if (activeObject == null || activeObject.targetObject == null)
        {
            StopEditing();
            return;
        }

        if (activePosition == null || activePosition.serializedObject == null)
        {
            StopEditing();
            return;
        }

        // prevent errors when deleting an item from a list.
        if (!PropertyIsValid(activePosition))
        {
            StopEditing();
            return;
        }

        if (activeRotation != null && !PropertyIsValid(activeRotation))
        {
            StopEditing();
            return;
        }

        // Sync changes from the target to serialized object.
        activeObject.Update();

        // Extract the component that owns the fields.
        var comp = activeObject.targetObject as Component;
        if (comp == null)
        {
            StopEditing();
            return;
        }

        // Read rotation (if any).
        var rot = activeRotation != null ? activeRotation.quaternionValue : Quaternion.identity;

        // Protect against invalid quaternion (0,0,0,0).
        if (rot.x == 0f && rot.y == 0f && rot.z == 0f && rot.w == 0f) rot = Quaternion.identity;

        // Extract the local position and transform it to world space.
        var pos = activePosition.vector3Value;
        var t = comp.transform;
        var worldPos = t.TransformPoint(pos);
        var worldRot = t.rotation * rot;

        EditorGUI.BeginChangeCheck(); // Begin checking for gizmo changes.

        // --- Handle Position ---
        if (activeRotation == null || (editingPosition.ContainsKey(activePosition.propertyPath) && editingPosition[activePosition.propertyPath]))
        {
            var handleRot = Tools.pivotRotation == PivotRotation.Local ? worldRot : Quaternion.identity;
            var newWorldPos = Handles.PositionHandle(worldPos, handleRot);

            if (EditorGUI.EndChangeCheck())
            {
                // Record undo on the target object (ensures list edits are undoable).
                Undo.RecordObject(activeObject.targetObject, "Gizmo Position");

                // Convert back to local and assign to serialized property.
                activePosition.vector3Value = t.InverseTransformPoint(newWorldPos);

                // Apply and mark dirty so Unity persists change in lists.
                activeObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(activeObject.targetObject);
            }
        }
        // --- Handle Rotation ---
        else if (activeRotation != null && editingRotation.ContainsKey(activePosition.propertyPath) && editingRotation[activePosition.propertyPath])
        {
            var newWorldRot = Handles.RotationHandle(worldRot, worldPos);

            if (EditorGUI.EndChangeCheck())
            {
                // Record undo on the target object (ensures list edits are undoable).
                Undo.RecordObject(activeObject.targetObject, "Gizmo Rotation");

                // Convert rotation back to local-space quaternion and assign.
                activeRotation.quaternionValue = Quaternion.Inverse(t.rotation) * newWorldRot;

                // Apply and mark dirty so Unity persists change in lists.
                activeObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(activeObject.targetObject);
            }
        }

        DrawGizmo(worldPos, worldRot); // Draw gizmo visuals.
    }

    #endregion

    #region === Editing Control ===

    /// <summary>
    /// Toggles the position editing state for a specific property.
    /// </summary>
    private static void TogglePositionEditing(SerializedProperty pos, SerializedProperty rot)
    {
        string key = pos.propertyPath;
        bool current = editingPosition.ContainsKey(key) && editingPosition[key];

        // Disable editing for all other properties.
        foreach (var k in editingPosition.Keys.ToArray()) editingPosition[k] = false;
        foreach (var k in editingRotation.Keys.ToArray()) editingRotation[k] = false;

        // Toggle off if already active.
        if (current)
        {
            StopEditing();
            return;
        }

        // Activate position editing.
        editingPosition[key] = true;

        activeObject = pos.serializedObject;
        activePosition = pos;
        activeRotation = rot;

        // Ensure SceneGUI is active.
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    /// <summary>
    /// Toggles the rotation editing state for a specific property.
    /// </summary>
    private static void ToggleRotationEditing(SerializedProperty pos, SerializedProperty rot)
    {
        string key = pos.propertyPath;
        bool current = editingRotation.ContainsKey(key) && editingRotation[key];

        // Disable editing for all other properties.
        foreach (var k in editingPosition.Keys.ToArray()) editingPosition[k] = false;
        foreach (var k in editingRotation.Keys.ToArray()) editingRotation[k] = false;

        // Toggle off if already active.
        if (current)
        {
            StopEditing();
            return;
        }

        // Activate rotation editing.
        editingRotation[key] = true;

        activeObject = pos.serializedObject;
        activePosition = pos;
        activeRotation = rot;

        // Ensure SceneGUI is active.
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    /// <summary>
    /// Checks if a SerializedProperty is still valid or has been destroyed.
    /// </summary>
    private static bool PropertyIsValid(SerializedProperty p)
    {
        try
        {
            var _ = p.propertyType; // Throws if property is invalid.
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Resets any active gizmo editing when scripts are reloaded by the Unity editor.
    /// Ensures no leftover SceneGUI events or invalid serialized references remain.
    /// </summary>
    [InitializeOnLoadMethod]
    public static void ResetOnScriptsReload() => StopEditing();

    /// <summary>
    /// Stops all editing, clears dictionaries, and refreshes the Scene View.
    /// </summary>
    private static void StopEditing()
    {
        editingPosition.Clear();
        editingRotation.Clear();

        activeObject = null;
        activePosition = null;
        activeRotation = null;

        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.RepaintAll();
    }

    #endregion

    #region === Gizmo Drawing ===

    /// <summary>
    /// Draws the transformation gizmo (sphere + axis lines).
    /// </summary>
    private static void DrawGizmo(Vector3 pos, Quaternion rot)
    {
        // Protect against invalid quaternion as an extra safety.
        if (rot.x == 0f && rot.y == 0f && rot.z == 0f && rot.w == 0f) rot = Quaternion.identity;

        Handles.color = Color.white;
        Handles.SphereHandleCap(0, pos, rot, 0.05f, EventType.Repaint);

        DrawAxis(pos, rot, Vector3.right, Color.red);
        DrawAxis(pos, rot, Vector3.up, Color.green);
        DrawAxis(pos, rot, Vector3.forward, Color.blue);
    }

    /// <summary>
    /// Draws a single colored axis from the gizmo center.
    /// </summary>
    private static void DrawAxis(Vector3 pos, Quaternion rot, Vector3 dir, Color color)
    {
        Handles.color = color;
        Handles.DrawLine(pos, pos + (rot * dir * 0.5f));
    }

    #endregion

    #region === Inspector GUI ===

    /// <summary>
    /// Draws the inspector UI: buttons + Vector3 field.
    /// </summary>
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var att = (GizmoTransformAttribute)attribute;

        // Resolve rotation property using robust approach:
        // 1) Try FindPropertyRelative (works for sibling fields inside same object).
        // 2) If null and path contains Array.data[] then build absolute path based on property path.
        // 3) Fallback to root FindProperty.
        SerializedProperty rot = null;
        bool hasRotation = false;

        if (!string.IsNullOrEmpty(att.rotationPropertyName))
        {
            // 1) Try relative lookup first (works for simple cases and nested classes that are directly parent).
            rot = property.FindPropertyRelative(att.rotationPropertyName);

            // 2) If not found, and property is inside array/list, attempt to build absolute path.
            if (rot == null)
            {
                string path = property.propertyPath;

                if (path.Contains("Array.data["))
                {
                    int lastDot = path.LastIndexOf('.');
                    if (lastDot >= 0)
                    {
                        string parentPath = path[..lastDot]; // e.g. gizmoTransform.Array.data[0]
                        string absoluteRotPath = parentPath + "." + att.rotationPropertyName;
                        rot = property.serializedObject.FindProperty(absoluteRotPath);
                    }
                }
            }

            // 3) Final fallback - search by name at root level.
            rot ??= property.serializedObject.FindProperty(att.rotationPropertyName);

            hasRotation = rot != null;
        }

        string key = property.propertyPath;

        // Ensure dictionary entries exist.
        if (!editingPosition.ContainsKey(key)) editingPosition[key] = false;
        if (!editingRotation.ContainsKey(key)) editingRotation[key] = false;

        // --- Position Button ---
        GUI.color = editingPosition[key] ? Color.white : Color.gray;

        Rect btnRect1 = new(position.x, position.y, buttonWidth, buttonHeight);
        if (GUI.Button(btnRect1, editPositionButtonContent)) TogglePositionEditing(property, rot);

        GUI.color = Color.white;

        // --- Rotation Button ---
        if (hasRotation)
        {
            if (!editingRotation.ContainsKey(key)) editingRotation[key] = false;

            GUI.color = editingRotation[key] ? Color.white : Color.gray;

            Rect btnRect2 = new(position.x + buttonWidth + buttonSpacing, position.y, buttonWidth, buttonHeight);
            if (GUI.Button(btnRect2, editRotationButtonContent)) ToggleRotationEditing(property, rot);

            GUI.color = Color.white;
        }

        // Draw the Vector3 property field below buttons.
        Rect fieldRect = new(position.x, position.y + buttonHeight + buttonSpacing, position.width, EditorGUIUtility.singleLineHeight);

        EditorGUI.PropertyField(fieldRect, property, label, true);
    }

    /// <summary>
    /// Calculates inspector height to fit the buttons + field.
    /// </summary>
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return buttonHeight + buttonSpacing + EditorGUIUtility.singleLineHeight;
    }

    #endregion
}

#endregion

#endif