/*
 * ---------------------------------------------------------------------------
 * Description: Implements interactive Scene View tools for editing custom
 *              Gizmo positions, rotations, dimensions, and paths.
 *
 * Author: Lucas Gomes Cecchini
 * Pseudonym: AGAMENOM
 * ---------------------------------------------------------------------------
*/

using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEditor;

namespace CustomAttributes.Editing
{
    #region === Shared helpers ===

    /// <summary>
    /// Provides shared coordinate and handle operations for Gizmo tools.
    /// </summary>
    /// <remarks>
    /// Centralizing these operations keeps the interaction behavior consistent
    /// across tools and prevents each tool from implementing its own coordinate
    /// conversion and pivot-orientation rules.
    /// </remarks>
    internal static class GizmoToolUtil
    {
        /// <summary>
        /// Determines the rotation used by interactive Scene View handles.
        /// </summary>
        /// <param name="t">The Transform defining the local coordinate system.</param>
        /// <param name="localRotation">The rotation relative to the Transform.</param>
        /// <returns>The combined world rotation when the Editor uses Local pivot mode; otherwise, the identity rotation for Global mode.</returns>
        public static Quaternion HandleRotation(Transform t, Quaternion localRotation) => Tools.pivotRotation == PivotRotation.Local ? t.rotation * localRotation : Quaternion.identity;

        /// <summary>
        /// Moves a serialized local-space center using a world-space position handle.
        /// </summary>
        /// <param name="t">The Transform that defines the local coordinate system.</param>
        /// <param name="center">The serialized Vector3 property containing the center.</param>
        /// <returns>True if the handle changed the center; otherwise, false.</returns>
        /// <remarks>
        /// PositionHandle operates in world space, while the serialized center
        /// belongs to the component's local space. Converting the edited position
        /// back before storing it keeps the value consistent with the Gizmo's
        /// local-space drawing and preview calculations.
        /// </remarks>
        public static bool MoveCenter(Transform t, SerializedProperty center)
        {
            Vector3 world = t.TransformPoint(center.vector3Value);

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.PositionHandle(world, HandleRotation(t, Quaternion.identity));

            if (!EditorGUI.EndChangeCheck()) return false;

            center.vector3Value = t.InverseTransformPoint(moved);
            center.serializedObject.ApplyModifiedProperties();
            return true;
        }
    }

    #endregion

    #region === Transform (position + optional rotation) ===

    /// <summary>
    /// Provides interactive position and optional rotation editing for a Transform Gizmo.
    /// </summary>
    /// <remarks>
    /// The position and rotation are stored in serialized component fields
    /// rather than applied directly to the GameObject's Transform. This allows
    /// the tool to edit custom positions and orientations used by other systems.
    /// </remarks>
    public sealed class TransformTool : GizmoTool
    {
        #region Identifiers and state

        /// <summary>Identifier for the position editing mode.</summary>
        public const string PositionId = "position";

        /// <summary>Identifier for the rotation editing mode.</summary>
        public const string RotationId = "rotation";

        private readonly GizmoTransformAttribute attribute;
        private readonly bool rotationMode;

        #endregion

        #region Initialization

        /// <summary>
        /// Creates a Transform tool in position or rotation mode.
        /// </summary>
        /// <param name="attribute">The attribute configuration associated with the field.</param>
        /// <param name="rotationMode">Whether the tool edits rotation instead of position.</param>
        public TransformTool(GizmoTransformAttribute attribute, bool rotationMode)
        {
            this.attribute = attribute;
            this.rotationMode = rotationMode;
        }

        /// <summary>
        /// Gets the identifier corresponding to the active editing mode.
        /// </summary>
        public override string Id => rotationMode ? RotationId : PositionId;

        #endregion

        #region Scene interaction

