/*
 * ---------------------------------------------------------------------------
 * Description: Provides custom PropertyDrawers for object references,
 *              interface validation, missing-reference highlighting,
 *              and expandable object properties in the Unity Inspector.
 *
 * Author: Lucas Gomes Cecchini
 * Pseudonym: AGAMENOM
 * ---------------------------------------------------------------------------
*/

using UnityEditor;
using UnityEngine;
using System;

using Object = UnityEngine.Object;

namespace CustomAttributes.Editing
{
    #region === HighlightEmptyReference ===

    /// <summary>
    /// Highlights empty object references and distinguishes unassigned references
    /// from references whose original objects are missing.
    /// </summary>
    [CustomPropertyDrawer(typeof(HighlightEmptyReferenceAttribute))]
    public sealed class HighlightEmptyReferenceDrawer : PropertyDrawer
    {
        #region Layout Configuration

        private const float Spacing = 2f;

        /// <summary>
        /// Reserves two Inspector lines for the reference diagnostic message.
        /// </summary>
        private static float HelpHeight => EditorGUIUtility.singleLineHeight * 2f;

        #endregion

        #region Reference Validation

        /// <summary>
        /// Determines whether the property is an empty object reference and identifies
        /// whether Unity still retains an entity identifier for the missing object.
        /// </summary>
        /// <param name="property">The serialized property being inspected.</param>
        /// <param name="missing">True when the reference is empty but its entity identifier indicates that an object previously existed.</param>
        /// <returns>True when the property is an object reference with no current object and is not part of a mixed multi-object selection.</returns>
        private static bool IsEmpty(SerializedProperty property, out bool missing)
        {
            missing = false;

            // Only object references can use this drawer's empty-reference checks.
            if (property.propertyType != SerializedPropertyType.ObjectReference) return false;

            // A multi-object selection may contain different reference values.
            // Treating that state as empty could incorrectly highlight a valid reference.
            if (property.hasMultipleDifferentValues) return false;
            if (property.objectReferenceValue != null) return false;

            // A default EntityId indicates that no missing entity is being tracked.
            // A non-default identifier allows the drawer to distinguish a deleted or destroyed reference from a field that was never assigned.
            missing = !property.objectReferenceEntityIdValue.Equals(default(EntityId));
            return true;
        }

        /// <summary>
        /// Gets the declared type of the reference field, including collection elements.
        /// </summary>
        private string GetTypeName()
        {
            var type = fieldInfo?.FieldType;
            var element = PropertyReflection.GetElementType(type);

            // For arrays and supported collections, the expected reference type is the element type rather than the collection type itself.
            if (element != null) type = element;
            return type != null ? type.Name : "Object";
        }

        #endregion

        #region Layout Calculation

        /// <summary>
        /// Adds space for a diagnostic message only when the reference is empty.
        /// </summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            return IsEmpty(property, out _) ? line + Spacing + HelpHeight : line;
        }

        #endregion

        #region Inspector Rendering

