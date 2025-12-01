/*
 * ---------------------------------------------------------------------------
 * Description: Attribute + Custom PropertyDrawer that allows a Vector3 field
 *              to be edited as a 3D gizmo sphere directly in Scene View.
 *              The attribute links an offset field to a float radius field and
 *              optionally allows specifying an RGB color for the gizmo.
 *              Supports persistent editing state, snapping with Ctrl, undo,
 *              safe handling of arrays/nested properties, and editor-only code.
 *
 * Usage:       [GizmoSphere(nameof(radius))]
 *              public Vector3 sphereOffset;
 *              public float radius;
 *
 *              [GizmoSphere(nameof(radius), 1, 0, 0)]
 *              public Vector3 sphereOffset;
 *              public float radius;
 *
 * Author:      Lucas Gomes Cecchini.
 * Pseudonym:   AGAMENOM.
 * ---------------------------------------------------------------------------
*/

using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using System.Linq;
#endif

#region === Attribute Definition ===

/// <summary>
/// Attribute used to enable Scene View sphere editing for a Vector3 offset field.
/// It links the offset to a float radius property and optionally sets a custom color.
/// </summary>
public class GizmoSphereAttribute : PropertyAttribute
{
    /// <summary>
    /// The serialized field name containing the sphere radius.
    /// </summary>
    public readonly string radiusPropertyName;

    /// <summary>
    /// Custom color used for drawing the sphere gizmo.
    /// </summary>
    public readonly Color customColor;

    /// <summary>
    /// Constructor that uses default green color.
    /// </summary>
    public GizmoSphereAttribute(string radiusPropertyName)
    {
        this.radiusPropertyName = radiusPropertyName;
        this.customColor = Color.green;
    }

    /// <summary>
    /// Constructor that allows specifying a custom RGB color.
    /// </summary>
    public GizmoSphereAttribute(string radiusPropertyName, float r, float g, float b)
    {
        this.radiusPropertyName = radiusPropertyName;
        this.customColor = new Color(r, g, b, 1f);
    }
}

#endregion

#if UNITY_EDITOR

#region === GizmoSphereDrawer ===

/// <summary>
/// Custom PropertyDrawer responsible for drawing the inspector button and
/// handling Scene View sphere manipulation, including editing offset and radius.
/// </summary>
[CustomPropertyDrawer(typeof(GizmoSphereAttribute))]
public class GizmoSphereDrawer : PropertyDrawer
{
    #region === Persistent Editing State ===

    // Maps propertyPath -> attribute for active editing properties.
    private static readonly Dictionary<string, GizmoSphereAttribute> editingAttributes = new();

    // Maps propertyPath -> whether it is currently being edited.
    private static readonly Dictionary<string, bool> editingSphere = new();

    // Currently active serialized object and properties for editing.
    private static SerializedObject activeObject;
    private static SerializedProperty activeOffset;
    private static SerializedProperty activeRadius;

    // UI constants for the inspector button.
    private const float buttonWidth = 35f;
    private const float buttonHeight = 25f;
    private const float buttonSpacing = 6f;

    // Button icon for editing.
    private static readonly GUIContent editButtonContent = new(EditorGUIUtility.IconContent("EditCollider")) { tooltip = "Edit Sphere" };

    #endregion

    #region === Scene GUI ===