        /// <summary>
        /// Draws the Transform axes and processes position or rotation handle input.
        /// </summary>
        /// <param name="component">The component containing the serialized fields.</param>
        /// <param name="property">The serialized position property.</param>
        /// <param name="view">The Scene View in which the tool is active.</param>
        public override void OnScene(Component component, SerializedProperty property, SceneView view)
        {
            var t = component.transform;
            var rotationProperty = string.IsNullOrEmpty(attribute.RotationPropertyName) ? null : PropertyReflection.FindSibling(property, attribute.RotationPropertyName);

            Quaternion localRotation = ReadRotation(rotationProperty);
            Vector3 worldPosition = t.TransformPoint(property.vector3Value);
            Quaternion worldRotation = t.rotation * localRotation;

            GizmoDraw.Axes(worldPosition, worldRotation);
            Handles.Label(worldPosition + 0.25f * HandleUtility.GetHandleSize(worldPosition) * Vector3.up, property.displayName);

            EditorGUI.BeginChangeCheck();

            if (!rotationMode)
            {
                // Position handles return world-space coordinates, so the result must be converted before updating the serialized field.
                Vector3 moved = Handles.PositionHandle(worldPosition, GizmoToolUtil.HandleRotation(t, localRotation));

                if (EditorGUI.EndChangeCheck())
                {
                    property.vector3Value = t.InverseTransformPoint(moved);
                    property.serializedObject.ApplyModifiedProperties();
                }
            }
            else if (rotationProperty != null)
            {
                Quaternion rotated = Handles.RotationHandle(worldRotation, worldPosition);

                // Remove the component's world rotation to store the edited orientation relative to the component rather than globally.
                if (EditorGUI.EndChangeCheck())
                {
                    WriteRotation(rotationProperty, Quaternion.Inverse(t.rotation) * rotated);
                    property.serializedObject.ApplyModifiedProperties();
                }
            }
            else
            {
                EditorGUI.EndChangeCheck(); // Keep BeginChangeCheck and EndChangeCheck balanced even when rotation mode is active without a valid rotation property.
            }
        }

        #endregion

        #region Rotation serialization

        /// <summary>
        /// Checks whether a serialized property can represent a rotation.
        /// </summary>
        /// <param name="p">The property to validate.</param>
        /// <returns>True for Quaternion and Vector3 properties.</returns>
        /// <remarks>Supporting both types allows rotations to be stored either directly as quaternions or as Euler angles, depending on the component's design.</remarks>
        public static bool IsValidRotationProperty(SerializedProperty p) => p != null && (p.propertyType == SerializedPropertyType.Quaternion || p.propertyType == SerializedPropertyType.Vector3);

        /// <summary>
        /// Reads a serialized rotation and converts it to a Quaternion.
        /// </summary>
        /// <param name="p">The serialized rotation property, if available.</param>
        /// <returns>The represented rotation, or identity when no supported property exists.</returns>
        private static Quaternion ReadRotation(SerializedProperty p)
        {
            if (p == null) return Quaternion.identity;
            if (p.propertyType == SerializedPropertyType.Quaternion) return GizmoDraw.SafeRotation(p.quaternionValue);
            if (p.propertyType == SerializedPropertyType.Vector3) return Quaternion.Euler(p.vector3Value);
            return Quaternion.identity;
        }

        /// <summary>
        /// Writes a rotation using the serialized property's existing representation.
        /// </summary>
        /// <param name="p">The serialized rotation property.</param>
        /// <param name="rotation">The rotation to store.</param>
        /// <remarks>Keeping the property's original type avoids changing the component's serialized data structure when the user edits its rotation.</remarks>
        private static void WriteRotation(SerializedProperty p, Quaternion rotation)
        {
            if (p.propertyType == SerializedPropertyType.Quaternion) p.quaternionValue = rotation;
            else if (p.propertyType == SerializedPropertyType.Vector3) p.vector3Value = rotation.eulerAngles;
        }

        #endregion
    }

    #endregion

    #region === Cube ===

