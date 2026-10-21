/*
 * ---------------------------------------------------------------------------
 * Description: Provides custom PropertyDrawers for relabeling fields,
 *              displaying suffixes, editing minimum and maximum values,
 *              and generating unique identifiers in the Unity Inspector.
 *
 * Author: Lucas Gomes Cecchini
 * Pseudonym: AGAMENOM
 * ---------------------------------------------------------------------------
*/

using UnityEditor;
using UnityEngine;

namespace CustomAttributes.Editing
{
    #region === Label ===

    /// <summary>
    /// Replaces a property's displayed label while preserving its tooltip and value.
    /// </summary>
    [CustomPropertyDrawer(typeof(LabelAttribute))]
    public sealed class LabelDrawer : PropertyDrawer
    {
        #region Inspector Rendering

        /// <summary>
        /// Draws the property using the custom label defined by the attribute.
        /// </summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Only the displayed text changes. Preserving the original tooltip keeps the property's contextual help available under the replacement label.
            var attr = (LabelAttribute)attribute;
            EditorGUI.PropertyField(position, property, new GUIContent(attr.Text, label.tooltip), true);
        }

        #endregion

        #region Layout Calculation

        /// <summary>
        /// Uses Unity's standard height calculation so nested properties remain visible.
        /// </summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUI.GetPropertyHeight(property, label, true);

