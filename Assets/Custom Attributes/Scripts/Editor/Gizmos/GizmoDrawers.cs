/*
 * ---------------------------------------------------------------------------
 * Description: Implements PropertyDrawers for Gizmo attributes, providing
 *              validation, Scene View editing controls, and field rendering.
 *
 * Author: Lucas Gomes Cecchini
 * Pseudonym: AGAMENOM
 * ---------------------------------------------------------------------------
*/

using UnityEditor;
using UnityEngine;

namespace CustomAttributes.Editing
{
    #region === Base drawer ===

    /// <summary>
    /// Provides shared Inspector rendering and editing controls for Gizmo attributes.
    /// </summary>
    /// <remarks>
    /// Concrete drawers define how their linked properties are validated and
    /// which interactive tools are available. Keeping the Inspector layout here
    /// ensures that every Gizmo attribute follows the same interaction pattern.
    /// </remarks>
    public abstract class GizmoDrawerBase : PropertyDrawer
    {
        #region Layout constants

        // Keep the editing controls compact so they fit naturally beneath the associated field in the Inspector.
        private const float ButtonHeight = 20f;
        private const float ButtonWidth = 34f;
        private const float Gap = 4f;
        private const float Spacing = 2f;

        // Reserve two lines for validation messages so longer property names can be displayed without overlapping the next Inspector control.
        private static float HelpHeight => EditorGUIUtility.singleLineHeight * 2f;

        #endregion

        #region Validation and tool definitions

        /// <summary>
        /// Validates the serialized field and any properties required by its attribute.
        /// </summary>
        /// <param name="property">The field being drawn.</param>
        /// <returns>Null when the field configuration is valid; otherwise, an explanatory error message.</returns>
        protected abstract string Validate(SerializedProperty property);

        /// <summary>
        /// Creates the editing tools supported by this attribute.
        /// </summary>
        /// <param name="property">The field associated with the tools.</param>
        /// <returns>The buttons available for editing the field in the Scene View.</returns>
        protected abstract GizmoButton[] CreateButtons(SerializedProperty property);

        /// <summary>
        /// Checks whether this drawer can provide interactive Gizmo editing for the field.
        /// </summary>
        /// <param name="property">The serialized field being inspected.</param>
        /// <returns>True when the field is a Vector3 belonging to a Component.</returns>
        /// <remarks>
        /// The editing tools operate on component Transforms and Vector3 values.
        /// Restricting interactive editing to this combination prevents tools
        /// from receiving unsupported property types or non-component targets.
        /// </remarks>
        private static bool IsSupported(SerializedProperty property) => property.propertyType == SerializedPropertyType.Vector3 && property.serializedObject.targetObject is Component;

        #endregion

        #region Inspector layout

        /// <summary>
        /// Calculates the height required by the field, editing buttons, and validation message.
        /// </summary>
        /// <param name="property">The serialized field being drawn.</param>
        /// <param name="label">The field label.</param>
        /// <returns>The total height required by this drawer.</returns>
        /// <remarks>
        /// Unity asks for the property height before calling OnGUI. This method
        /// must account for the same controls that OnGUI will render; otherwise,
        /// the Inspector may overlap subsequent fields or leave unnecessary gaps.
        /// </remarks>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float field = EditorGUI.GetPropertyHeight(property, label, true);
            if (!IsSupported(property)) return field;
            return Validate(property) != null ? field + Spacing + HelpHeight : field + Spacing + ButtonHeight;
        }

        /// <summary>
        /// Draws the field and, when supported, its Scene View editing controls.
        /// </summary>
        /// <param name="position">The rectangle allocated by the Inspector.</param>
        /// <param name="property">The serialized field being drawn.</param>
        /// <param name="label">The field label.</param>
        /// <remarks>
        /// Validation runs before tool buttons are created. When linked properties
        /// are missing or have incompatible types, the drawer displays the field
        /// and an error message instead of offering controls that cannot work.
        /// </remarks>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!IsSupported(property))
            {
                // A non-Vector3 field cannot be used by these Gizmo tools.
                // Show a direct diagnostic rather than silently drawing an attribute whose expected editing behavior is unavailable.
                if (property.propertyType != SerializedPropertyType.Vector3)
                {
                    EditorGUI.LabelField(position, label.text, "Gizmo attributes require a Vector3 field.");
                }
                else
                {
                    // Preserve normal field rendering when the property type is valid but the target is not a Component.
                    EditorGUI.PropertyField(position, property, label, true);
                }
                return;
            }

