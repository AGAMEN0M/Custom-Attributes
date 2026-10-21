/*
 * ---------------------------------------------------------------------------
 * Description: Defines metadata attributes for customizing field visibility,
 *              editability, validation, grouping, value-change callbacks, automatic component
 *              references, and drag-and-drop list filling in the Unity Inspector.
 *
 * Using:
 * Apply these attributes to fields or properties supported by the package's
 * custom Inspector implementation.
 *
 * Conditional Attributes:
 * [ShowIf(nameof(isEnabled))] displays a field when its condition is met.
 * [HideIf(nameof(isEnabled))] hides a field when its condition is met.
 * [EnableIf(nameof(isEnabled))] enables a field when its condition is met.
 * [DisableIf(nameof(isEnabled))] disables a field when its condition is met.
 * Use a comparison value to compare against a specific value:
 * [ShowIf(nameof(count), 5)].
 * Use ConditionLogic.And or ConditionLogic.Or to combine conditions:
 * [ShowIf(ConditionLogic.And, nameof(isEnabled), nameof(isReady))].
 *
 * ReadOnly:
 * Use [ReadOnly] to prevent editing in the Inspector.
 * Configure ReadOnlyMode.Always, WhenPlaying, or WhenEditing to select
 * when the field should be read-only.
 *
 * Required:
 * Use [Required] to mark a field as required.
 * Supply a custom message with [Required("Reference is required.")].
 *
 * OnValueChanged:
 * Use [OnValueChanged(nameof(UpdateValues))] to identify the method
 * associated with a field's value-change event.
 *
 * GUIColor:
 * Use [GUIColor(1f, 0f, 0f)] to specify a color using RGBA components.
 * Use [GUIColor("#FF0000FF")] to specify a hexadecimal color.
 *
 * FoldoutGroup:
 * Use [FoldoutGroup("Settings")] to group fields under a foldout.
 * Set DefaultExpanded to control its initial expanded state.
 *
 * ShowInInspector:
 * Use [ShowInInspector] to expose a supported field or property in the
 * Inspector. Supply a custom label or set OnlyInPlayMode to restrict display.
 *
 * Automatic References:
 * Use [GetComponent] to identify a component on the same GameObject.
 * Use [GetInChildren] to identify a component in child objects.
 * Use [GetInParent] to identify a component in parent objects.
 * Set IncludeInactive to control whether inactive objects are included
 * by the corresponding reference-resolution implementation.
 *
 * DragAndDrop:
 * Use [DragAndDrop] on an array or List of UnityEngine.Object types to display
 * a drop area above it. Dropped objects are added to the list: direct matches,
 * components of dropped GameObjects, sub-assets of dropped assets (such as Sprites
 * inside a Texture), and the matching assets inside dropped folders.
 * Supply a custom area text with [DragAndDrop("Drop fonts here")].
 * Set Height, AllowDuplicates, or AcceptFolders to adjust the behavior.
 * Works on top-level fields and inside plain [Serializable] classes.
 *
 * Example:
 * [ShowIf(nameof(isEnabled))]
 * [FoldoutGroup("Movement")]
 * [Required("A target is required.")]
 * public Transform target;
 *
 * [ReadOnly(ReadOnlyMode.WhenPlaying)]
 * public float currentSpeed;
 *
 * [OnValueChanged(nameof(UpdateMovement))]
 * public float movementSpeed;
 *
 * private void UpdateMovement()
 * {
 *     // Update dependent values when invoked by the editor implementation.
 * }
 *
 * Note:
 * These attributes describe metadata. Their actual Inspector behavior is
 * implemented by the associated editor scripts.
 *
 * Author: Lucas Gomes Cecchini
 * Pseudonym: AGAMENOM
 * ---------------------------------------------------------------------------
*/

using UnityEngine;
using System;

namespace CustomAttributes
{
    #region Base Attribute

    /// <summary>
    /// Base class for metadata attributes that can be applied to fields
    /// and properties, including support for multiple attributes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
    public abstract class MetaAttribute : Attribute { }

    #endregion

