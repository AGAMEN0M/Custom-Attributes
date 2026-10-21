/*
 * ---------------------------------------------------------------------------
 * Description: Core utilities for drawing, managing, and previewing custom
 *              Gizmos and interactive Scene View tools in the Unity Editor.
 *
 * Author: Lucas Gomes Cecchini
 * Pseudonym: AGAMENOM
 * ---------------------------------------------------------------------------
*/

using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using System;

using Object = UnityEngine.Object;

namespace CustomAttributes.Editing
{
    #region === Icons ===

    /// <summary>
    /// Provides shared icon content for Gizmo-related editor controls.
    /// </summary>
    /// <remarks>Centralizing these icons keeps toolbar buttons visually consistent and ensures their tooltips are defined in one place.</remarks>
    internal static class GizmoIcons
    {
        public static readonly GUIContent Move = Make("MoveTool", "Move");
        public static readonly GUIContent Rotate = Make("RotateTool", "Rotate");
        public static readonly GUIContent Edit = Make("EditCollider", "Edit shape in the Scene View");
        public static readonly GUIContent Path = Make("EditCollider", "Edit path in the Scene View");

        /// <summary>
        /// Creates GUI content from a built-in Unity Editor icon.
        /// </summary>
        /// <param name="icon">The internal name of the Unity Editor icon.</param>
        /// <param name="tooltip">The tooltip displayed when hovering over the icon.</param>
        /// <returns>GUI content containing the icon and its tooltip.</returns>
        private static GUIContent Make(string icon, string tooltip)
        {
            GUIContent content = new(EditorGUIUtility.IconContent(icon))
            {
                tooltip = tooltip
            };
            return content;
        }
    }

    #endregion

    #region === Drawing helpers ===

    /// <summary>
    /// Provides reusable drawing operations for custom Gizmos in the Scene View.
    /// </summary>
    /// <remarks>
    /// These helpers separate geometric drawing from the attributes and tools
    /// that request it. This allows preview rendering and interactive tools
    /// to share the same visual representation.
    /// </remarks>
    internal static class GizmoDraw
    {
        // Reuse the same vertex buffer when drawing cube faces to avoid allocating a new array every time the Scene View repaints.
        private static readonly Vector3[] quad = new Vector3[4];

        /// <summary>
        /// Returns the largest absolute component of a Transform's world scale.
        /// </summary>
        /// <param name="t">The Transform whose scale is evaluated.</param>
        /// <returns>A non-negative scale suitable for uniformly sized Gizmos.</returns>
        /// <remarks>
        /// Using the largest axis keeps spherical and distance-based previews
        /// visible under non-uniform scaling, rather than shrinking them along
        /// whichever axis happens to be smallest.
        /// </remarks>
        public static float UniformScale(Transform t)
        {
            Vector3 s = t.lossyScale;
            return Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        }

        /// <summary>
        /// Replaces an all-zero quaternion with the identity rotation.
        /// </summary>
        /// <param name="q">The rotation to validate.</param>
        /// <returns>The original rotation, or the identity rotation if all components are zero.</returns>
        /// <remarks>
        /// An all-zero quaternion is not a valid rotation. Checking for this
        /// specific value prevents invalid serialized data from propagating
        /// into rotation calculations used by the Gizmo drawing code.
        /// </remarks>
        public static Quaternion SafeRotation(Quaternion q)
        {
            bool zero = q.x == 0f && q.y == 0f && q.z == 0f && q.w == 0f;
            return zero ? Quaternion.identity : q;
        }

        /// <summary>
        /// Draws the local right, up, and forward axes at a world-space position.
        /// </summary>
        /// <param name="position">The world-space origin of the axes.</param>
        /// <param name="rotation">The rotation that determines the axis directions.</param>
        /// <remarks>HandleUtility.GetHandleSize adapts the axes to the Scene View camera, keeping them readable as the camera zooms in and out.</remarks>
        public static void Axes(Vector3 position, Quaternion rotation)
        {
            float size = HandleUtility.GetHandleSize(position) * 0.6f;
            using (new Handles.DrawingScope(Color.red)) Handles.DrawLine(position, position + rotation * Vector3.right * size);
            using (new Handles.DrawingScope(Color.green)) Handles.DrawLine(position, position + rotation * Vector3.up * size);
            using (new Handles.DrawingScope(Color.blue)) Handles.DrawLine(position, position + rotation * Vector3.forward * size);
        }