    /// <summary>
    /// Provides interactive resizing and repositioning for a Gizmo cube.
    /// </summary>
    /// <remarks>
    /// BoxBoundsHandle supplies Unity's built-in box editing controls. The
    /// tool transfers values between the handle and serialized properties,
    /// leaving the underlying component as the source of persistent data.
    /// </remarks>
    public sealed class CubeTool : GizmoTool
    {
        #region Identifiers and state

        /// <summary>Identifier for cube resizing mode.</summary>
        public const string SizeId = "size";

        /// <summary>Identifier for cube movement mode.</summary>
        public const string MoveId = "move";

        // Reuse the handle to avoid allocating a new editor control for each
        // Scene View callback.
        private static readonly BoxBoundsHandle handle = new();

        private readonly GizmoCubeAttribute attribute;
        private readonly bool moveMode;

        #endregion

        #region Initialization

        /// <summary>
        /// Creates a cube editing tool.
        /// </summary>
        /// <param name="attribute">The cube's Gizmo configuration.</param>
        /// <param name="moveMode">Whether the tool moves the cube instead of resizing it.</param>
        public CubeTool(GizmoCubeAttribute attribute, bool moveMode)
        {
            this.attribute = attribute;
            this.moveMode = moveMode;
        }

        /// <summary>
        /// Gets the identifier for the current editing mode.
        /// </summary>
        public override string Id => moveMode ? MoveId : SizeId;

        #endregion

        #region Scene interaction

        /// <summary>
        /// Draws and processes the cube's movement or bounds-editing handles.
        /// </summary>
        /// <param name="component">The component containing the cube fields.</param>
        /// <param name="property">The serialized local-space center.</param>
        /// <param name="view">The Scene View in which the tool is active.</param>
        public override void OnScene(Component component, SerializedProperty property, SceneView view)
        {
            var t = component.transform;
            var size = PropertyReflection.FindSibling(property, attribute.SizePropertyName);

            // The bounds handle requires three-dimensional size data. If the linked property is missing or incompatible, no edit is attempted.
            if (size == null || size.propertyType != SerializedPropertyType.Vector3) return;

            // Show the cube while the position handle edits its center.
            if (moveMode)
            {
                GizmoDraw.Cube(t, property.vector3Value, size.vector3Value, attribute.Color);
                GizmoToolUtil.MoveCenter(t, property);
                return;
            }

            using (new Handles.DrawingScope(attribute.Color, Matrix4x4.TRS(t.position, t.rotation, t.lossyScale)))
            {
                handle.center = property.vector3Value;
                handle.size = size.vector3Value;
                handle.SetColor(attribute.Color);

                EditorGUI.BeginChangeCheck();
                handle.DrawHandle();

                if (EditorGUI.EndChangeCheck())
                {
                    property.vector3Value = handle.center;
                    size.vector3Value = handle.size;
                    property.serializedObject.ApplyModifiedProperties();
                }
            }
        }

        #endregion
    }

    #endregion

    #region === Sphere ===

    /// <summary>
    /// Provides interactive resizing and repositioning for a Gizmo sphere.
    /// </summary>
    public sealed class SphereTool : GizmoTool
    {
        #region Identifiers and state

        /// <summary>Identifier for sphere resizing mode.</summary>
        public const string SizeId = "size";

        /// <summary>Identifier for sphere movement mode.</summary>
        public const string MoveId = "move";

        // Reusing the bounds handle avoids repeated allocations during drawing.
        private static readonly SphereBoundsHandle handle = new();

        private readonly GizmoSphereAttribute attribute;
        private readonly bool moveMode;

        #endregion

        #region Initialization

        /// <summary>
        /// Creates a sphere editing tool.
        /// </summary>
        /// <param name="attribute">The sphere's Gizmo configuration.</param>
        /// <param name="moveMode">Whether the tool moves the sphere instead of resizing it.</param>
        public SphereTool(GizmoSphereAttribute attribute, bool moveMode)
        {
            this.attribute = attribute;
            this.moveMode = moveMode;
        }