    #region Conditional Attributes

    /// <summary>
    /// Determines how multiple conditions are combined.
    /// </summary>
    public enum ConditionLogic
    {
        /// <summary>All specified conditions must be satisfied.</summary>
        And,

        /// <summary>At least one specified condition must be satisfied.</summary>
        Or
    }

    /// <summary>
    /// Identifies the Inspector behavior controlled by a condition.
    /// </summary>
    public enum ConditionEffect
    {
        /// <summary>Controls whether a field is shown.</summary>
        Show,

        /// <summary>Controls whether a field is hidden.</summary>
        Hide,

        /// <summary>Controls whether a field is enabled.</summary>
        Enable,

        /// <summary>Controls whether a field is disabled.</summary>
        Disable
    }

    /// <summary>
    /// Stores the condition information shared by conditional attributes.
    /// The editor implementation evaluates these settings.
    /// </summary>
    public abstract class ConditionAttribute : MetaAttribute
    {
        /// <summary>
        /// Gets the names of the members used as conditions.
        /// </summary>
        public string[] Conditions { get; }

        /// <summary>
        /// Gets the logic used to combine multiple conditions.
        /// </summary>
        public ConditionLogic Logic { get; }

        /// <summary>
        /// Gets the optional value against which a condition is compared.
        /// </summary>
        public object CompareValue { get; }

        /// <summary>
        /// Indicates whether an explicit comparison value was supplied.
        /// </summary>
        public bool HasCompareValue { get; }

        /// <summary>
        /// Gets the effect applied when the conditions are satisfied.
        /// </summary>
        public abstract ConditionEffect Effect { get; }

        /// <summary>
        /// Initializes a condition that references one member.
        /// </summary>
        /// <param name="condition">The condition member name.</param>
        protected ConditionAttribute(string condition)
        {
            Conditions = new[] { condition };
            Logic = ConditionLogic.And;
        }

        /// <summary>
        /// Initializes a condition with an explicit comparison value.
        /// </summary>
        /// <param name="condition">The condition member name.</param>
        /// <param name="compareValue">The value to compare against.</param>
        protected ConditionAttribute(string condition, object compareValue)
        {
            Conditions = new[] { condition };
            Logic = ConditionLogic.And;
            CompareValue = compareValue;
            HasCompareValue = true;
        }

        /// <summary>
        /// Initializes a condition using multiple member names and a
        /// combination rule.
        /// </summary>
        /// <param name="logic">The rule used to combine conditions.</param>
        /// <param name="conditions">The condition member names.</param>
        protected ConditionAttribute(ConditionLogic logic, string[] conditions)
        {
            // An empty array avoids a null collection when no conditions
            // are supplied. The evaluator determines how that case behaves.
            Conditions = conditions ?? Array.Empty<string>();
            Logic = logic;
        }
    }

    /// <summary>
    /// Displays a field when its condition evaluates as satisfied.
    /// </summary>
    public class ShowIfAttribute : ConditionAttribute
    {
        public override ConditionEffect Effect => ConditionEffect.Show;

        public ShowIfAttribute(string condition) : base(condition) { }

        public ShowIfAttribute(string condition, object compareValue) : base(condition, compareValue) { }

        public ShowIfAttribute(ConditionLogic logic, params string[] conditions) : base(logic, conditions) { }
    }

    /// <summary>
    /// Hides a field when its condition evaluates as satisfied.
    /// </summary>
    public class HideIfAttribute : ConditionAttribute
    {
        public override ConditionEffect Effect => ConditionEffect.Hide;

        public HideIfAttribute(string condition) : base(condition) { }

        public HideIfAttribute(string condition, object compareValue) : base(condition, compareValue) { }

        public HideIfAttribute(ConditionLogic logic, params string[] conditions) : base(logic, conditions) { }
    }

    /// <summary>
    /// Enables a field when its condition evaluates as satisfied.
    /// </summary>
    public class EnableIfAttribute : ConditionAttribute
    {
        public override ConditionEffect Effect => ConditionEffect.Enable;

