/*
 * ---------------------------------------------------------------------------
 * Description: Defines the Button attribute, allowing methods to be displayed
 *              as clickable buttons in the Unity Inspector.
 *
 * Using:
 * Apply [Button] to a method to display it as a button in the Inspector.
 * Use [Button("Custom Label")] to define its displayed text.
 * Configure Mode to control when the button is available:
 * - ButtonMode.Always: Available in Edit Mode and Play Mode.
 * - ButtonMode.PlayModeOnly: Available only during Play Mode.
 * - ButtonMode.EditModeOnly: Available only during Edit Mode.
 * Use Position to choose whether the button appears at the top or bottom.
 * Set Confirm to require confirmation before executing the method.
 * Set Height to customize the button's height.
 *
 * Example:
 * [Button("Reset Values", Mode = ButtonMode.EditModeOnly)]
 * private void ResetValues()
 * {
 *     // Reset values here.
 * }
 *
 * Author: Lucas Gomes Cecchini
 * Pseudonym: AGAMENOM
 * ---------------------------------------------------------------------------
*/

using System;

namespace CustomAttributes
{
    #region === Button configuration enums ===

    /// <summary>
    /// Defines when a method button is available in the Unity Inspector.
    /// </summary>
    /// <remarks>
    /// This setting lets a button be restricted to a specific Editor state,
    /// preventing methods intended for editing or runtime use from being
    /// exposed in the wrong context.
    /// </remarks>
    public enum ButtonMode
    {
        /// <summary>Makes the button available in both Edit Mode and Play Mode.</summary>
        Always,

        /// <summary>Makes the button available only while the application is running in Play Mode.</summary>
        PlayModeOnly,

        /// <summary>Makes the button available only while the Editor is in Edit Mode.</summary>
        EditModeOnly
    }

    /// <summary>
    /// Defines where the method button appears relative to other buttons.
    /// </summary>
    /// <remarks>
    /// The position is a presentation option used by the Inspector drawer.
    /// It does not affect the method's execution or its availability.
    /// </remarks>
    public enum ButtonPosition
    {
        /// <summary>Places the button before other method buttons.</summary>
        Top,

        /// <summary>Places the button after other method buttons.</summary>
        Bottom
    }

    #endregion

    #region === Button attribute ===

    /// <summary>
    /// Exposes a method as a button in the Unity Inspector.
    /// </summary>
    /// <remarks>
    /// This attribute stores presentation and execution options for a method.
    /// The Editor-side implementation is responsible for discovering attributed
    /// methods, drawing their buttons, applying mode restrictions, and invoking
    /// the methods when the user interacts with the Inspector.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class ButtonAttribute : Attribute
    {
        #region Properties

        /// <summary>
        /// Gets the optional text displayed on the method button.
        /// </summary>
        /// <remarks>
        /// A null label allows the Inspector implementation to determine an
        /// appropriate default, such as deriving the text from the method name.
        /// The attribute itself does not perform that fallback.
        /// </remarks>
        public string Label { get; }

        /// <summary>
        /// Gets or sets the Editor state in which the button is available.
        /// </summary>
        /// <remarks>
        /// Defaults to <see cref="ButtonMode.Always"/> so a button is not
        /// restricted to a particular Editor state unless explicitly configured.
        /// </remarks>
        public ButtonMode Mode { get; set; } = ButtonMode.Always;

        /// <summary>
        /// Gets or sets the button's position relative to other method buttons.
        /// </summary>
        /// <remarks>
        /// Defaults to <see cref="ButtonPosition.Top"/>. The Inspector drawer
        /// determines how this setting affects the final visual ordering.
        /// </remarks>
        public ButtonPosition Position { get; set; } = ButtonPosition.Top;

        /// <summary>
        /// Gets or sets whether invoking the button requires confirmation.
        /// </summary>
        /// <remarks>
        /// This option records the desired confirmation behavior. The Editor-side
        /// button implementation must display the confirmation prompt and decide
        /// whether to invoke the method based on the user's response.
        /// </remarks>
        public bool Confirm { get; set; }

        /// <summary>
        /// Gets or sets the requested height of the method button.
        /// </summary>
        /// <remarks>
        /// A value of zero is the default configuration and leaves the effective
        /// behavior to the Inspector drawer. The attribute does not enforce a
        /// minimum, maximum, or unit for this value.
        /// </remarks>
        public float Height { get; set; }

        #endregion

        #region Initialization

        /// <summary>
        /// Creates an attribute that exposes a method as an Inspector button.
        /// </summary>
        /// <param name="label">Optional text for the button. When omitted, the value is null.</param>
        /// <remarks>The label is immutable after construction, while the remaining configuration options can be set through named attribute arguments.</remarks>
        public ButtonAttribute(string label = null) => Label = label;

        #endregion
    }

    #endregion
}