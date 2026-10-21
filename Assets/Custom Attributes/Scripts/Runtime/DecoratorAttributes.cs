/*
 * ---------------------------------------------------------------------------
 * Description: Defines decorator attributes used to customize the appearance
 *              of fields in the Unity Inspector, including information boxes, titles,
 *              horizontal lines, labels, and suffixes.
 *
 * Using:
 * Apply these attributes above serialized fields to customize their
 * presentation in the Unity Inspector.
 *
 * InfoBox:
 * Use [InfoBox("Message")] to display an informational message.
 * Specify InfoBoxType.Warning or InfoBoxType.Error to change its severity.
 * InfoBoxType.None represents the absence of a message type.
 *
 * Title:
 * Use [Title("Section Name")] to identify a section above a field.
 *
 * HorizontalLine:
 * Use [HorizontalLine] to draw a separator.
 * Customize its appearance with thickness and a hexadecimal color:
 * [HorizontalLine(2f, "#FF0000FF")]
 * Thickness is clamped to a minimum of 1.
 * Invalid hexadecimal colors fall back to Color.gray.
 *
 * Label:
 * Use [Label("Custom Label")] to provide custom label text.
 *
 * Suffix:
 * Use [Suffix("units")] to associate suffix text with a field.
 *
 * Example:
 * [InfoBox("Configure the character settings.")]
 * [Title("Movement")]
 * [HorizontalLine(2f, "#808080FF")]
 * [Label("Movement Speed")]
 * [Suffix("m/s")]
 * public float movementSpeed = 5f;
 *
 * Note:
 * These attributes define the metadata used to customize Inspector fields.
 * Their visual rendering is handled by the corresponding editor drawers.
 *
 * Author: Lucas Gomes Cecchini
 * Pseudonym: AGAMENOM
 * ---------------------------------------------------------------------------
*/

using UnityEngine;

namespace CustomAttributes
{
    #region Info Box Attributes

    /// <summary>
    /// Defines the severity or presentation type of an information box.
    /// The corresponding editor drawer determines how each type is displayed.
    /// </summary>
    public enum InfoBoxType
    {
        /// <summary>Represents an information box without a specific message type.</summary>
        None,

        /// <summary>Represents a standard informational message.</summary>
        Info,

        /// <summary>Represents a warning message that requires attention.</summary>
        Warning,

        /// <summary>Represents an error message.</summary>
        Error
    }

    /// <summary>
    /// Attaches a message and its severity to a field for display in the
    /// Unity Inspector.
    /// </summary>
    public class InfoBoxAttribute : PropertyAttribute
    {
        /// <summary>
        /// Gets the message displayed by the information box.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the type used to determine the message's presentation.
        /// </summary>
        public InfoBoxType Type { get; }

        /// <summary>
        /// Initializes the information box configuration.
        /// </summary>
        /// <param name="message">The message to display.</param>
        /// <param name="type">The message type. Defaults to <see cref="InfoBoxType.Info"/>.</param>
        public InfoBoxAttribute(string message, InfoBoxType type = InfoBoxType.Info)
        {
            Message = message;
            Type = type;
        }
    }

    #endregion

    #region Title Attribute

    /// <summary>
    /// Provides a title string that an editor drawer can use to identify
    /// or separate a section of Inspector fields.
    /// </summary>
    public class TitleAttribute : PropertyAttribute
    {
        /// <summary>
        /// Gets the title text.
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// Initializes the title displayed by the associated editor drawer.
        /// </summary>
        /// <param name="title">The title text.</param>
        public TitleAttribute(string title) => Title = title;
    }

    #endregion

    #region Horizontal Line Attribute

    /// <summary>
    /// Provides the thickness and color settings for a horizontal separator
    /// in the Unity Inspector.
    /// </summary>
    public class HorizontalLineAttribute : PropertyAttribute
    {
        /// <summary>
        /// Gets the line thickness, with a minimum value of 1.
        /// </summary>
        public float Thickness { get; }

        /// <summary>
        /// Gets the parsed color used by the separator.
        /// </summary>
        public Color Color { get; }

        /// <summary>
        /// Initializes the separator's appearance.
        /// </summary>
        /// <param name="thickness">The requested line thickness. Values below 1 are clamped to 1.</param>
        /// <param name="hexColor">
        /// The color expressed as a hexadecimal HTML color string.
        /// Defaults to semi-transparent gray.
        /// </param>
        public HorizontalLineAttribute(float thickness = 1f, string hexColor = "#80808080")
        {
            Thickness = Mathf.Max(1f, thickness);
            Color = ColorUtility.TryParseHtmlString(hexColor, out var parsed) ? parsed : Color.gray;
        }
    }

    #endregion

    #region Label Attribute

    /// <summary>
    /// Provides custom text for an editor drawer to use as a field label.
    /// </summary>
    public class LabelAttribute : PropertyAttribute
    {
        /// <summary>
        /// Gets the custom label text.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Initializes the custom label.
        /// </summary>
        /// <param name="text">The text to use for the field label.</param>
        public LabelAttribute(string text) => Text = text;
    }

    #endregion

    #region Suffix Attribute

    /// <summary>
    /// Provides suffix text associated with a field, such as a unit of
    /// measurement. Its visual placement is handled by the editor drawer.
    /// </summary>
    public class SuffixAttribute : PropertyAttribute
    {
        /// <summary>
        /// Gets the suffix text.
        /// </summary>
        public string Suffix { get; }

        /// <summary>
        /// Initializes the suffix associated with the field.
        /// </summary>
        /// <param name="suffix">The suffix text to display.</param>
        public SuffixAttribute(string suffix) => Suffix = suffix;
    }

    #endregion
}