        /// <summary>
        /// Draws the reference field and highlights unassigned or missing references.
        /// </summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                EditorGUI.LabelField(position, label.text, "Use [HighlightEmptyReference] with object references.");
                return;
            }

            bool empty = IsEmpty(property, out bool missing);
            Rect line = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            // Save the global GUI color before highlighting the field. Restoring it prevents the red background from affecting subsequent Inspector controls.
            Color previous = GUI.backgroundColor;
            if (empty) GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);

            EditorGUI.PropertyField(line, property, label);
            GUI.backgroundColor = previous;

            if (empty)
            {
                Rect help = new(position.x, line.yMax + Spacing, position.width, HelpHeight);
                string message = missing ? "The referenced object is missing (it was deleted or destroyed)." : $"Assign an object of type '{GetTypeName()}' here!";
                EditorGUI.HelpBox(help, message, MessageType.Error);
            }
        }

        #endregion
    }

    #endregion

    #region === RequireInterface ===

    /// <summary>
    /// Restricts object-reference assignments to objects that implement a specified
    /// interface, resolving GameObjects or Components to a compatible component.
    /// </summary>
    [CustomPropertyDrawer(typeof(RequireInterfaceAttribute))]
    public sealed class RequireInterfaceDrawer : PropertyDrawer
    {
        #region Layout Configuration

        private const float Spacing = 2f;

        /// <summary>
        /// Reserves two Inspector lines for interface-validation feedback.
        /// </summary>
        private static float HelpHeight => EditorGUIUtility.singleLineHeight * 2f;

        #endregion

        #region Type Resolution

        /// <summary>
        /// Gets the object type accepted by the serialized field.
        /// </summary>
        private Type ObjectType()
        {
            var type = fieldInfo != null ? (PropertyReflection.GetElementType(fieldInfo.FieldType) ?? fieldInfo.FieldType) : null;
            return type != null && typeof(Object).IsAssignableFrom(type) ? type : typeof(Object);
        }

        /// <summary>
        /// Determines whether the current non-null reference violates the interface requirement.
        /// </summary>
        private bool IsInvalid(SerializedProperty property)
        {
            var attr = (RequireInterfaceAttribute)attribute;

            // Different selected objects may have different reference values.
            // Do not report a single invalid result for a mixed selection.
            if (property.hasMultipleDifferentValues) return false;
            var value = property.objectReferenceValue;

            return value != null && attr.InterfaceType != null && !attr.InterfaceType.IsInstanceOfType(value);
        }

        /// <summary>
        /// Resolves a selected object to one that implements the required interface.
        /// </summary>
        /// <param name="picked">The object selected in the Inspector.</param>
        /// <param name="interfaceType">The required interface type.</param>
        /// <returns>The selected object or a compatible component, or null when no compatible object can be found.</returns>
        private static Object Resolve(Object picked, Type interfaceType)
        {
            if (picked == null) return null;

            // An object that already implements the interface can be assigned directly.
            if (interfaceType.IsInstanceOfType(picked)) return picked;

            // A GameObject can satisfy an interface requirement through one of its attached components. The same applies when the selected object is already a Component.
            Component found = null;
            if (picked is GameObject go) found = go.GetComponent(interfaceType);
            else if (picked is Component component) found = component.GetComponent(interfaceType);

            if (found == null) Debug.LogWarning($"'{picked.name}' does not implement {interfaceType.Name}.");
            return found;
        }

        #endregion

        #region Layout Calculation

        /// <summary>
        /// Reserves extra space when the current reference fails interface validation.
        /// </summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            return property.propertyType == SerializedPropertyType.ObjectReference && IsInvalid(property) ? line + Spacing + HelpHeight : line;
        }

        #endregion

        #region Inspector Rendering

        /// <summary>
        /// Draws the object picker, resolves compatible components, and reports invalid references.
        /// </summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attr = (RequireInterfaceAttribute)attribute;

            if (property.propertyType != SerializedPropertyType.ObjectReference || attr.InterfaceType == null)
            {
                EditorGUI.LabelField(position, label.text, "Use [RequireInterface(typeof(IFoo))] with object references.");
                return;
            }

            Rect line = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            // Preserve Unity's mixed-value presentation for multi-object editing.
            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();

            var picked = EditorGUI.ObjectField(line, label, property.objectReferenceValue, ObjectType(), true);

            // Resolve the selection before storing it. Selecting a GameObject can therefore assign its implementing component rather than the GameObject itself.
            if (EditorGUI.EndChangeCheck()) property.objectReferenceValue = Resolve(picked, attr.InterfaceType);

            // Reset the shared EditorGUI state so the mixed-value indicator does not leak into controls drawn after this property.
            EditorGUI.showMixedValue = false;

            if (IsInvalid(property))
            {
                Rect help = new(position.x, line.yMax + Spacing, position.width, HelpHeight);
                EditorGUI.HelpBox(help, $"The assigned object does not implement {attr.InterfaceType.Name}.", MessageType.Error);
            }

            EditorGUI.EndProperty();
        }

        #endregion
    }

    #endregion

    #region === Expandable ===

    /// <summary>
    /// Draws an object reference with a foldout that exposes the referenced object's
    /// serialized properties directly inside the current Inspector.
    /// </summary>
    [CustomPropertyDrawer(typeof(ExpandableAttribute))]
    public sealed class ExpandableDrawer : PropertyDrawer
    {
        #region Layout Configuration

        private const float Padding = 4f;

        #endregion

        #region Expansion Validation

        /// <summary>
        /// Determines whether the current property can display expanded object contents.
        /// </summary>
        private static bool CanExpand(SerializedProperty property) => property.propertyType == SerializedPropertyType.ObjectReference && !property.hasMultipleDifferentValues && property.objectReferenceValue != null;

        #endregion

        #region Child Layout Calculation

        /// <summary>
        /// Calculates the total height of the referenced object's visible properties.
        /// </summary>
        /// <param name="so">The serialized object whose properties are being measured.</param>
        private static float ChildrenHeight(SerializedObject so)
        {
            float height = 0f;
            var it = so.GetIterator();

            // NextVisible traverses the serialized object's visible Inspector fields.
            // The first call enters the root; subsequent calls walk the remaining visible properties without recursively entering every child.
            for (bool enter = true; it.NextVisible(enter); enter = false)
            {
                if (it.propertyPath == "m_Script") continue;
                height += EditorGUI.GetPropertyHeight(it, true) + EditorGUIUtility.standardVerticalSpacing;
            }
            return height;
        }

        #endregion

        #region Layout Calculation

        /// <summary>
        /// Calculates the collapsed line height or the full expanded object height.
        /// </summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;

            if (CanExpand(property) && property.isExpanded)
            {
                // Use a separate SerializedObject so the referenced object's fields can be measured independently from the object holding this reference.
                using var so = new SerializedObject(property.objectReferenceValue);
                height += EditorGUIUtility.standardVerticalSpacing + ChildrenHeight(so) + Padding * 2f;
            }
            return height;
        }

        #endregion

        #region Inspector Rendering

        /// <summary>
        /// Draws the reference field and, when expanded, its referenced object's properties.
        /// </summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                EditorGUI.LabelField(position, label.text, "Use [Expandable] with object references.");
                return;
            }

            Rect line = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            // When the reference is empty or ambiguous, retain the standard property field rather than showing a foldout that cannot display meaningful content.
            if (!CanExpand(property))
            {
                EditorGUI.PropertyField(line, property, label);
                return;
            }

            // Separate the foldout label from the object field so the foldout controls expansion while the object field continues to control the reference itself.
            Rect foldout = new(line.x, line.y, EditorGUIUtility.labelWidth, line.height);
            property.isExpanded = EditorGUI.Foldout(foldout, property.isExpanded, label, true);

            // The field occupies the label's former space, so temporarily remove the indentation to avoid adding the normal Inspector indent a second time.
            Rect field = new(line.x + EditorGUIUtility.labelWidth, line.y, line.width - EditorGUIUtility.labelWidth, line.height);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            EditorGUI.PropertyField(field, property, GUIContent.none);
            EditorGUI.indentLevel = indent;

            if (!property.isExpanded || property.objectReferenceValue == null) return;

            using var so = new SerializedObject(property.objectReferenceValue);
            so.Update();

            float y = line.yMax + EditorGUIUtility.standardVerticalSpacing;
            float boxHeight = ChildrenHeight(so) + Padding * 2f;

            // The help-box style visually groups the nested properties and separates them from the fields belonging to the object that holds this reference.
            GUI.Box(new Rect(position.x, y, position.width, boxHeight), GUIContent.none, EditorStyles.helpBox);

            y += Padding;
            EditorGUI.indentLevel++;

            var it = so.GetIterator();
            for (bool enter = true; it.NextVisible(enter); enter = false)
            {
                if (it.propertyPath == "m_Script") continue;
                float h = EditorGUI.GetPropertyHeight(it, true);
                EditorGUI.PropertyField(new Rect(position.x + 2f, y, position.width - 4f, h), it, true);
                y += h + EditorGUIUtility.standardVerticalSpacing;
            }

            EditorGUI.indentLevel--;

            // Apply edits made to the referenced object. Without this call, changes
            // made through the nested fields would not be committed through this SerializedObject's editing workflow.
            so.ApplyModifiedProperties();
        }

        #endregion
    }

    #endregion
}