        /// <summary>
        /// Draws a wireframe cube with translucent faces.
        /// </summary>
        /// <param name="t">The Transform that defines the cube's world transform.</param>
        /// <param name="center">The cube center in the Transform's local space.</param>
        /// <param name="size">The cube dimensions in local space.</param>
        /// <param name="color">The color used for the outline and translucent faces.</param>
        /// <remarks>
        /// The drawing matrix applies the Transform's position, rotation, and
        /// lossy scale, so the supplied center and dimensions remain local-space
        /// values. Each face is built from its normal axis and the two remaining
        /// axes, avoiding the need to maintain separate face definitions.
        /// </remarks>
        public static void Cube(Transform t, Vector3 center, Vector3 size, Color color)
        {
            using (new Handles.DrawingScope(color, Matrix4x4.TRS(t.position, t.rotation, t.lossyScale)))
            {
                Handles.DrawWireCube(center, size);

                Vector3 half = size * 0.5f;
                Color face = new(color.r, color.g, color.b, 0.08f);

                for (int axis = 0; axis < 3; axis++)
                {
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        // The selected axis defines the face plane; the other two axes define the four corners within that plane.
                        int u = (axis + 1) % 3;
                        int v = (axis + 2) % 3;

                        Vector3 normal = Vector3.zero;
                        normal[axis] = sign * half[axis];

                        Vector3 du = Vector3.zero;
                        du[u] = half[u];

                        Vector3 dv = Vector3.zero;
                        dv[v] = half[v];

                        quad[0] = center + normal + du + dv;
                        quad[1] = center + normal - du + dv;
                        quad[2] = center + normal - du - dv;
                        quad[3] = center + normal + du - dv;

                        // Keep the fill subtle so the wireframe and scene objects remain visible through the preview.
                        Handles.DrawSolidRectangleWithOutline(quad, face, Color.clear);
                    }
                }
            }
        }

        /// <summary>
        /// Draws a spherical preview centered at a local-space offset.
        /// </summary>
        /// <param name="t">The Transform used to convert the offset to world space.</param>
        /// <param name="offset">The sphere center relative to the Transform.</param>
        /// <param name="radius">The sphere radius before scaling.</param>
        /// <param name="color">The preview color.</param>
        /// <remarks>
        /// Three circles aligned with the Transform axes communicate the sphere's
        /// orientation in the scene. An additional camera-facing circle and
        /// translucent disc improve depth perception when a Scene View camera is available.
        /// </remarks>
        public static void Sphere(Transform t, Vector3 offset, float radius, Color color)
        {
            Vector3 center = t.TransformPoint(offset);
            float r = Mathf.Abs(radius) * UniformScale(t);

            using (new Handles.DrawingScope(color))
            {
                Handles.DrawWireDisc(center, t.up, r);
                Handles.DrawWireDisc(center, t.right, r);
                Handles.DrawWireDisc(center, t.forward, r);

                if (Camera.current != null)
                {
                    Vector3 view = Camera.current.transform.forward;
                    Handles.DrawWireDisc(center, view, r);

                    Handles.color = new Color(color.r, color.g, color.b, 0.06f);
                    Handles.DrawSolidDisc(center, view, r);
                }
            }
        }

