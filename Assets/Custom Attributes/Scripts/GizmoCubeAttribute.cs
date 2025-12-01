/*
 * ---------------------------------------------------------------------------
 * Description: Attribute + Custom PropertyDrawer that allows a Vector3 field
 *              to be edited as a 3D gizmo cube directly in Scene View. The 
 *              user can adjust offset and size visually through handles, with
 *              optional custom color. Includes snapping with Ctrl and internal
 *              persistent editing state for seamless interaction.
 * 
 * Usage:       [GizmoCube(nameof(cubeSize))]
 *              public Vector3 cubeOffset; // Local-space offset.
 *              public Vector3 cubeSize;   // Local-space cube size.
 *              
 *              [GizmoCube(nameof(cubeSize), 1, 0, 0)]
 *              public Vector3 cubeOffset; // Local-space offset.
 *              public Vector3 cubeSize;   // Local-space cube size.
 * 
 * Author:      Lucas Gomes Cecchini
 * Pseudonym:   AGAMENOM
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
/// Attribute used to enable Scene View cube editing for a Vector3 field.  
/// It links an offset field to a size field and optionally allows setting  
/// a custom gizmo color.
/// </summary>
public class GizmoCubeAttribute : PropertyAttribute
{
    /// <summary>
    /// The serialized field name containing the cube size.  
    /// Used to access the correct Vector3 field at runtime.
    /// </summary>
    public readonly string sizePropertyName;

    /// <summary>
    /// Custom color used for drawing the cube gizmo.
    /// </summary>
    public readonly Color customColor;

    /// <summary>
    /// Constructor that uses default cyan color.  
    /// </summary>
    public GizmoCubeAttribute(string sizePropertyName)
    {
        this.sizePropertyName = sizePropertyName;
        this.customColor = Color.cyan;
    }

    /// <summary>
    /// Constructor that allows specifying a custom RGB color.  
    /// </summary>
    public GizmoCubeAttribute(string sizePropertyName, float r, float g, float b)
    {
        this.sizePropertyName = sizePropertyName;
        this.customColor = new Color(r, g, b, 1f);
    }
}

#endregion

#if UNITY_EDITOR

#region === GizmoCubeDrawer ===

/// <summary>
/// Custom PropertyDrawer responsible for drawing the inspector button and  
/// handling Scene View cube manipulation, including editing offset and size.
/// </summary>
[CustomPropertyDrawer(typeof(GizmoCubeAttribute))]
public class GizmoCubeDrawer : PropertyDrawer
{
    #region === Persistent Editing State ===

    // Stores which property is currently linked to which GizmoCubeAttribute.
    private static readonly Dictionary<string, GizmoCubeAttribute> editingAttributes = new();

    // Stores which cube (per-property) is currently being edited.
    private static readonly Dictionary<string, bool> editingCube = new();

    // Currently active serialized object and properties.
    private static SerializedObject activeObject;
    private static SerializedProperty activeOffset;
    private static SerializedProperty activeSize;

    // UI constants for the editor button.
    private const float buttonWidth = 35f;
    private const float buttonHeight = 25f;
    private const float buttonSpacing = 10f;

    // Button icon for editing.
    private static readonly GUIContent editCubeButtonContent = new(EditorGUIUtility.IconContent("EditCollider")) { tooltip = "Edit Cube" };

    #endregion

    #region === Scene GUI ===

    /// <summary>
    /// Handles all SceneView logic for drawing and interacting with cube handles.  
    /// Called every frame during Scene View rendering.
    /// </summary>
    private static void OnSceneGUI(SceneView view)
    {
        // Validates selections and active properties.
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

        if (!PropertyIsValid(activeOffset) || !PropertyIsValid(activeSize))
        {
            StopEditing();
            return;
        }

        // Updates serialized object before operations.
        activeObject.Update();

        // Ensures attached object is a Component.
        var comp = activeObject.targetObject as Component;
        if (comp == null)
        {
            StopEditing();
            return;
        }

        var t = comp.transform;

        // Retrieves current offset and size values.
        Vector3 localOffset = activeOffset.vector3Value;
        Vector3 localSize = activeSize.vector3Value;

        // Converts offset to world space.
        Vector3 worldCenter = t.TransformPoint(localOffset);

        // Converts size into world half-extents.
        Vector3 worldHalf = t.TransformVector(localSize) * 0.5f;

        // Retrieves color from attribute.
        GizmoCubeAttribute currentAttr = null;
        if (activeOffset != null) editingAttributes.TryGetValue(activeOffset.propertyPath, out currentAttr);

        Color drawColor = currentAttr != null ? currentAttr.customColor : Color.cyan;
        Handles.color = drawColor;

        EditorGUI.BeginChangeCheck();

        // Creates handle positions along each axis.
        Vector3[] handlesPos =
        {
            worldCenter + new Vector3(worldHalf.x, 0, 0),
            worldCenter - new Vector3(worldHalf.x, 0, 0),
            worldCenter + new Vector3(0, worldHalf.y, 0),
            worldCenter - new Vector3(0, worldHalf.y, 0),
            worldCenter + new Vector3(0, 0, worldHalf.z),
            worldCenter - new Vector3(0, 0, worldHalf.z)
        };

        // Draws free-move handles for each axis.
        for (int i = 0; i < handlesPos.Length; i++)
        {
            float size = HandleUtility.GetHandleSize(handlesPos[i]) * 0.1f;
            Vector3 original = handlesPos[i];

            if (Event.current.control)
            {
                // Snapping (Ctrl).
                Vector3 moved = Handles.FreeMoveHandle(handlesPos[i], size, Vector3.zero, Handles.CubeHandleCap);
                const float snap = 1f;

                Vector3 snapped = new(Mathf.Round(moved.x / snap) * snap, Mathf.Round(moved.y / snap) * snap, Mathf.Round(moved.z / snap) * snap);
                Vector3 corrected = original;

                if (Mathf.Abs(original.x - snapped.x) > snap) corrected.x = original.x + Mathf.Sign(snapped.x - original.x) * snap;
                if (Mathf.Abs(original.y - snapped.y) > snap) corrected.y = original.y + Mathf.Sign(snapped.y - original.y) * snap;
                if (Mathf.Abs(original.z - snapped.z) > snap) corrected.z = original.z + Mathf.Sign(snapped.z - original.z) * snap;

                handlesPos[i] = corrected;
            }
            else
            {
                // Free movement without snapping.
                handlesPos[i] = Handles.FreeMoveHandle(handlesPos[i], size, Vector3.zero, Handles.CubeHandleCap);
            }
        }

        // If any handle changed, recalculate offset and size.
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(activeObject.targetObject, "Edit Cube Gizmo");

            // Recomputes world size.
            Vector3 halfX = new(Mathf.Abs(handlesPos[0].x - handlesPos[1].x) / 2f, 0, 0);
            Vector3 halfY = new(0, Mathf.Abs(handlesPos[2].y - handlesPos[3].y) / 2f, 0);
            Vector3 halfZ = new(0, 0, Mathf.Abs(handlesPos[4].z - handlesPos[5].z) / 2f);
            Vector3 newHalfSizeWorld = new(halfX.x, halfY.y, halfZ.z);
            Vector3 newWorldSize = newHalfSizeWorld * 2f;

            // Converts world size to local.
            Vector3 localNewSize = t.InverseTransformVector(newWorldSize);
            activeSize.vector3Value = new Vector3(Mathf.Abs(localNewSize.x), Mathf.Abs(localNewSize.y), Mathf.Abs(localNewSize.z));

            // Recomputes center.
            Vector3 newCenterWorld = new((handlesPos[0].x + handlesPos[1].x) * 0.5f, (handlesPos[2].y + handlesPos[3].y) * 0.5f, (handlesPos[4].z + handlesPos[5].z) * 0.5f);

            activeOffset.vector3Value = t.InverseTransformPoint(newCenterWorld);

            activeObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(activeObject.targetObject);
        }

        // Draw final cube gizmo.
        DrawGizmo(worldCenter, localSize, t, drawColor);
    }

    #endregion

    #region === Gizmo Drawing ===

    /// <summary>
    /// Draws the wireframe cube and its translucent faces in Scene View.
    /// </summary>
    private static void DrawGizmo(Vector3 center, Vector3 localSize, Transform t, Color color)
    {
        Vector3 halfLocal = localSize * 0.5f;

        // Calculates direction vectors in world space.
        Vector3 right = t.rotation * Vector3.right * halfLocal.x;
        Vector3 up = t.rotation * Vector3.up * halfLocal.y;
        Vector3 forward = t.rotation * Vector3.forward * halfLocal.z;

        // Corner points.
        Vector3 p0 = center + right + up + forward;
        Vector3 p1 = center + right + up - forward;
        Vector3 p2 = center + right - up - forward;
        Vector3 p3 = center + right - up + forward;
        Vector3 p4 = center - right + up + forward;
        Vector3 p5 = center - right + up - forward;
        Vector3 p6 = center - right - up - forward;
        Vector3 p7 = center - right - up + forward;

        // Wireframe size.
        Vector3 approxWorldSize = (right * 2f) + (up * 2f) + (forward * 2f);
        Vector3 wireSize = new(Mathf.Abs(approxWorldSize.x), Mathf.Abs(approxWorldSize.y), Mathf.Abs(approxWorldSize.z));

        Handles.color = new(color.r, color.g, color.b, 1);
        Handles.DrawWireCube(center, wireSize);

        // Face colors.
        Color faceColor = new(color.r, color.g, color.b, 0.1f);
        Color outlineColor = Color.clear;

        // Each face.
        Handles.DrawSolidRectangleWithOutline(new[] { p0, p1, p2, p3 }, faceColor, outlineColor);
        Handles.DrawSolidRectangleWithOutline(new[] { p4, p5, p6, p7 }, faceColor, outlineColor);
        Handles.DrawSolidRectangleWithOutline(new[] { p4, p0, p3, p7 }, faceColor, outlineColor);
        Handles.DrawSolidRectangleWithOutline(new[] { p5, p1, p2, p6 }, faceColor, outlineColor);
        Handles.DrawSolidRectangleWithOutline(new[] { p4, p5, p1, p0 }, faceColor, outlineColor);
        Handles.DrawSolidRectangleWithOutline(new[] { p7, p6, p2, p3 }, faceColor, outlineColor);
    }

    #endregion

    #region === Inspector GUI ===

    /// <summary>
    /// Draws the inspector UI, including the edit button and the field itself.
    /// </summary>
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var localAttr = attribute as GizmoCubeAttribute;

        // Attempts to find linked size property.
        var sizeProp = property.FindPropertyRelative(localAttr.sizePropertyName);

        // Handles arrays and nested objects.
        if (sizeProp == null && property.propertyPath.Contains("Array.data["))
        {
            string path = property.propertyPath;
            int lastDot = path.LastIndexOf('.');

            if (lastDot >= 0)
            {
                string parent = path[..lastDot];
                string fullPath = parent + "." + localAttr.sizePropertyName;
                sizeProp = property.serializedObject.FindProperty(fullPath);
            }
        }

        sizeProp ??= property.serializedObject.FindProperty(localAttr.sizePropertyName);
        string key = property.propertyPath;

        if (!editingCube.ContainsKey(key)) editingCube[key] = false;

        GUI.color = editingCube[key] ? Color.white : Color.gray;

        // Draws the edit button.
        Rect btn = new(position.x, position.y, buttonWidth, buttonHeight);
        if (GUI.Button(btn, editCubeButtonContent)) ToggleEditing(property, sizeProp, localAttr);

        GUI.color = Color.white;

        // Draws the property field below.
        Rect fieldRect = new(position.x, position.y + buttonHeight + buttonSpacing, position.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.PropertyField(fieldRect, property, label, true);
    }

    /// <summary>
    /// Ensures the drawer reserves enough vertical space for the button.
    /// </summary>
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return buttonHeight + buttonSpacing + EditorGUIUtility.singleLineHeight;
    }

    #endregion

    #region === Editing State Control ===

    /// <summary>
    /// Toggles cube editing on/off for a given property.  
    /// Ensures only one cube can be edited at a time.
    /// </summary>
    private static void ToggleEditing(SerializedProperty offset, SerializedProperty size, GizmoCubeAttribute attr)
    {
        string key = offset.propertyPath;
        bool isEditing = editingCube.ContainsKey(key) && editingCube[key];

        // Turns off editing for all cubes.
        foreach (var k in editingCube.Keys.ToArray()) editingCube[k] = false;

        editingAttributes.Clear();

        // If already editing, stop.
        if (isEditing)
        {
            StopEditing();
            return;
        }

        // Activates editing.
        editingCube[key] = true;

        activeObject = offset.serializedObject;
        activeOffset = offset;
        activeSize = size;

        editingAttributes[key] = attr;

        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    /// <summary>
    /// Validates that a SerializedProperty is safe to access.  
    /// Helps prevent Unity exceptions on broken/invalid references.
    /// </summary>
    private static bool PropertyIsValid(SerializedProperty p)
    {
        try
        {
            var _ = p.propertyType;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Automatically resets the editing state after scripts recompile.
    /// </summary>
    [InitializeOnLoadMethod]
    public static void ResetOnReload() => StopEditing();

    /// <summary>
    /// Fully clears all editing references and detaches SceneView events.
    /// </summary>
    private static void StopEditing()
    {
        editingAttributes.Clear();
        editingCube.Clear();

        activeObject = null;
        activeOffset = null;
        activeSize = null;

        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.RepaintAll();
    }

    #endregion
}

#endregion

#endif