        /// <summary>
        /// Gets the identifier for the current editing mode.
        /// </summary>
        public override string Id => moveMode ? MoveId : SizeId;

        #endregion

        #region Scene interaction

        /// <summary>
        /// Draws and processes the sphere's movement or radius-editing handles.
        /// </summary>
        /// <param name="component">The component containing the sphere fields.</param>
        /// <param name="property">The serialized local-space center.</param>
        /// <param name="view">The Scene View in which the tool is active.</param>
        public override void OnScene(Component component, SerializedProperty property, SceneView view)
        {
            var t = component.transform;
            var radius = PropertyReflection.FindSibling(property, attribute.RadiusPropertyName);
            if (radius == null || radius.propertyType != SerializedPropertyType.Float) return;

            if (moveMode)
            {
                GizmoDraw.Sphere(t, property.vector3Value, radius.floatValue, attribute.Color);
                GizmoToolUtil.MoveCenter(t, property);
                return;
            }

            float scale = GizmoDraw.UniformScale(t);

            // A near-zero scale would make the world-space handle unusable and would cause unstable conversion back to the stored radius.
            if (scale <= 1e-6f) return;

            Vector3 worldCenter = t.TransformPoint(property.vector3Value);

            using (new Handles.DrawingScope(attribute.Color, Matrix4x4.TRS(worldCenter, t.rotation, Vector3.one * scale)))
            {
                // The handle is centered at the origin of the drawing matrix, which is already positioned at the sphere's world center.
                handle.center = Vector3.zero;
                handle.radius = radius.floatValue;
                handle.SetColor(attribute.Color);

                EditorGUI.BeginChangeCheck();
                handle.DrawHandle();

                if (EditorGUI.EndChangeCheck())
                {
                    // Prevent the handle from storing a negative radius.
                    radius.floatValue = Mathf.Max(0f, handle.radius);
                    property.serializedObject.ApplyModifiedProperties();
                }
            }
        }

        #endregion
    }

    #endregion

    #region === Cone ===

    /// <summary>
    /// Provides interactive movement and dimension editing for a Gizmo cone.
    /// </summary>
    /// <remarks>
    /// The distance handle moves the cone's tip along its forward axis, while
    /// the edge handle adjusts the cone's opening angle. Both operations update
    /// the serialized fields used by the corresponding Gizmo attribute.
    /// </remarks>
    public sealed class ConeTool : GizmoTool
    {
        #region Identifiers and state

        /// <summary>Identifier for cone dimension editing mode.</summary>
        public const string SizeId = "size";

        /// <summary>Identifier for cone movement mode.</summary>
        public const string MoveId = "move";

        private readonly GizmoConeAttribute attribute;
        private readonly bool moveMode;

        #endregion

        #region Initialization

        /// <summary>
        /// Creates a cone editing tool.
        /// </summary>
        /// <param name="attribute">The cone's Gizmo configuration.</param>
        /// <param name="moveMode">Whether the tool moves the cone instead of changing its dimensions.</param>
        public ConeTool(GizmoConeAttribute attribute, bool moveMode)
        {
            this.attribute = attribute;
            this.moveMode = moveMode;
        }

        /// <summary>
        /// Gets the identifier for the current editing mode.
        /// </summary>
        public override string Id => moveMode ? MoveId : SizeId;

        #endregion

        #region Scene interaction