            float fieldHeight = EditorGUI.GetPropertyHeight(property, label, true);
            string error = Validate(property);
            float y = position.y;

            if (error == null)
            {
                var buttons = CreateButtons(property);

                for (int i = 0; i < buttons.Length; i++)
                {
                    // Reflect the active session in the Inspector so the user can see which tool currently owns the Scene View interaction.
                    Rect rect = new(position.x + i * (ButtonWidth + Gap), y, ButtonWidth, ButtonHeight);
                    bool active = GizmoSession.IsActive(property, buttons[i].Id);

                    // GUI.Toggle returns the new state. Comparing it with the current state detects clicks without maintaining duplicate activation state inside the drawer.
                    if (GUI.Toggle(rect, active, buttons[i].Content, EditorStyles.miniButton) != active) GizmoSession.Toggle(property, buttons[i].Factory());
                }

                y += ButtonHeight + Spacing;
            }

            EditorGUI.PropertyField(new Rect(position.x, y, position.width, fieldHeight), property, label, true);

            if (error != null)
            {
                Rect help = new(position.x, y + fieldHeight + Spacing, position.width, HelpHeight);
                EditorGUI.HelpBox(help, error, MessageType.Error);
            }
        }

        #endregion
    }

    #endregion

    #region === Transform drawer ===

    /// <summary>
    /// Provides Inspector controls for editing a position and optional rotation with Scene View handles.
    /// </summary>
    [CustomPropertyDrawer(typeof(GizmoTransformAttribute))]
    public sealed class GizmoTransformDrawer : GizmoDrawerBase
    {
        /// <summary>
        /// Validates the optional rotation field referenced by the attribute.
        /// </summary>
        /// <param name="property">The position field associated with the attribute.</param>
        /// <returns>Null when valid, or a message describing the expected rotation type.</returns>
        protected override string Validate(SerializedProperty property)
        {
            var attr = (GizmoTransformAttribute)attribute;

            // Position editing does not require a separate rotation field.
            if (string.IsNullOrEmpty(attr.RotationPropertyName)) return null;
            var rotation = PropertyReflection.FindSibling(property, attr.RotationPropertyName);
            return TransformTool.IsValidRotationProperty(rotation) ? null : $"Rotation property '{attr.RotationPropertyName}' must be a Quaternion or Vector3 field.";
        }

        /// <summary>
        /// Creates the position tool and, when configured, the rotation tool.
        /// </summary>
        /// <param name="property">The serialized position field.</param>
        /// <returns>The tools available for the current attribute configuration.</returns>
        protected override GizmoButton[] CreateButtons(SerializedProperty property)
        {
            var attr = (GizmoTransformAttribute)attribute;
            var move = new GizmoButton(TransformTool.PositionId, GizmoIcons.Move, () => new TransformTool(attr, false));

            // Rotation editing is optional, so omit its button when no rotation property has been configured.
            if (string.IsNullOrEmpty(attr.RotationPropertyName)) return new[] { move };

            var rotate = new GizmoButton(TransformTool.RotationId, GizmoIcons.Rotate, () => new TransformTool(attr, true));
            return new[] { move, rotate };
        }
    }

    #endregion

    #region === Cube drawer ===

    /// <summary>
    /// Provides Inspector controls for resizing and repositioning a Gizmo cube.
    /// </summary>
    [CustomPropertyDrawer(typeof(GizmoCubeAttribute))]
    public sealed class GizmoCubeDrawer : GizmoDrawerBase
    {
        /// <summary>
        /// Ensures that the attribute references a valid Vector3 size field.
        /// </summary>
        /// <param name="property">The cube's serialized position field.</param>
        /// <returns>Null when the size field exists and has the required type; otherwise, an error message.</returns>
        protected override string Validate(SerializedProperty property)
        {
            var attr = (GizmoCubeAttribute)attribute;
            var size = PropertyReflection.FindSibling(property, attr.SizePropertyName);

            // Cube dimensions are three-dimensional, so a Vector3 is required for the size tool to edit all axes.
            return size != null && size.propertyType == SerializedPropertyType.Vector3 ? null : $"Linked Vector3 property '{attr.SizePropertyName}' was not found.";
        }

        /// <summary>
        /// Creates the cube resizing and movement tools.
        /// </summary>
        /// <param name="property">The cube's serialized position field.</param>
        /// <returns>The buttons available for editing the cube.</returns>
        protected override GizmoButton[] CreateButtons(SerializedProperty property)
        {
            var attr = (GizmoCubeAttribute)attribute;
            return new[]
            {
                new GizmoButton(CubeTool.SizeId, GizmoIcons.Edit, () => new CubeTool(attr, false)),
                new GizmoButton(CubeTool.MoveId, GizmoIcons.Move, () => new CubeTool(attr, true))
            };
        }
    }

    #endregion

    #region === Sphere drawer ===

    /// <summary>
    /// Provides Inspector controls for resizing and repositioning a Gizmo sphere.
    /// </summary>
    [CustomPropertyDrawer(typeof(GizmoSphereAttribute))]
    public sealed class GizmoSphereDrawer : GizmoDrawerBase
    {
        /// <summary>
        /// Ensures that the attribute references a valid floating-point radius field.
        /// </summary>
        /// <param name="property">The sphere's serialized position field.</param>
        /// <returns>Null when the radius field exists and has the required type; otherwise, an error message.</returns>
        protected override string Validate(SerializedProperty property)
        {
            var attr = (GizmoSphereAttribute)attribute;
            var radius = PropertyReflection.FindSibling(property, attr.RadiusPropertyName);

            // The sphere size tool edits a single scalar value rather than independent dimensions for each axis.
            return radius != null && radius.propertyType == SerializedPropertyType.Float ? null : $"Linked float property '{attr.RadiusPropertyName}' was not found.";
        }

        /// <summary>
        /// Creates the sphere resizing and movement tools.
        /// </summary>
        /// <param name="property">The sphere's serialized position field.</param>
        /// <returns>The buttons available for editing the sphere.</returns>
        protected override GizmoButton[] CreateButtons(SerializedProperty property)
        {
            var attr = (GizmoSphereAttribute)attribute;
            return new[]
            {
                new GizmoButton(SphereTool.SizeId, GizmoIcons.Edit, () => new SphereTool(attr, false)),
                new GizmoButton(SphereTool.MoveId, GizmoIcons.Move, () => new SphereTool(attr, true))
            };
        }
    }

    #endregion

    #region === Cone drawer ===

    /// <summary>
    /// Provides Inspector controls for editing a Gizmo cone's dimensions and position.
    /// </summary>
    [CustomPropertyDrawer(typeof(GizmoConeAttribute))]
    public sealed class GizmoConeDrawer : GizmoDrawerBase
    {
        /// <summary>
        /// Ensures that the attribute references valid floating-point angle and distance fields.
        /// </summary>
        /// <param name="property">The cone's serialized position field.</param>
        /// <returns>Null when both linked fields are valid; otherwise, an error message.</returns>
        protected override string Validate(SerializedProperty property)
        {
            var attr = (GizmoConeAttribute)attribute;
            var angle = PropertyReflection.FindSibling(property, attr.AnglePropertyName);
            var distance = PropertyReflection.FindSibling(property, attr.DistancePropertyName);

            // Both values are required to define the cone's geometry: angle determines its spread, and distance determines its extent.
            bool valid = angle != null && angle.propertyType == SerializedPropertyType.Float && distance != null && distance.propertyType == SerializedPropertyType.Float;
            return valid ? null : $"Linked float properties '{attr.AnglePropertyName}' and '{attr.DistancePropertyName}' are required.";
        }

        /// <summary>
        /// Creates the cone resizing and movement tools.
        /// </summary>
        /// <param name="property">The cone's serialized position field.</param>
        /// <returns>The buttons available for editing the cone.</returns>
        protected override GizmoButton[] CreateButtons(SerializedProperty property)
        {
            var attr = (GizmoConeAttribute)attribute;
            return new[]
            {
                new GizmoButton(ConeTool.SizeId, GizmoIcons.Edit, () => new ConeTool(attr, false)),
                new GizmoButton(ConeTool.MoveId, GizmoIcons.Move, () => new ConeTool(attr, true))
            };
        }
    }

    #endregion
}