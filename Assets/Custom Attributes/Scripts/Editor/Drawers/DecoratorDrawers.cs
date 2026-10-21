/*
 * ---------------------------------------------------------------------------
 * Description: Provides custom Unity DecoratorDrawers for the InfoBox, Title,
 *              and HorizontalLine attributes. These drawers render informational
 *              message boxes, section headings with separators, and configurable
 *              horizontal lines in the Inspector, respectively.
 *
 * Author: Lucas Gomes Cecchini
 * Pseudonym: AGAMENOM
 * ---------------------------------------------------------------------------
*/

using UnityEditor;
using UnityEngine;

namespace CustomAttributes.Editing
{
    #region === Info Box Drawer ===

    /// <summary>
    /// Renders an informational message box in the Unity Inspector using the
    /// message and severity configured by <see cref="InfoBoxAttribute"/>.
    /// </summary>
    /// <remarks>
    /// The drawer uses Unity's built-in help box styling so informational messages
    /// remain visually consistent with the Editor's native warnings and errors.
    /// Its height is calculated from the message and available Inspector width,
    /// with a minimum height to keep short messages readable.
    /// </remarks>
    [CustomPropertyDrawer(typeof(InfoBoxAttribute))]
    public sealed class InfoBoxDrawer : DecoratorDrawer
    {
        #region === Layout Configuration ===

        /// <summary>
        /// Vertical spacing added below the message box.
        /// </summary>
        /// <remarks>Keeping this spacing separate from the calculated help-box height prevents adjacent Inspector elements from appearing crowded.</remarks>
        private const float Margin = 4f;

        #endregion

        #region === Layout Calculation ===

        /// <summary>
        /// Calculates the vertical space required to display the message box.
        /// </summary>
        /// <returns>The calculated message height, including the bottom margin.</returns>
        public override float GetHeight()
        {
            var attr = (InfoBoxAttribute)attribute;

            // Estimate the usable width by accounting for the Inspector's surrounding layout.
            // The help box uses this width to determine how many lines the message occupies when wrapped.
            float width = EditorGUIUtility.currentViewWidth - 40f;
            float height = EditorStyles.helpBox.CalcHeight(new GUIContent(attr.Message), width);

            // Short messages still need enough room to look like a help box rather than a thin label. The margin is added after applying the minimum.
            return Mathf.Max(height, 32f) + Margin;
        }

        #endregion

        #region === Inspector Rendering ===

        /// <summary>
        /// Draws the message box using the configured text and severity.
        /// </summary>
        /// <param name="position">The rectangle allocated to this decorator.</param>
        public override void OnGUI(Rect position)
        {
            var attr = (InfoBoxAttribute)attribute;
            position.height -= Margin;
            EditorGUI.HelpBox(position, attr.Message, ToMessageType(attr.Type));
        }

        /// <summary>
        /// Converts the attribute's message category into Unity's help-box type.
        /// </summary>
        /// <param name="type">The message category configured on the attribute.</param>
        /// <returns>The corresponding Unity Editor message type.</returns>
        private static MessageType ToMessageType(InfoBoxType type) => type switch
        {
            InfoBoxType.Info => MessageType.Info,
            InfoBoxType.Warning => MessageType.Warning,
            InfoBoxType.Error => MessageType.Error,
            _ => MessageType.None
        };

        #endregion
    }

    #endregion

    #region === Title Drawer ===

    /// <summary>
    /// Draws a bold section title followed by a subtle horizontal separator.
    /// </summary>
    /// <remarks>
    /// This decorator provides a lightweight way to visually divide Inspector
    /// content without introducing a foldout or changing the underlying property layout.
    /// </remarks>
    [CustomPropertyDrawer(typeof(TitleAttribute))]
    public sealed class TitleDrawer : DecoratorDrawer
    {
        #region === Layout Calculation ===

        /// <summary>
        /// Calculates the space required for the title and its separator.
        /// </summary>
        /// <returns>The single-line label height plus additional vertical spacing.</returns>
        public override float GetHeight() => EditorGUIUtility.singleLineHeight + 8f;

        #endregion

        #region === Inspector Rendering ===

        /// <summary>
        /// Draws the title label and the separator beneath it.
        /// </summary>
        /// <param name="position">The rectangle allocated to this decorator.</param>
        public override void OnGUI(Rect position)
        {
            var attr = (TitleAttribute)attribute;

            // Offset the label slightly from the top to give the heading visual breathing room.
            Rect label = new(position.x, position.y + 2f, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(label, attr.Title, EditorStyles.boldLabel);

            // Position the separator relative to the label instead of the original rectangle, keeping the line aligned if the label height changes.
            Rect line = new(position.x, label.yMax + 1f, position.width, 1f);
            EditorGUI.DrawRect(line, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        }

        #endregion
    }

    #endregion

    #region === Horizontal Line Drawer ===

    /// <summary>
    /// Draws a horizontal line using the thickness and color configured by
    /// <see cref="HorizontalLineAttribute"/>.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="TitleDrawer"/>, this decorator does not draw a label.
    /// Its appearance is controlled entirely by the attribute, allowing the line
    /// to serve as a simple visual separator between Inspector elements.
    /// </remarks>
    [CustomPropertyDrawer(typeof(HorizontalLineAttribute))]
    public sealed class HorizontalLineDrawer : DecoratorDrawer
    {
        #region === Layout Calculation ===

        /// <summary>
        /// Calculates the vertical space required for the line and surrounding spacing.
        /// </summary>
        /// <returns>The configured line thickness plus eight points of spacing.</returns>
        public override float GetHeight()
        {
            var attr = (HorizontalLineAttribute)attribute;
            return attr.Thickness + 8f;
        }

        #endregion

        #region === Inspector Rendering ===

        public override void OnGUI(Rect position)
        {
            var attr = (HorizontalLineAttribute)attribute;

            // Keep a small offset from the top of the allocated area so the line does not touch the preceding Inspector element.
            Rect line = new(position.x, position.y + 4f, position.width, attr.Thickness);
            EditorGUI.DrawRect(line, attr.Color);
        }

        #endregion
    }

    #endregion
}