        /// <summary>
        /// Draws the cone and processes its position, distance, and angle handles.
        /// </summary>
        /// <param name="component">The component containing the cone fields.</param>
        /// <param name="property">The serialized local-space origin.</param>
        /// <param name="view">The Scene View in which the tool is active.</param>
        public override void OnScene(Component component, SerializedProperty property, SceneView view)
        {
            var t = component.transform;
            var angle = PropertyReflection.FindSibling(property, attribute.AnglePropertyName);
            var distance = PropertyReflection.FindSibling(property, attribute.DistancePropertyName);

            // Both dimensions are required to draw and edit a valid cone.
            if (angle == null || distance == null) return;
            if (angle.propertyType != SerializedPropertyType.Float || distance.propertyType != SerializedPropertyType.Float) return;

            GizmoDraw.Cone(t, property.vector3Value, angle.floatValue, distance.floatValue, attribute.Color);

            if (moveMode)
            {
                GizmoToolUtil.MoveCenter(t, property);
                return;
            }

            float scale = GizmoDraw.UniformScale(t);
            if (scale <= 1e-6f) return;

            Vector3 origin = t.TransformPoint(property.vector3Value);
            Vector3 forward = t.forward;
            float length = Mathf.Abs(distance.floatValue) * scale;
            float half = Mathf.Clamp(angle.floatValue * 0.5f, 0f, 180f);

            Vector3 tip = origin + forward * length;
            float tipSize = HandleUtility.GetHandleSize(tip) * 0.1f;

            using (new Handles.DrawingScope(attribute.Color))
            {
                #region Distance handle

                EditorGUI.BeginChangeCheck();
                Vector3 newTip = Handles.Slider(tip, forward, tipSize, Handles.DotHandleCap, 0f);

                if (EditorGUI.EndChangeCheck())
                {
                    // Project the moved tip onto the forward axis. This makes distance depend on movement along the cone, not sideways mouse displacement, and converts it back from world scale.
                    distance.floatValue = Mathf.Max(0f, Vector3.Dot(newTip - origin, forward) / scale);
                    property.serializedObject.ApplyModifiedProperties();
                    return;
                }

                #endregion

                #region Angle handle

                Vector3 edgeDirection = Quaternion.AngleAxis(half, t.up) * forward;
                Vector3 edge = origin + edgeDirection * length;
                float edgeSize = HandleUtility.GetHandleSize(edge) * 0.08f;

                EditorGUI.BeginChangeCheck();
                Vector3 newEdge = Handles.FreeMoveHandle(edge, edgeSize, Vector3.zero, Handles.DotHandleCap);

                // A handle positioned exactly at the origin has no meaningful direction, so leave the existing angle intact.
                if (EditorGUI.EndChangeCheck())
                {
                    Vector3 direction = newEdge - origin;
                    if (direction.sqrMagnitude > 1e-8f)
                    {
                        // The handle represents one side of the cone. Doubling its angle from the forward axis recovers the full angle.
                        angle.floatValue = Mathf.Clamp(Vector3.Angle(forward, direction) * 2f, 0f, 360f);
                        property.serializedObject.ApplyModifiedProperties();
                    }
                }

                #endregion
            }
        }

        #endregion
    }

    #endregion

    #region === Path ===

    /// <summary>
    /// Provides interactive editing of a serialized array of Vector3 path points.
    /// </summary>
    /// <remarks>
    /// The tool supports inserting points between existing segments and moving
    /// selected points. It can work with either local-space or world-space data,
    /// as configured by the associated GizmoPathAttribute.
    /// </remarks>
    public sealed class PathTool : GizmoTool
    {
        #region State

        /// <summary>Identifier for the path editing tool.</summary>
        public const string ToolId = "path";

        private readonly GizmoPathAttribute attribute;

        // Store the selected point index instead of a SerializedProperty
        // reference, which may become invalid between Scene View callbacks.
        private int selected = -1;

        #endregion

        #region Initialization

        /// <summary>
        /// Creates a path editing tool.
        /// </summary>
        /// <param name="attribute">The path's Gizmo configuration.</param>
        public PathTool(GizmoPathAttribute attribute) => this.attribute = attribute;

        /// <summary>
        /// Gets the identifier for the path editing tool.
        /// </summary>
        public override string Id => ToolId;

        #endregion

        #region Scene interaction

