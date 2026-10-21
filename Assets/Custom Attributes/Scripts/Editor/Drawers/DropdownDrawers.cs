/*
 * ---------------------------------------------------------------------------
 * Description: Provides custom dropdown drawers for tags, scenes, layers,
 *              and dynamically generated values in the Unity Inspector.
 *
 * Author: Lucas Gomes Cecchini
 * Pseudonym: AGAMENOM
 * ---------------------------------------------------------------------------
*/

using System.Collections.Generic;
using System.Globalization;
using UnityEditorInternal;
using System.Collections;
using UnityEditor;
using UnityEngine;
using System.IO;
using System;

namespace CustomAttributes.Editing
{
    #region === Shared ===

    /// <summary>
    /// Stores the values and prebuilt GUI labels used by string-based dropdowns.
    /// </summary>
    public sealed class ChoiceSet
    {
        #region Fields

        /// <summary>
        /// The original string values represented by the dropdown options.
        /// </summary>
        public readonly string[] Values;

        /// <summary>
        /// GUI labels corresponding directly to <see cref="Values"/>.
        /// </summary>
        public readonly GUIContent[] Contents;

        /// <summary>
        /// GUI labels with an additional first entry reserved for a placeholder.
        /// </summary>
        public readonly GUIContent[] PlaceholderContents;

        #endregion

        #region Initialization

        /// <summary>
        /// Creates the GUI representations for a collection of available values.
        /// </summary>
        /// <param name="values">The values that the dropdown should display.</param>
        public ChoiceSet(string[] values)
        {
            Values = values;
            Contents = new GUIContent[values.Length];
            PlaceholderContents = new GUIContent[values.Length + 1];
            PlaceholderContents[0] = new GUIContent(); // Index zero is reserved for a missing or invalid current value.

            for (int i = 0; i < values.Length; i++)
            {
                Contents[i] = new GUIContent(values[i]);
                PlaceholderContents[i + 1] = Contents[i];
            }
        }

        #endregion

        #region Comparison

        /// <summary>
        /// Determines whether another array contains the same values in the same order.
        /// </summary>
        /// <param name="other">The values to compare with this choice set.</param>
        /// <returns>True when both arrays have identical lengths and matching values at every index.</returns>
        public bool SameAs(string[] other)
        {
            if (other.Length != Values.Length) return false;
            for (int i = 0; i < other.Length; i++)
            {
                if (other[i] != Values[i]) return false;
            }
            return true;
        }

        #endregion
    }

    /// <summary>
    /// Provides shared Inspector rendering and validation for dropdowns
    /// whose serialized values are strings.
    /// </summary>
    public abstract class StringChoiceDrawer : PropertyDrawer
    {
        #region Configuration

        private const float Spacing = 2f;

        /// <summary>
        /// Gets the attribute name used in the message for unsupported field types.
        /// </summary>
        protected abstract string AttributeName { get; }

        /// <summary>
        /// Gets the current set of valid choices for this dropdown.
        /// </summary>
        protected abstract ChoiceSet GetChoices();

        /// <summary>
        /// Creates the placeholder displayed when the stored value is not a valid choice.
        /// </summary>
        /// <param name="value">The currently stored value.</param>
        protected abstract string GetPlaceholder(string value);

        /// <summary>
        /// Creates a diagnostic message for an invalid or missing value.
        /// </summary>
        /// <param name="value">The currently stored value.</param>
        /// <param name="type">The severity assigned to the diagnostic message.</param>
        protected abstract string GetProblem(string value, out MessageType type);

        /// <summary>
        /// Reserves two Inspector lines for the validation help box.
        /// </summary>
        private static float HelpHeight => EditorGUIUtility.singleLineHeight * 2f;

        #endregion

        #region Validation