        /// <summary>
        /// Draws a cone-shaped preview originating at a local-space offset.
        /// </summary>
        /// <param name="t">The Transform that defines the cone's orientation.</param>
        /// <param name="offset">The cone origin relative to the Transform.</param>
        /// <param name="angle">The full cone angle in degrees.</param>
        /// <param name="distance">The cone length before scaling.</param>
        /// <param name="color">The preview color.</param>
        /// <remarks>
        /// The end-cap center and radius are derived from the half-angle using
        /// trigonometry. Clamping the half-angle prevents values outside the
        /// supported 0-to-180-degree range from producing unexpected geometry.
        /// The wireframe arcs show the cone's spread, while a translucent arc
        /// makes its volume easier to interpret.
        /// </remarks>
        public static void Cone(Transform t, Vector3 offset, float angle, float distance, Color color)
        {
            Vector3 origin = t.TransformPoint(offset);
            Vector3 forward = t.forward;
            Vector3 up = t.up;
            Vector3 right = t.right;

            float half = Mathf.Clamp(angle * 0.5f, 0f, 180f);
            float length = Mathf.Abs(distance) * UniformScale(t);
            float capDistance = length * Mathf.Cos(half * Mathf.Deg2Rad);
            float capRadius = length * Mathf.Sin(half * Mathf.Deg2Rad);
            Vector3 capCenter = origin + forward * capDistance;

            using (new Handles.DrawingScope(color))
            {
                Handles.DrawWireDisc(capCenter, forward, capRadius);

                // Four radial lines provide a quick visual reference for the cone's extent without requiring a densely sampled mesh.
                for (int i = 0; i < 4; i++)
                {
                    Vector3 radial = Quaternion.AngleAxis(i * 90f, forward) * up;
                    Handles.DrawLine(origin, capCenter + radial * capRadius);
                }

                Vector3 fromHorizontal = Quaternion.AngleAxis(-half, up) * forward;
                Vector3 fromVertical = Quaternion.AngleAxis(-half, right) * forward;
                Handles.DrawWireArc(origin, up, fromHorizontal, half * 2f, length);
                Handles.DrawWireArc(origin, right, fromVertical, half * 2f, length);

                Handles.color = new Color(color.r, color.g, color.b, 0.06f);
                Handles.DrawSolidArc(origin, up, fromHorizontal, half * 2f, length);
            }
        }

        /// <summary>
        /// Draws a sequence of points as a path, optionally connecting its final point to its first.
        /// </summary>
        /// <param name="t">The Transform used when points are in local space.</param>
        /// <param name="points">The points that define the path.</param>
        /// <param name="closed">Whether to connect the last point back to the first.</param>
        /// <param name="worldSpace">Whether the supplied points are already in world space.</param>
        /// <param name="color">The path color.</param>
        /// <remarks>
        /// Each point is marked and labeled by its index. Direction indicators
        /// are placed at segment midpoints when a segment has a non-negligible
        /// length, avoiding invalid look rotations for coincident points.
        /// </remarks>
        public static void Path(Transform t, IList<Vector3> points, bool closed, bool worldSpace, Color color)
        {
            int count = points.Count;
            if (count == 0) return;

            using (new Handles.DrawingScope(color))
            {
                for (int i = 0; i < count; i++)
                {
                    Vector3 current = ToWorld(t, points[i], worldSpace);
                    float size = HandleUtility.GetHandleSize(current);

                    Handles.SphereHandleCap(0, current, Quaternion.identity, size * 0.12f, EventType.Repaint);
                    Handles.Label(current + 0.2f * size * Vector3.up, i.ToString());

                    bool hasNext = i < count - 1 || (closed && count > 2);
                    if (!hasNext) continue;

                    Vector3 next = ToWorld(t, points[(i + 1) % count], worldSpace);
                    Handles.DrawLine(current, next);
                    Vector3 direction = next - current;

                    // A zero-length segment has no usable direction, so skip its arrow instead of attempting to construct a rotation.
                    if (direction.sqrMagnitude > 1e-6f)
                    {
                        Vector3 middle = (current + next) * 0.5f;
                        Handles.ConeHandleCap(0, middle, Quaternion.LookRotation(direction), HandleUtility.GetHandleSize(middle) * 0.12f, EventType.Repaint);
                    }
                }
            }
        }

        /// <summary>
        /// Converts a point to world space when it is defined in local space.
        /// </summary>
        /// <param name="t">The Transform defining local space.</param>
        /// <param name="point">The point to convert.</param>
        /// <param name="worldSpace">Whether the point is already in world space.</param>
        /// <returns>The point in world space.</returns>
        public static Vector3 ToWorld(Transform t, Vector3 point, bool worldSpace) => worldSpace ? point : t.TransformPoint(point);