        /// <summary>
        /// Draws the path, inserts points on segment clicks, and moves the selected point.
        /// </summary>
        /// <param name="component">The component containing the path array.</param>
        /// <param name="property">The serialized array of Vector3 points.</param>
        /// <param name="view">The Scene View in which the tool is active.</param>
        public override void OnScene(Component component, SerializedProperty property, SceneView view)
        {
            // Path editing requires an array of Vector3 elements. Reject other property types before attempting to read or modify their contents.
            if (!property.isArray) return;

            int count = property.arraySize;
            if (count == 0) return;
            if (property.GetArrayElementAtIndex(0).propertyType != SerializedPropertyType.Vector3) return;

            var t = component.transform;
            bool world = attribute.WorldSpace;
            Color color = attribute.Color;

            // Build a world-space copy for drawing and handle interaction.
            // Serialized values remain in their configured coordinate space.
            var points = new Vector3[count];
            for (int i = 0; i < count; i++) points[i] = GizmoDraw.ToWorld(t, property.GetArrayElementAtIndex(i).vector3Value, world);

            bool closed = attribute.Closed && count > 2;

            #region Path lines

            using (new Handles.DrawingScope(color))
            {
                for (int i = 0; i < count - 1; i++) Handles.DrawLine(points[i], points[i + 1]);
                if (closed) Handles.DrawLine(points[count - 1], points[0]);
            }

            #endregion

            #region Point insertion

            // A closed path has one additional segment connecting the last point to the first. Open paths stop at the final point.
            int segments = closed ? count : count - 1;
            for (int i = 0; i < segments; i++)
            {
                Vector3 middle = (points[i] + points[(i + 1) % count]) * 0.5f;
                float size = HandleUtility.GetHandleSize(middle) * 0.05f;

                Handles.color = new Color(color.r, color.g, color.b, 0.6f);
                if (Handles.Button(middle, Quaternion.identity, size, size * 1.5f, Handles.DotHandleCap))
                {
                    // Insert immediately after the segment's starting point.
                    // Convert the midpoint back to the storage coordinate space before assigning it to the newly inserted array element.
                    Insert(property, i + 1, GizmoDraw.ToLocal(t, middle, world));
                    selected = i + 1;
                    return;
                }
            }

            #endregion

            #region Point selection

            for (int i = 0; i < count; i++)
            {
                float size = HandleUtility.GetHandleSize(points[i]) * 0.08f;
                Handles.color = color;
                Handles.Label(points[i] + 2.5f * size * Vector3.up, i.ToString());

                // Do not let the selection button interfere with the position handle of the point that is already selected.
                if (i == selected) continue;
                if (Handles.Button(points[i], Quaternion.identity, size, size * 1.5f, Handles.SphereHandleCap)) selected = i;
            }

            #endregion

            #region Selected point movement

            if (selected >= 0 && selected < count)
            {
                // World-space paths use global axes. Local-space paths respect the Transform's orientation when the Editor uses Local mode.
                Quaternion rotation = world ? Quaternion.identity : GizmoToolUtil.HandleRotation(t, Quaternion.identity);

                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(points[selected], rotation);

                if (EditorGUI.EndChangeCheck())
                {
                    property.GetArrayElementAtIndex(selected).vector3Value = GizmoDraw.ToLocal(t, moved, world);
                    property.serializedObject.ApplyModifiedProperties();
                }
            }

            #endregion
        }

        #endregion

        #region Array editing

        /// <summary>
        /// Inserts a new point into the serialized path array.
        /// </summary>
        /// <param name="array">The serialized array to modify.</param>
        /// <param name="index">The insertion index.</param>
        /// <param name="localPosition">The point value to assign to the new element.</param>
        /// <remarks>
        /// The new element is explicitly assigned after insertion so the path
        /// does not depend on Unity's default value or duplicated-element behavior
        /// when expanding serialized arrays.
        /// </remarks>
        private static void Insert(SerializedProperty array, int index, Vector3 localPosition)
        {
            array.InsertArrayElementAtIndex(index);
            array.GetArrayElementAtIndex(index).vector3Value = localPosition;
            array.serializedObject.ApplyModifiedProperties();
        }

        #endregion
    }

    #endregion
}