    /// <summary>
    /// Scene GUI callback that draws the sphere handles and updates radius using a simple distance calculation.
    /// </summary>
    private static void OnSceneGUI(SceneView view)
    {
        // Basic validation.
        if (Selection.activeObject == null)
        {
            StopEditing();
            return;
        }
        if (activeObject == null || activeObject.targetObject == null)
        {
            StopEditing();
            return;
        }
        if (!PropertyIsValid(activeOffset) || !PropertyIsValid(activeRadius))
        {
            StopEditing();
            return;
        }

        // Sync serialized object.
        activeObject.Update();

        // Must be attached to a Component.
        var comp = activeObject.targetObject as Component;
        if (comp == null)
        {
            StopEditing();
            return;
        }

        var t = comp.transform;

        // Get values.
        Vector3 localOffset = activeOffset.vector3Value;
        float radius = Mathf.Abs(activeRadius.floatValue);

        // World center of the sphere.
        Vector3 worldCenter = t.TransformPoint(localOffset);

        // Get attribute color.
        editingAttributes.TryGetValue(activeOffset.propertyPath, out GizmoSphereAttribute attr);
        Color drawColor = (attr != null ? attr.customColor : Color.green);

        Handles.color = drawColor;

        EditorGUI.BeginChangeCheck();

        // Compute handle size.
        float handleSize = HandleUtility.GetHandleSize(worldCenter) * 0.1f;

        // Positions of handles in world space.
        Vector3 topPos = worldCenter + t.up * radius;
        Vector3 bottomPos = worldCenter - t.up * radius;
        Vector3 rightPos = worldCenter + t.right * radius;
        Vector3 leftPos = worldCenter - t.right * radius;
        Vector3 frontPos = worldCenter + t.forward * radius;
        Vector3 backPos = worldCenter - t.forward * radius;

        // Create IDs.
        int idTop = GUIUtility.GetControlID(FocusType.Passive) + 1;
        int idBot = GUIUtility.GetControlID(FocusType.Passive) + 2;
        int idLeft = GUIUtility.GetControlID(FocusType.Passive) + 3;
        int idRight = GUIUtility.GetControlID(FocusType.Passive) + 4;
        int idFront = GUIUtility.GetControlID(FocusType.Passive) + 5;
        int idBack = GUIUtility.GetControlID(FocusType.Passive) + 6;

        // Move handles.
        topPos = Handles.FreeMoveHandle(idTop, topPos, handleSize, Vector3.zero, Handles.SphereHandleCap);
        bottomPos = Handles.FreeMoveHandle(idBot, bottomPos, handleSize, Vector3.zero, Handles.SphereHandleCap);
        leftPos = Handles.FreeMoveHandle(idLeft, leftPos, handleSize, Vector3.zero, Handles.SphereHandleCap);
        rightPos = Handles.FreeMoveHandle(idRight, rightPos, handleSize, Vector3.zero, Handles.SphereHandleCap);
        frontPos = Handles.FreeMoveHandle(idFront, frontPos, handleSize, Vector3.zero, Handles.SphereHandleCap);
        backPos = Handles.FreeMoveHandle(idBack, backPos, handleSize, Vector3.zero, Handles.SphereHandleCap);

        // If changed, update radius.
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(activeObject.targetObject, "Edit Sphere Radius");

            int hot = GUIUtility.hotControl;

            float newRadius = radius;

            // Determine which handle moved.
            if (hot == idTop) newRadius = Vector3.Distance(worldCenter, topPos);
            else if (hot == idBot) newRadius = Vector3.Distance(worldCenter, bottomPos);
            else if (hot == idRight) newRadius = Vector3.Distance(worldCenter, rightPos);
            else if (hot == idLeft) newRadius = Vector3.Distance(worldCenter, leftPos);
            else if (hot == idFront) newRadius = Vector3.Distance(worldCenter, frontPos);
            else if (hot == idBack) newRadius = Vector3.Distance(worldCenter, backPos);

            // Optional snapping (same as your original script).
            if (Event.current.control)
            {
                const float snap = 0.5f;
                newRadius = Mathf.Round(newRadius / snap) * snap;
            }

            // Apply radius (offset NEVER changes).
            activeRadius.floatValue = Mathf.Max(0f, newRadius);

            activeObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(activeObject.targetObject);
        }