        #endregion
    }

    #endregion

    #region === Suffix ===

    /// <summary>
    /// Displays a subtle text suffix alongside a property without changing its value.
    /// </summary>
    [CustomPropertyDrawer(typeof(SuffixAttribute))]
    public sealed class SuffixDrawer : PropertyDrawer
    {
        #region Inspector Rendering

        /// <summary>
        /// Draws the property and overlays the configured suffix at the right edge.
        /// </summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.PropertyField(position, property, label, true);

            // Generic properties may span multiple lines or contain nested controls.
            // Avoid placing a single-line suffix over that more complex layout.
            if (property.propertyType == SerializedPropertyType.Generic) return;

            var attr = (SuffixAttribute)attribute;
            var content = new GUIContent(attr.Suffix);
            var style = EditorStyles.miniLabel;

            // Calculate the suffix width from its rendered text so different suffixes occupy only the space they need instead of using a fixed-width region.
            float width = style.CalcSize(content).x + 4f;
            Rect suffix = new(position.xMax - width - 2f, position.y, width, EditorGUIUtility.singleLineHeight);

            // Lowering the content alpha keeps the suffix visually secondary to the property's actual value and label.
            Color previous = GUI.contentColor;
            GUI.contentColor = new Color(1f, 1f, 1f, 0.55f);
            GUI.Label(suffix, content, style);

            // GUI.contentColor is shared drawing state; restore it to avoid tinting unrelated controls rendered after this drawer.
            GUI.contentColor = previous;
        }

        #endregion

        #region Layout Calculation

        /// <summary>
        /// Delegates height calculation to Unity to support expanded properties.
        /// </summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUI.GetPropertyHeight(property, label, true);

        #endregion
    }

    #endregion

    #region === MinMax Slider ===

    /// <summary>
    /// Draws a slider with separate minimum and maximum fields for Vector2 or Vector2Int values.
    /// </summary>
    [CustomPropertyDrawer(typeof(MinMaxSliderAttribute))]
    public sealed class MinMaxSliderDrawer : PropertyDrawer
    {
        #region Layout Configuration

        /// <summary>
        /// Fixed width reserved for each numeric endpoint field.
        /// </summary>
        private const float FieldWidth = 46f;

        /// <summary>
        /// Horizontal spacing between the endpoint fields and the slider.
        /// </summary>
        private const float Gap = 4f;

        #endregion

        #region Inspector Rendering

        /// <summary>
        /// Draws the minimum field, slider, and maximum field, then stores the updated range.
        /// </summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attr = (MinMaxSliderAttribute)attribute;
            bool isInt = property.propertyType == SerializedPropertyType.Vector2Int;

            // The range is represented by the X and Y components of a vector.
            // Reject other types instead of silently converting or overwriting their data.
            if (property.propertyType != SerializedPropertyType.Vector2 && !isInt)
            {
                EditorGUI.LabelField(position, label.text, "Use [MinMaxSlider] with Vector2 or Vector2Int.");
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            // PrefixLabel reserves the normal Inspector label area, leaving the remaining rectangle for the two numeric fields and the slider.
            Rect content = EditorGUI.PrefixLabel(position, label);
            int indent = EditorGUI.indentLevel;

            // The custom layout uses explicit coordinates, so disable indentation while drawing its internal controls to prevent a second horizontal offset.
            EditorGUI.indentLevel = 0;

            Rect minRect = new(content.x, content.y, FieldWidth, content.height);
            Rect maxRect = new(content.xMax - FieldWidth, content.y, FieldWidth, content.height);
            Rect sliderRect = new(minRect.xMax + Gap, content.y, maxRect.x - minRect.xMax - Gap * 2f, content.height);

            // Vector2 and Vector2Int share the same range-editing UI. Converting integer endpoints to floats allows both types to use Unity's slider.
            Vector2 value = isInt ? (Vector2)property.vector2IntValue : property.vector2Value;
            float min = value.x;
            float max = value.y;

            EditorGUI.BeginChangeCheck();

            min = EditorGUI.FloatField(minRect, min);
            max = EditorGUI.FloatField(maxRect, max);
            EditorGUI.MinMaxSlider(sliderRect, ref min, ref max, attr.Min, attr.Max);

            if (EditorGUI.EndChangeCheck())
            {
                // Clamp the lower endpoint first, then constrain the upper endpoint against it. This maintains min <= max while respecting the bounds.
                min = Mathf.Clamp(min, attr.Min, max);
                max = Mathf.Clamp(max, min, attr.Max);

                // Integer vectors cannot store fractional endpoints, so round only when committing the edited values.
                if (isInt) property.vector2IntValue = new Vector2Int(Mathf.RoundToInt(min), Mathf.RoundToInt(max));
                else property.vector2Value = new Vector2(min, max);
            }

            // Restore the Inspector indentation before ending the property scope.
            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }

        #endregion
    }

    #endregion

    #region === Unique ID ===

    /// <summary>
    /// Automatically generates a unique identifier for a string property and
    /// provides a button for explicitly generating a replacement.
    /// </summary>
    [CustomPropertyDrawer(typeof(UniqueIdAttribute))]
    public sealed class UniqueIdDrawer : PropertyDrawer
    {
        #region Layout Configuration

        /// <summary>
        /// Width reserved for the button that generates a replacement identifier.
        /// </summary>
        private const float ButtonWidth = 48f;

        #endregion

        #region Inspector Rendering

        /// <summary>
        /// Displays the identifier as read-only and provides a button to regenerate it.
        /// </summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // GUID generation is intended for string properties. Reporting an incompatible field type avoids accidental data changes.
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "Use [UniqueId] with string fields.");
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            // Initialize empty IDs automatically, but do not assign a single new value when editing several objects with different current values.
            if (string.IsNullOrEmpty(property.stringValue) && !property.hasMultipleDifferentValues) property.stringValue = System.Guid.NewGuid().ToString("N");

            // Reserve space for the action button while allowing the text field to use the remaining width.
            Rect field = new(position.x, position.y, position.width - ButtonWidth - 4f, position.height);
            Rect button = new(position.xMax - ButtonWidth, position.y, ButtonWidth, position.height);

            // The identifier is visible for inspection and copying, but normal typing is disabled so users change it through the explicit regeneration action.
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.TextField(field, label, property.stringValue);
            }
            EditorGUI.showMixedValue = false; // Restore the standard mixed-value display before drawing the button.

            // Regeneration is explicit: an existing identifier remains stable until the user requests a new one.
            if (GUI.Button(button, "New")) property.stringValue = System.Guid.NewGuid().ToString("N");

            EditorGUI.EndProperty();
        }

        #endregion
    }

    #endregion
}