        public EnableIfAttribute(string condition) : base(condition) { }

        public EnableIfAttribute(string condition, object compareValue) : base(condition, compareValue) { }

        public EnableIfAttribute(ConditionLogic logic, params string[] conditions) : base(logic, conditions) { }
    }

    /// <summary>
    /// Disables a field when its condition evaluates as satisfied.
    /// </summary>
    public class DisableIfAttribute : ConditionAttribute
    {
        public override ConditionEffect Effect => ConditionEffect.Disable;

        public DisableIfAttribute(string condition) : base(condition) { }

        public DisableIfAttribute(string condition, object compareValue) : base(condition, compareValue) { }

        public DisableIfAttribute(ConditionLogic logic, params string[] conditions) : base(logic, conditions) { }
    }

    #endregion

    #region Read-Only Attribute

    /// <summary>
    /// Defines when a field should be displayed as read-only.
    /// </summary>
    public enum ReadOnlyMode
    {
        /// <summary>The field is always read-only.</summary>
        Always,

        /// <summary>The field is read-only during Play Mode.</summary>
        WhenPlaying,

        /// <summary>The field is read-only outside Play Mode.</summary>
        WhenEditing
    }

    /// <summary>
    /// Marks a field or property as read-only according to a selected mode.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class ReadOnlyAttribute : MetaAttribute
    {
        /// <summary>Gets the read-only mode.</summary>
        public ReadOnlyMode Mode { get; }

        /// <summary>
        /// Initializes the attribute with the selected read-only mode.
        /// </summary>
        /// <param name="mode">
        /// The mode to apply. Defaults to <see cref="ReadOnlyMode.Always"/>.
        /// </param>
        public ReadOnlyAttribute(ReadOnlyMode mode = ReadOnlyMode.Always) => Mode = mode;
    }

    #endregion

    #region Required Attribute

    /// <summary>
    /// Marks a field or property as requiring a valid value.
    /// The editor implementation determines how missing values are handled.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class RequiredAttribute : MetaAttribute
    {
        /// <summary>Gets the optional validation message.</summary>
        public string Message { get; }

        /// <summary>
        /// Initializes the required-value validation metadata.
        /// </summary>
        /// <param name="message">An optional custom validation message.</param>
        public RequiredAttribute(string message = null) => Message = message;
    }

    #endregion

    #region Value Changed Attribute

    /// <summary>
    /// Identifies a method to be invoked when the associated value changes,
    /// according to the editor implementation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public class OnValueChangedAttribute : MetaAttribute
    {
        /// <summary>Gets the name of the callback method.</summary>
        public string MethodName { get; }

        /// <summary>
        /// Initializes the callback method reference.
        /// </summary>
        /// <param name="methodName">The callback method name.</param>
        public OnValueChangedAttribute(string methodName) => MethodName = methodName;
    }

    #endregion

    #region GUI Color Attribute

    /// <summary>
    /// Defines the GUI color associated with a field or property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class GUIColorAttribute : MetaAttribute
    {
        /// <summary>Gets the configured GUI color.</summary>
        public Color Color { get; }

        /// <summary>
        /// Initializes the color using RGBA components.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <param name="a">The alpha component, defaulting to 1.</param>
        public GUIColorAttribute(float r, float g, float b, float a = 1f) => Color = new Color(r, g, b, a);

        /// <summary>
        /// Initializes the color from a hexadecimal HTML color string.
        /// Invalid values fall back to white.
        /// </summary>
        /// <param name="hex">The hexadecimal color string.</param>
        public GUIColorAttribute(string hex) => Color = ColorUtility.TryParseHtmlString(hex, out var parsed) ? parsed : Color.white;
    }

    #endregion

    #region Foldout Group Attribute

    /// <summary>
    /// Assigns a field or property to a named Inspector foldout group.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class FoldoutGroupAttribute : MetaAttribute
    {
        /// <summary>Gets the name used to identify the group.</summary>
        public string GroupName { get; }

        /// <summary>
        /// Gets or sets whether the group starts expanded.
        /// Defaults to true.
        /// </summary>
        public bool DefaultExpanded { get; set; } = true;

        /// <summary>
        /// Initializes the foldout group name.
        /// </summary>
        /// <param name="groupName">The group identifier.</param>
        public FoldoutGroupAttribute(string groupName) => GroupName = groupName;
    }