        // Draw visual sphere (wire + translucent).
        DrawSphereGizmo(worldCenter, radius, t.rotation, drawColor);
    }

    #endregion

    #region === Gizmo Drawing ===

    /// <summary>
    /// Draws a wireframe sphere and a faint translucent sphere for context.
    /// </summary>
    private static void DrawSphereGizmo(Vector3 center, float radius, Quaternion rotation, Color color)
    {
        // Draw wire sphere using Handles with full alpha.
        Handles.color = new Color(color.r, color.g, color.b, 1f);
        // Unity does not have a built-in Handles.DrawWireSphere with rotation,
        // but wire sphere is rotation-invariant, so we can call DrawWireDisc on three planes for approximate sphere.
        Handles.DrawWireDisc(center, rotation * Vector3.up, radius);
        Handles.DrawWireDisc(center, rotation * Vector3.right, radius);
        Handles.DrawWireDisc(center, rotation * Vector3.forward, radius);

        // Draw translucent discs on three principal planes to give volume sense.
        Color faceColor = new(color.r, color.g, color.b, 0.07f);
        Handles.color = faceColor;
        Handles.DrawSolidDisc(center, rotation * Vector3.up, radius * 0.999f);
        Handles.DrawSolidDisc(center, rotation * Vector3.right, radius * 0.999f);
        Handles.DrawSolidDisc(center, rotation * Vector3.forward, radius * 0.999f);

        // Reset handles color.
        Handles.color = Color.white;
    }

    #endregion

    #region === Inspector GUI ===

    /// <summary>
    /// Draws the inspector UI, including the edit toggle button and the property field.
    /// </summary>
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var localAttr = attribute as GizmoSphereAttribute;

        // Attempt to find the linked radius property relative to the offset property.
        var radiusProp = property.FindPropertyRelative(localAttr.radiusPropertyName);

        // If the property is inside an array or nested object, try to resolve full path.
        if (radiusProp == null && property.propertyPath.Contains("Array.data["))
        {
            string path = property.propertyPath;
            int lastDot = path.LastIndexOf('.');
            if (lastDot >= 0)
            {
                string parent = path[..lastDot];
                string fullPath = parent + "." + localAttr.radiusPropertyName;
                radiusProp = property.serializedObject.FindProperty(fullPath);
            }
        }

        // As fallback, try to find by name on the root object.
        radiusProp ??= property.serializedObject.FindProperty(localAttr.radiusPropertyName);

        // Ensure we have a key for persistent editing state.
        string key = property.propertyPath;
        if (!editingSphere.ContainsKey(key)) editingSphere[key] = false;

        // Set button color based on whether this property is being edited.
        GUI.color = editingSphere[key] ? Color.white : Color.gray;

        // Draw the edit button.
        Rect btnRect = new(position.x, position.y, buttonWidth, buttonHeight);
        if (GUI.Button(btnRect, editButtonContent)) ToggleEditing(property, radiusProp, localAttr);

        // Reset GUI state.
        GUI.color = Color.white;

        // Draw the property field below the button.
        Rect fieldRect = new(position.x, position.y + buttonHeight + buttonSpacing, position.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.PropertyField(fieldRect, property, label, true);
    }

    /// <summary>
    /// Ensures the drawer reserves enough vertical space for the button and the field.
    /// </summary>
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // Reserve space for the button, spacing, and a single-line property field.
        return buttonHeight + buttonSpacing + EditorGUIUtility.singleLineHeight;
    }

    #endregion

    #region === Editing State Control ===

    /// <summary>
    /// Toggles sphere editing on/off for a given property and ensures only one sphere is edited at a time.
    /// </summary>
    private static void ToggleEditing(SerializedProperty offset, SerializedProperty radius, GizmoSphereAttribute attr)
    {
        string key = offset.propertyPath;
        bool isEditing = editingSphere.ContainsKey(key) && editingSphere[key];

        // Turn off editing for all spheres first.
        foreach (var k in editingSphere.Keys.ToList()) editingSphere[k] = false;

        editingAttributes.Clear();

        // If already editing the same sphere, stop editing.
        if (isEditing)
        {
            StopEditing();
            return;
        }

        // Start editing for this sphere.
        editingSphere[key] = true;

        activeObject = offset.serializedObject;
        activeOffset = offset;
        activeRadius = radius;

        editingAttributes[key] = attr;

        // Attach scene gui callback.
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;

        // Force repaint to show handles.
        SceneView.RepaintAll();
    }

    /// <summary>
    /// Validates that a SerializedProperty is safe to access to avoid Unity exceptions.
    /// </summary>
    private static bool PropertyIsValid(SerializedProperty p)
    {
        try
        {
            // Accessing propertyType will throw if property is invalid.
            var _ = p.propertyType;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Resets the editing state after scripts recompile or editor reloads.
    /// </summary>
    [InitializeOnLoadMethod]
    public static void ResetOnReload() => StopEditing();

    /// <summary>
    /// Stops editing and clears all references and callbacks.
    /// </summary>
    private static void StopEditing()
    {
        editingAttributes.Clear();
        editingSphere.Clear();

        activeObject = null;
        activeOffset = null;
        activeRadius = null;

        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.RepaintAll();
    }

    #endregion
}

#endregion

#endif