        /// <summary>
        /// Checks whether a serialized string is absent from the available choices.
        /// </summary>
        /// <remarks>Mixed selections are not treated as invalid because the Inspector cannot represent several different stored values with a single validity result.</remarks>
        private bool IsInvalid(SerializedProperty property, ChoiceSet choices)
        {
            if (property.hasMultipleDifferentValues) return false;
            return Array.IndexOf(choices.Values, property.stringValue) < 0;
        }

        #endregion

        #region Layout Calculation

        /// <summary>
        /// Calculates the height required by the dropdown and any validation message.
        /// </summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (property.propertyType == SerializedPropertyType.String && IsInvalid(property, GetChoices())) height += Spacing + HelpHeight;
            return height;
        }

        #endregion

        #region Inspector Rendering

        /// <summary>
        /// Draws the dropdown and reports missing or invalid stored values.
        /// </summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // These drawers are specifically intended for serialized string fields.
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, $"Use [{AttributeName}] with string fields.");
                return;
            }

            var choices = GetChoices();
            string current = property.stringValue;
            bool mixed = property.hasMultipleDifferentValues;
            int index = mixed ? -1 : Array.IndexOf(choices.Values, current);
            bool invalid = !mixed && index < 0;

            GUIContent[] contents;
            int popupIndex;

            if (invalid)
            {
                // Preserve visibility of the stored value without pretending it is a valid option. The placeholder occupies index zero, so the real choices are shifted by one in this particular display array.
                choices.PlaceholderContents[0].text = GetPlaceholder(current);
                contents = choices.PlaceholderContents;
                popupIndex = 0;
            }
            else
            {
                // A mixed selection has no single current index. Use a safe index for the popup while showMixedValue communicates the mixed state.
                contents = choices.Contents;
                popupIndex = Mathf.Max(index, 0);
            }

            Rect line = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.showMixedValue = mixed;
            EditorGUI.BeginChangeCheck();

            // When a placeholder is present, subtract its offset to recover the index in the original Values array. Selecting the placeholder itself must not overwrite the serialized value.
            int picked = EditorGUI.Popup(line, label, popupIndex, contents);

            if (EditorGUI.EndChangeCheck())
            {
                int choice = invalid ? picked - 1 : picked;
                if (choice >= 0 && choice < choices.Values.Length) property.stringValue = choices.Values[choice];
            }

            // Restore the global Inspector flag so this drawer does not affect the appearance of controls rendered after it.
            EditorGUI.showMixedValue = false;

            if (invalid)
            {
                Rect help = new(position.x, line.yMax + Spacing, position.width, HelpHeight);
                EditorGUI.HelpBox(help, GetProblem(current, out MessageType type), type);
            }

            EditorGUI.EndProperty();
        }

        #endregion
    }

    #endregion

    #region === Tags ===

    /// <summary>
    /// Draws a dropdown populated with tags registered in the Unity Tag Manager.
    /// </summary>>
    [CustomPropertyDrawer(typeof(TagDropdownAttribute))]
    public sealed class TagDropdownDrawer : StringChoiceDrawer
    {
        #region Cache

        private static ChoiceSet cache;
        private static double nextRefresh;

        #endregion

        #region Choice Retrieval

        protected override string AttributeName => "TagDropdown";

        /// <summary>
        /// Retrieves the current tag list, refreshing the cache periodically.
        /// </summary>
        protected override ChoiceSet GetChoices()
        {
            if (cache == null || EditorApplication.timeSinceStartup >= nextRefresh)
            {
                // The time-based refresh avoids querying the internal tag list on every Inspector repaint while still allowing changes to appear.
                nextRefresh = EditorApplication.timeSinceStartup + 1.0;
                string[] tags = InternalEditorUtility.tags;

                // Reuse the existing GUIContent objects when the tag list has not changed, reducing unnecessary allocations during Inspector updates.
                if (cache == null || !cache.SameAs(tags)) cache = new ChoiceSet(tags);
            }
            return cache;
        }

        #endregion

        #region Validation Messages

        protected override string GetPlaceholder(string value) => string.IsNullOrEmpty(value) ? "Tag Missing" : $"Tag Missing ({value})";

        protected override string GetProblem(string value, out MessageType type)
        {
            type = MessageType.Warning;
            return string.IsNullOrEmpty(value) ? "No tag selected. Pick one from the dropdown." : $"Tag '{value}' does not exist in the Tag Manager.";
        }

        #endregion
    }

    #endregion

    #region === Scenes ===

    /// <summary>
    /// Draws a dropdown containing enabled scenes from the Build Settings.
    /// Disabled or missing scenes are reported without silently changing their values.
    /// </summary>
    [CustomPropertyDrawer(typeof(SceneTagDropdownAttribute))]
    public sealed class SceneTagDropdownDrawer : StringChoiceDrawer
    {
        #region Cache

        private static ChoiceSet enabledScenes;
        private static HashSet<string> allScenes;
        private static bool dirty = true;

        /// <summary>
        /// Registers a callback so changes to the Build Settings invalidate the cache.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void Init()
        {
            // Remove the callback first to avoid duplicate subscriptions when this initialization method runs again during the Editor's lifetime.
            EditorBuildSettings.sceneListChanged -= MarkDirty;
            EditorBuildSettings.sceneListChanged += MarkDirty;
        }

        /// <summary>
        /// Marks the cached scene data as outdated.
        /// </summary>
        private static void MarkDirty() => dirty = true;

        #endregion;

        #region Choice Retrieval

        protected override string AttributeName => "SceneTagDropdown";

        /// <summary>
        /// Builds the enabled-scene choices and a set containing all configured scenes.
        /// </summary>
        protected override ChoiceSet GetChoices()
        {
            if (!dirty && enabledScenes != null) return enabledScenes;

            var enabled = new List<string>();
            var all = new HashSet<string>();

            foreach (var scene in EditorBuildSettings.scenes)
            {
                // Scene names are derived from paths so the dropdown stores the scene name rather than its asset path.
                string name = Path.GetFileNameWithoutExtension(scene.path);
                all.Add(name);
                if (scene.enabled && !enabled.Contains(name)) enabled.Add(name);
            }

            // Keep the complete scene set separately so validation can distinguish a disabled scene from one that no longer exists in Build Settings.
            allScenes = all;
            enabledScenes = new ChoiceSet(enabled.ToArray());
            dirty = false;
            return enabledScenes;
        }

        #endregion

        #region Validation Messages

        /// <summary>
        /// Describes whether the stored scene name is empty, disabled, or missing.
        /// </summary>
        protected override string GetPlaceholder(string value)
        {
            GetChoices(); // Ensure the scene cache is current before consulting allScenes.
            if (string.IsNullOrEmpty(value)) return "No Scene";
            return allScenes.Contains(value) ? $"{value} [Disabled]" : $"Missing Scene ({value})";
        }

        /// <summary>
        /// Produces a warning for empty or disabled scenes and an error for missing scenes.
        /// </summary>
        protected override string GetProblem(string value, out MessageType type)
        {
            GetChoices();

            if (string.IsNullOrEmpty(value))
            {
                type = MessageType.Warning;
                return "No scene selected.";
            }

            if (allScenes.Contains(value))
            {
                type = MessageType.Warning;
                return $"Scene '{value}' is in the Build Settings but is disabled.";
            }

            type = MessageType.Error;
            return $"Scene '{value}' was not found in the Build Settings.";
        }

        #endregion
    }

    #endregion

    #region === Layers ===

    /// <summary>
    /// Draws a dropdown for a Unity layer represented by either its name or numeric index.
    /// </summary>
    [CustomPropertyDrawer(typeof(LayerDropdownAttribute))]
    public sealed class LayerDropdownDrawer : PropertyDrawer
    {
        /// <summary>
        /// Draws the layer selector and preserves values that do not match a current layer.
        /// </summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            bool isString = property.propertyType == SerializedPropertyType.String;
            bool isInt = property.propertyType == SerializedPropertyType.Integer;

            if (!isString && !isInt)
            {
                EditorGUI.LabelField(position, label.text, "Use [LayerDropdown] with int or string fields.");
                return;
            }

            string[] names = InternalEditorUtility.layers;
            int[] numbers = new int[names.Length];

            // Keep names and numeric layer indices aligned by array position so the selected popup entry can update either supported field type.
            for (int i = 0; i < names.Length; i++) numbers[i] = LayerMask.NameToLayer(names[i]);

            bool mixed = property.hasMultipleDifferentValues;
            int current = isString ? Array.IndexOf(names, property.stringValue) : Array.IndexOf(numbers, property.intValue);
            bool invalid = !mixed && current < 0;

            // An invalid stored layer receives a placeholder at index zero.
            // Valid layer entries are shifted only when that placeholder is needed.
            int offset = invalid ? 1 : 0;
            var contents = new GUIContent[names.Length + offset];
            if (invalid) contents[0] = new GUIContent(isString ? $"Layer Missing ({property.stringValue})" : $"Layer Missing ({property.intValue})");
            for (int i = 0; i < names.Length; i++)
            {
                contents[i + offset] = new GUIContent(isString ? names[i] : $"{numbers[i]}: {names[i]}");
            }

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.showMixedValue = mixed;
            EditorGUI.BeginChangeCheck();

            int picked = EditorGUI.Popup(position, label, invalid ? 0 : Mathf.Max(current, 0), contents);

            if (EditorGUI.EndChangeCheck())
            {
                // Convert the popup index back to the layer-array index. This also prevents choosing the missing-layer placeholder from changing data.
                int choice = picked - offset;
                if (choice >= 0 && choice < names.Length)
                {
                    if (isString) property.stringValue = names[choice];
                    else property.intValue = numbers[choice];
                }
            }

            EditorGUI.showMixedValue = false;
            EditorGUI.EndProperty();
        }
    }

    #endregion

    #region === Generic values from a member ===

    /// <summary>
    /// Draws a dropdown whose available options come from another field, property,
    /// or method returning an enumerable collection.
    /// </summary>
    [CustomPropertyDrawer(typeof(DropdownAttribute))]
    public sealed class DropdownDrawer : PropertyDrawer
    {
        #region Configuration

        private const float Spacing = 2f;

        /// <summary>
        /// Reserves two Inspector lines for validation or source-member errors.
        /// </summary>
        private static float HelpHeight => EditorGUIUtility.singleLineHeight * 2f;

        #endregion

        #region Choice Retrieval

        /// <summary>
        /// Resolves the configured member and copies its enumerable values into a list.
        /// </summary>
        /// <param name="property">The serialized property being drawn.</param>
        /// <param name="values">The available values when retrieval succeeds.</param>
        /// <param name="error">An explanatory error when the member cannot be used.</param>
        /// <returns>True when the configured member returns an enumerable collection.</returns>
        private bool TryGetValues(SerializedProperty property, out List<object> values, out string error)
        {
            values = new List<object>();
            error = null;

            var attr = (DropdownAttribute)attribute;
            object owner = PropertyReflection.GetOwner(property.serializedObject.targetObject, property.propertyPath);

            if (!PropertyReflection.TryGetMember(owner, attr.MemberName, out var source) || source is not IEnumerable enumerable)
            {
                error = $"Member '{attr.MemberName}' must be a field, property or method returning an IEnumerable.";
                return false;
            }

            // Materialize the enumerable so the drawer can search, count, and index the choices consistently while rendering the current Inspector frame.
            foreach (object item in enumerable) values.Add(item);
            return true;
        }

        #endregion

        #region Value Conversion and Comparison

        /// <summary>
        /// Determines whether the serialized field type is supported by this drawer.
        /// </summary>
        private static bool IsSupported(SerializedPropertyType type) => type == SerializedPropertyType.String || type == SerializedPropertyType.Integer || type == SerializedPropertyType.Float;

        /// <summary>
        /// Checks whether an available value matches the property's current value.
        /// </summary>
        /// <remarks>
        /// Conversion uses invariant culture so parsing and formatting do not depend on the operating system's decimal separator or regional number format.
        /// </remarks>
        private static bool Matches(SerializedProperty property, object value)
        {
            if (value == null) return false;

            try
            {
                switch (property.propertyType)
                {
                    case SerializedPropertyType.String: return property.stringValue == Convert.ToString(value, CultureInfo.InvariantCulture);
                    case SerializedPropertyType.Integer: return property.intValue == Convert.ToInt32(value, CultureInfo.InvariantCulture);
                    case SerializedPropertyType.Float: return Mathf.Approximately(property.floatValue, Convert.ToSingle(value, CultureInfo.InvariantCulture));
                }
            }
            catch (Exception)
            {
                // A value that cannot be converted simply does not match this property.
                // The dropdown can still display other valid options.
            }
            return false;
        }

        /// <summary>
        /// Converts an option to the serialized property's supported value type.
        /// </summary>
        private static void Apply(SerializedProperty property, object value)
        {
            try
            {
                switch (property.propertyType)
                {
                    case SerializedPropertyType.String: property.stringValue = Convert.ToString(value, CultureInfo.InvariantCulture); break;
                    case SerializedPropertyType.Integer: property.intValue = Convert.ToInt32(value, CultureInfo.InvariantCulture); break;
                    case SerializedPropertyType.Float: property.floatValue = Convert.ToSingle(value, CultureInfo.InvariantCulture); break;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Dropdown] Could not apply value '{value}': {e.Message}");
            }
        }

        #endregion

        #region Layout Calculation

        /// <summary>
        /// Reserves space for the dropdown and, when needed, its validation message.
        /// </summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            if (!IsSupported(property.propertyType)) return line;
            if (!TryGetValues(property, out List<object> values, out _)) return line + Spacing + HelpHeight;

            bool invalid = !property.hasMultipleDifferentValues && !values.Exists(v => Matches(property, v));
            return invalid ? line + Spacing + HelpHeight : line;
        }

        #endregion

        #region Inspector Rendering

        /// <summary>
        /// Draws the dropdown, applies a selected value, and reports invalid options.
        /// </summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!IsSupported(property.propertyType))
            {
                EditorGUI.LabelField(position, label.text, "Use [Dropdown] with string, int or float fields.");
                return;
            }

            Rect line = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            Rect help = new(position.x, line.yMax + Spacing, position.width, HelpHeight);

            if (!TryGetValues(property, out var values, out var error))
            {
                // Leave the serialized field editable even when the configured dropdown source is broken, and show the reason beneath it.
                EditorGUI.PropertyField(line, property, label);
                EditorGUI.HelpBox(help, error, MessageType.Error);
                return;
            }

            bool mixed = property.hasMultipleDifferentValues;
            int current = mixed ? -1 : values.FindIndex(v => Matches(property, v));
            bool invalid = !mixed && current < 0;

            int offset = invalid ? 1 : 0;
            var contents = new GUIContent[values.Count + offset];
            if (invalid) contents[0] = new GUIContent("Value Missing");
            for (int i = 0; i < values.Count; i++) contents[i + offset] = new GUIContent(Convert.ToString(values[i], CultureInfo.InvariantCulture));

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.showMixedValue = mixed;
            EditorGUI.BeginChangeCheck();

            int picked = EditorGUI.Popup(line, label, invalid ? 0 : Mathf.Max(current, 0), contents);

            if (EditorGUI.EndChangeCheck())
            {
                int choice = picked - offset;
                if (choice >= 0 && choice < values.Count) Apply(property, values[choice]);
            }

            EditorGUI.showMixedValue = false;

            if (invalid) EditorGUI.HelpBox(help, "The current value is not one of the available options.", MessageType.Warning);

            EditorGUI.EndProperty();
        }

        #endregion
    }

    #endregion
}