        /// <summary>
        /// Converts a world-space point to local space when required.
        /// </summary>
        /// <param name="t">The Transform defining local space.</param>
        /// <param name="point">The point to convert.</param>
        /// <param name="worldSpace">Whether the point is defined in world space.</param>
        /// <returns>The point in the coordinate space expected by the caller.</returns>
        public static Vector3 ToLocal(Transform t, Vector3 point, bool worldSpace) => worldSpace ? point : t.InverseTransformPoint(point);
    }

    #endregion

    #region === Tool + session ===

    /// <summary>
    /// Defines the contract for an interactive Gizmo editing tool.
    /// </summary>
    /// <remarks>
    /// Concrete tools provide a stable identifier and implement their own
    /// Scene View interaction. GizmoSession manages activation and lifecycle
    /// independently of each tool's specific editing behavior.
    /// </remarks>
    public abstract class GizmoTool
    {
        /// <summary>
        /// Gets the identifier used to distinguish this tool from other tools.
        /// </summary>
        public abstract string Id { get; }

        /// <summary>
        /// Draws or processes this tool's interaction in the Scene View.
        /// </summary>
        /// <param name="component">The component being edited.</param>
        /// <param name="property">The serialized property associated with the tool.</param>
        /// <param name="view">The Scene View currently being rendered.</param>
        public abstract void OnScene(Component component, SerializedProperty property, SceneView view);
    }

    /// <summary>
    /// Stores the UI content and factory used to activate a Gizmo tool.
    /// </summary>
    public sealed class GizmoButton
    {
        /// <summary>The identifier associated with the tool.</summary>
        public readonly string Id;

        /// <summary>The icon and tooltip displayed by the button.</summary>
        public readonly GUIContent Content;

        /// <summary>Creates an instance of the tool when the button is activated.</summary>
        public readonly Func<GizmoTool> Factory;

        /// <summary>
        /// Creates a button definition for a Gizmo tool.
        /// </summary>
        /// <param name="id">The tool identifier.</param>
        /// <param name="content">The content displayed by the button.</param>
        /// <param name="factory">The function that creates the tool instance.</param>
        public GizmoButton(string id, GUIContent content, Func<GizmoTool> factory)
        {
            Id = id;
            Content = content;
            Factory = factory;
        }
    }

    /// <summary>
    /// Manages the active Gizmo editing tool and its Scene View callback.
    /// </summary>
    /// <remarks>
    /// Only one tool session is active at a time. The session records the target
    /// object and serialized property path so the tool can reacquire the property
    /// during Scene View callbacks instead of retaining a SerializedProperty
    /// across editor events, when that reference may no longer be valid.
    /// </remarks>
    public static class GizmoSession
    {
        private static Object target;
        private static string propertyPath;
        private static GizmoTool tool;

        /// <summary>
        /// Initializes editor callbacks and clears any stale session after a reload.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void Init()
        {
            Stop();

            // Remove callbacks before adding them to prevent duplicate registrations when Unity reinitializes editor code.
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;

            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;

            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;
        }

        /// <summary>
        /// Ends editing whenever the Editor's play mode state changes.
        /// </summary>
        private static void OnPlayModeChanged(PlayModeStateChange state) => Stop();

        /// <summary>
        /// Ends editing if the active target is no longer part of the selection.
        /// </summary>
        private static void OnSelectionChanged()
        {
            if (target is Component component && component != null && Selection.Contains(component.gameObject)) return;
            Stop();
        }

        /// <summary>
        /// Determines whether the specified property is being edited by a particular tool.
        /// </summary>
        /// <param name="property">The serialized property to check.</param>
        /// <param name="toolId">The expected tool identifier.</param>
        /// <returns>True if the active session matches the target, path, and tool.</returns>
        internal static bool IsActive(SerializedProperty property, string toolId) => tool != null && target == property.serializedObject.targetObject && propertyPath == property.propertyPath && tool.Id == toolId;

        /// <summary>
        /// Determines whether the given object and property path belong to the active session.
        /// </summary>
        /// <param name="obj">The object being edited.</param>
        /// <param name="path">The serialized property path.</param>
        /// <returns>True if that object and path are currently being edited.</returns>
        internal static bool IsEditing(Object obj, string path) => tool != null && target == obj && propertyPath == path;

        /// <summary>
        /// Activates the requested tool, or deactivates it if it is already active for the same property.
        /// </summary>
        /// <param name="property">The serialized property associated with the tool.</param>
        /// <param name="newTool">The tool to activate.</param>
        /// <remarks>
        /// Stopping the previous session first ensures that switching tools
        /// cannot leave multiple Scene View callbacks or stale target state
        /// behind. Selecting the already-active tool toggles editing off.
        /// </remarks>
        internal static void Toggle(SerializedProperty property, GizmoTool newTool)
        {
            bool wasActive = IsActive(property, newTool.Id);
            Stop();
            if (wasActive) return;

            target = property.serializedObject.targetObject;
            propertyPath = property.propertyPath;
            tool = newTool;

            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Ends the current session and removes its Scene View callback.
        /// </summary>
        /// <remarks>This method is also used during selection changes, play mode transitions, and assembly reloads, making it the common cleanup path.</remarks>
        public static void Stop()
        {
            if (tool == null && target == null) return;

            SceneView.duringSceneGui -= OnSceneGUI;
            target = null;
            propertyPath = null;
            tool = null;
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Reacquires the active serialized property and delegates drawing to the current tool.
        /// </summary>
        /// <param name="view">The Scene View invoking this callback.</param>
        private static void OnSceneGUI(SceneView view)
        {
            if (tool == null || target == null)
            {
                Stop();
                return;
            }

            // Editing is limited to selected Components so the tool does not continue operating on an object that is no longer being edited.
            if (target is not Component component || !Selection.Contains(component.gameObject))
            {
                Stop();
                return;
            }

            using var so = new SerializedObject(target);
            var property = so.FindProperty(propertyPath);

            // The property may disappear after a field rename or a serialized structure change, so the session must terminate if it cannot be found.
            if (property == null)
            {
                Stop();
                return;
            }

            tool.OnScene(component, property, view);
        }
    }

    #endregion

    #region === Always-show preview ===

    /// <summary>
    /// Draws attribute-based Gizmo previews for selected GameObjects.
    /// </summary>
    /// <remarks>
    /// Attributes marked with AlwaysShow are rendered without activating an
    /// interactive tool. Reflection results are cached by component type to
    /// avoid repeating field discovery on every Scene View repaint.
    /// </remarks>
    internal static class GizmoPreview
    {
        // Cache field metadata and attribute instances per type: reflection is needed when discovering preview fields, not on every repaint.
        private static readonly Dictionary<Type, List<(FieldInfo field, GizmoAttributeBase attribute)>> cache = new();

        // Reuse buffers while traversing selected objects and path points to reduce recurring allocations during Scene View rendering.
        private static readonly List<MonoBehaviour> buffer = new();
        private static readonly List<Vector3> pathBuffer = new();

        /// <summary>
        /// Registers the preview callback with the Scene View.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void Init()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        /// <summary>
        /// Retrieves the fields with AlwaysShow Gizmo attributes for a component type.
        /// </summary>
        /// <param name="type">The concrete component type to inspect.</param>
        /// <returns>The cached field and attribute pairs for that type.</returns>
        /// <remarks>
        /// Each class in the inheritance chain is inspected separately with
        /// DeclaredOnly. This allows inherited component fields to be included
        /// without relying on reflection to return inherited private fields.
        /// </remarks>
        private static List<(FieldInfo field, GizmoAttributeBase attribute)> GetEntries(Type type)
        {
            if (cache.TryGetValue(type, out var entries)) return entries;

            entries = new List<(FieldInfo field, GizmoAttributeBase attribute)>();
            for (var t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
            {
                foreach (var field in t.GetFields(PropertyReflection.AllMembers | BindingFlags.DeclaredOnly))
                {
                    var attribute = field.GetCustomAttribute<GizmoAttributeBase>(false);
                    if (attribute != null && attribute.AlwaysShow) entries.Add((field, attribute));
                }
            }

            cache[type] = entries;
            return entries;
        }

        /// <summary>
        /// Draws all eligible previews for the currently selected GameObjects.
        /// </summary>
        /// <param name="view">The Scene View invoking the callback.</param>
        /// <remarks>
        /// Restricting drawing to Repaint events avoids repeating rendering work
        /// during layout, mouse, and keyboard events. Active edit sessions are
        /// skipped to prevent the passive preview from overlapping the tool's
        /// interactive representation of the same field.
        /// </remarks>
        private static void OnSceneGUI(SceneView view)
        {
            if (Event.current.type != EventType.Repaint) return;

            foreach (var go in Selection.gameObjects)
            {
                if (go == null) continue;

                go.GetComponents(buffer);
                foreach (var component in buffer)
                {
                    if (component == null) continue;

                    foreach (var (field, attribute) in GetEntries(component.GetType()))
                    {
                        if (GizmoSession.IsEditing(component, field.Name)) continue;
                        Draw(component, field, attribute);
                    }
                }
            }

            buffer.Clear();
        }

        /// <summary>
        /// Reads an attributed field and draws the matching Gizmo preview.
        /// </summary>
        /// <param name="component">The component containing the attributed field.</param>
        /// <param name="field">The field that stores the preview's primary value.</param>
        /// <param name="attribute">The Gizmo attribute defining how the value is drawn.</param>
        /// <remarks>
        /// Additional dimensions, such as radius or angle, are read through
        /// PropertyReflection using the names stored in each attribute. Pattern
        /// matching keeps each drawing branch restricted to the expected value
        /// type and avoids drawing incomplete previews when required data is absent.
        /// </remarks>
        private static void Draw(MonoBehaviour component, FieldInfo field, GizmoAttributeBase attribute)
        {
            var t = component.transform;
            object value = field.GetValue(component);

            switch (attribute)
            {
                case GizmoCubeAttribute cube when value is Vector3 cubeOffset && PropertyReflection.TryRead(component, cube.SizePropertyName, out Vector3 cubeSize):
                    GizmoDraw.Cube(t, cubeOffset, cubeSize, cube.Color);
                    break;

                case GizmoSphereAttribute sphere when value is Vector3 sphereOffset && PropertyReflection.TryRead(component, sphere.RadiusPropertyName, out float radius):
                    GizmoDraw.Sphere(t, sphereOffset, radius, sphere.Color);
                    break;

                case GizmoConeAttribute cone when value is Vector3 coneOffset && PropertyReflection.TryRead(component, cone.AnglePropertyName, out float angle) && PropertyReflection.TryRead(component, cone.DistancePropertyName, out float distance):
                    GizmoDraw.Cone(t, coneOffset, angle, distance, cone.Color);
                    break;

                case GizmoTransformAttribute transformAttr when value is Vector3 position:
                    {
                        Quaternion rotation = Quaternion.identity;
                        string rotationName = transformAttr.RotationPropertyName;

                        if (!string.IsNullOrEmpty(rotationName))
                        {
                            // Rotation fields may be stored as either a Quaternion or Euler angles, so try the supported representations before retaining the identity rotation.
                            if (PropertyReflection.TryRead(component, rotationName, out Quaternion q)) rotation = GizmoDraw.SafeRotation(q);
                            else if (PropertyReflection.TryRead(component, rotationName, out Vector3 euler)) rotation = Quaternion.Euler(euler);
                        }

                        GizmoDraw.Axes(t.TransformPoint(position), t.rotation * rotation);
                        break;
                    }

                case GizmoPathAttribute path when value is IList list:
                    {
                        pathBuffer.Clear();

                        // Only Vector3 entries can define path points. Ignoring other values preserves compatibility with non-generic lists.
                        foreach (var item in list)
                        {
                            if (item is Vector3 point) pathBuffer.Add(point);
                        }

                        GizmoDraw.Path(t, pathBuffer, path.Closed, path.WorldSpace, path.Color);
                        break;
                    }
            }
        }
    }

    #endregion
}