    #endregion

    #region Show In Inspector Attribute

    /// <summary>
    /// Marks a field or property for display in the Inspector, including
    /// members that the associated editor implementation supports displaying.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class ShowInInspectorAttribute : MetaAttribute
    {
        /// <summary>Gets the optional custom label.</summary>
        public string Label { get; }

        /// <summary>
        /// Gets or sets whether display is restricted to Play Mode.
        /// </summary>
        public bool OnlyInPlayMode { get; set; }

        /// <summary>
        /// Initializes the Inspector display metadata.
        /// </summary>
        /// <param name="label">An optional custom label.</param>
        public ShowInInspectorAttribute(string label = null) => Label = label;
    }

    #endregion

    #region Automatic Reference Attributes

    /// <summary>
    /// Defines the location from which a component reference can be resolved.
    /// </summary>
    public enum AutoReferenceSource
    {
        /// <summary>Search the same GameObject.</summary>
        Self,

        /// <summary>Search child GameObjects.</summary>
        Children,

        /// <summary>Search parent GameObjects.</summary>
        Parent
    }

    /// <summary>
    /// Base class for attributes that identify automatic component references.
    /// These attributes can be applied to fields only.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public abstract class AutoReferenceAttribute : MetaAttribute
    {
        /// <summary>Gets the location used to resolve the reference.</summary>
        public abstract AutoReferenceSource Source { get; }

        /// <summary>
        /// Gets or sets whether inactive objects are included in the search.
        /// Defaults to true.
        /// </summary>
        public bool IncludeInactive { get; set; } = true;
    }

    /// <summary>
    /// Identifies a component reference on the same GameObject.
    /// </summary>
    public class GetComponentAttribute : AutoReferenceAttribute
    {
        public override AutoReferenceSource Source => AutoReferenceSource.Self;
    }

    /// <summary>
    /// Identifies a component reference that should be resolved from children.
    /// </summary>
    public class GetInChildrenAttribute : AutoReferenceAttribute
    {
        public override AutoReferenceSource Source => AutoReferenceSource.Children;
    }

    /// <summary>
    /// Identifies a component reference that should be resolved from parents.
    /// </summary>
    public class GetInParentAttribute : AutoReferenceAttribute
    {
        public override AutoReferenceSource Source => AutoReferenceSource.Parent;
    }

    #endregion

    #region Drag And Drop Attribute

    /// <summary>
    /// Displays a drop area above an array or List of UnityEngine.Object types so objects can be
    /// added by dragging them into the Inspector.
    /// </summary>
    /// <remarks>
    /// Dropped objects are resolved against the list's element type: direct matches, components of
    /// dropped GameObjects, sub-assets of dropped assets, and matching assets inside dropped folders.
    /// Empty (null) slots are filled before the list grows. The drop area is disabled while editing
    /// several objects at once. This attribute must be applied to the array or List field itself.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class DragAndDropAttribute : MetaAttribute
    {
        /// <summary>
        /// Gets the optional text displayed in the drop area.
        /// When omitted, the text is generated from the list's element type.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Gets or sets the height of the drop area in pixels. Defaults to 60.
        /// </summary>
        public float Height { get; set; } = 60f;

        /// <summary>
        /// Gets or sets whether an object already present in the list can be added again.
        /// Defaults to false.
        /// </summary>
        public bool AllowDuplicates { get; set; }

        /// <summary>
        /// Gets or sets whether dropping a project folder adds every matching asset inside it.
        /// Defaults to true.
        /// </summary>
        public bool AcceptFolders { get; set; } = true;

        /// <summary>
        /// Initializes the drop area metadata.
        /// </summary>
        /// <param name="label">An optional text displayed in the drop area.</param>
        public DragAndDropAttribute(string label = null) => Label = label;
    }

    #endregion
}