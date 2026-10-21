
/*
 * ---------------------------------------------------------------------------
 * Description: Defines field attributes that customize how fields are
 *              displayed, selected, validated, or configured in the Unity Inspector.
 *
 * Using:
 * Apply these attributes above fields to activate their corresponding
 * custom Inspector drawers.
 *
 * TagDropdown:
 * Use [TagDropdown] on a string field to select a Unity tag from a dropdown.
 *
 * SceneTagDropdown:
 * Use [SceneTagDropdown] on a string field to select a scene from a dropdown.
 *
 * LayerDropdown:
 * Use [LayerDropdown] on a field to select a Unity layer from a dropdown.
 *
 * Dropdown:
 * Use [Dropdown(nameof(AvailableOptions))] to associate a field with a
 * member that provides its dropdown options.
 *
 * HighlightEmptyReference:
 * Use [HighlightEmptyReference] to highlight missing or empty references.
 *
 * RequireInterface:
 * Use [RequireInterface(typeof(IMyInterface))] to specify the interface
 * expected by a reference field.
 *
 * Expandable:
 * Use [Expandable] to allow a supported referenced object to be expanded
 * directly in the Inspector.
 *
 * MinMaxSlider:
 * Use [MinMaxSlider(0f, 100f)] to define the minimum and maximum values
 * for a range slider.
 *
 * UniqueId:
 * Use [UniqueId] on a field to enable the associated unique-ID behavior.
 *
 * Example:
 * [TagDropdown]
 * public string targetTag;
 *
 * [LayerDropdown]
 * public int targetLayer;
 *
 * [MinMaxSlider(0f, 100f)]
 * public float intensity;
 *
 * Note:
 * These attributes store metadata or identify fields for custom Inspector
 * drawers. Their exact behavior depends on the corresponding editor code.
 *
 * Author: Lucas Gomes Cecchini
 * Pseudonym: AGAMENOM
 * ---------------------------------------------------------------------------
*/

using UnityEngine;
using System;

namespace CustomAttributes
{
    #region Dropdown Attributes

    /// <summary>
    /// Marks a field for a dropdown containing Unity tags.
    /// The associated editor drawer handles the available options and display.
    /// </summary>
    public class TagDropdownAttribute : PropertyAttribute { }

    /// <summary>
    /// Marks a field for a dropdown containing scene options.
    /// The associated editor drawer determines which scenes are available.
    /// </summary>
    public class SceneTagDropdownAttribute : PropertyAttribute { }

    /// <summary>
    /// Marks a field for a dropdown containing Unity layers.
    /// </summary>
    public class LayerDropdownAttribute : PropertyAttribute { }

    /// <summary>
    /// Associates a field with a member that provides dropdown options.
    /// </summary>
    public class DropdownAttribute : PropertyAttribute
    {
        /// <summary>
        /// Gets the name of the member associated with the dropdown options.
        /// </summary>
        public string MemberName { get; }

        /// <summary>
        /// Initializes the dropdown attribute with the options member name.
        /// </summary>
        /// <param name="memberName">
        /// The name of the member that provides the dropdown options.
        /// </param>
        public DropdownAttribute(string memberName) => MemberName = memberName;
    }

    #endregion

    #region Reference Attributes

    /// <summary>
    /// Marks a field so the associated editor drawer can highlight empty
    /// or missing references.
    /// </summary>
    public class HighlightEmptyReferenceAttribute : PropertyAttribute { }

    /// <summary>
    /// Specifies an interface type expected by a reference field.
    /// The corresponding editor implementation determines how this
    /// requirement is validated or displayed.
    /// </summary>
    public class RequireInterfaceAttribute : PropertyAttribute
    {
        /// <summary>
        /// Gets the interface type required by the attribute.
        /// </summary>
        public Type InterfaceType { get; }

        /// <summary>
        /// Initializes the attribute with the required interface type.
        /// </summary>
        /// <param name="interfaceType">The expected interface type.</param>
        public RequireInterfaceAttribute(Type interfaceType) => InterfaceType = interfaceType;
    }

    /// <summary>
    /// Marks a reference field for expandable Inspector presentation,
    /// when supported by the corresponding editor drawer.
    /// </summary>
    public class ExpandableAttribute : PropertyAttribute { }

    #endregion

    #region Value Attributes

    /// <summary>
    /// Defines the minimum and maximum values associated with a range slider.
    /// </summary>
    public class MinMaxSliderAttribute : PropertyAttribute
    {
        /// <summary>
        /// Gets the lower bound of the slider range.
        /// </summary>
        public float Min { get; }

        /// <summary>
        /// Gets the upper bound of the slider range.
        /// </summary>
        public float Max { get; }

        /// <summary>
        /// Initializes the slider range.
        /// </summary>
        /// <param name="min">The minimum value.</param>
        /// <param name="max">The maximum value.</param>
        public MinMaxSliderAttribute(float min, float max)
        {
            Min = min;
            Max = max;
        }
    }

    /// <summary>
    /// Marks a field for the unique-identifier behavior implemented by
    /// the corresponding editor tooling.
    /// </summary>
    public class UniqueIdAttribute : PropertyAttribute { }

    #endregion
}