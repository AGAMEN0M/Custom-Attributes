
/*
 * ---------------------------------------------------------------------------
 * Description: Defines the attributes used to configure Scene View gizmos,
 *              including transforms, cubes, spheres, cones, and paths.
 *
 * Using:
 * Apply these attributes to fields supported by the package's Gizmo drawers.
 * The associated editor implementation reads the attribute settings and
 * uses them to determine how the gizmo is displayed.
 *
 * GizmoTransform:
 * Use [GizmoTransform] to configure a transform gizmo.
 * Use [GizmoTransform(nameof(rotation))] to associate a rotation property.
 *
 * GizmoCube:
 * Use [GizmoCube(nameof(size))] to associate a size property with a cube.
 * Specify a custom RGB color with [GizmoCube(nameof(size), 1f, 0f, 0f)].
 * Alternatively, use a hexadecimal color string:
 * [GizmoCube(nameof(size), "#FF0000FF")].
 *
 * GizmoSphere:
 * Use [GizmoSphere(nameof(radius))] to associate a radius property.
 * Specify an RGB color or hexadecimal color string to customize its color.
 *
 * GizmoCone:
 * Use [GizmoCone(nameof(angle), nameof(distance))] to associate the
 * cone's angle and distance properties.
 * A hexadecimal color string can be supplied as an additional argument.
 *
 * GizmoPath:
 * Use [GizmoPath] to configure a path gizmo.
 * Use [GizmoPath("#FFFF00FF")] to specify its color.
 * Set Closed to indicate a closed path and WorldSpace to configure the
 * coordinate space used by the path drawer.
 *
 * Common Setting:
 * Set AlwaysShow to configure whether the associated gizmo should remain
 * visible regardless of the selection state, as supported by its drawer.
 *
 * Example:
 * public Vector3 size = Vector3.one;
 * public float radius = 2f;
 * public float angle = 45f;
 * public float distance = 5f;
 * public Vector3 rotation;
 *
 * [GizmoCube(nameof(size), "#00FFFFFF")]
 * public Transform cubeGizmo;
 *
 * [GizmoSphere(nameof(radius))]
 * public Transform sphereGizmo;
 *
 * [GizmoCone(nameof(angle), nameof(distance))]
 * public Transform coneGizmo;
 *
 * Author: Lucas Gomes Cecchini
 * Pseudonym: AGAMENOM
 * ---------------------------------------------------------------------------
*/

using UnityEngine;

namespace CustomAttributes
{
    #region Gizmo Attribute Base

    /// <summary>
    /// Provides shared configuration for attributes that define gizmos
    /// in the Unity Scene View.
    /// </summary>
    public abstract class GizmoAttributeBase : PropertyAttribute
    {
        /// <summary>
        /// Gets the color assigned to the gizmo.
        /// </summary>
        public Color Color { get; }

        /// <summary>
        /// Gets or sets whether the gizmo should always be displayed,
        /// according to the behavior implemented by its drawer.
        /// </summary>
        public bool AlwaysShow { get; set; }

        /// <summary>
        /// Initializes the base attribute with a predefined color.
        /// </summary>
        /// <param name="color">The color used by the gizmo.</param>
        protected GizmoAttributeBase(Color color) => Color = color;

        /// <summary>
        /// Attempts to parse a hexadecimal HTML color string.
        /// </summary>
        /// <param name="hex">The hexadecimal color string.</param>
        /// <param name="fallback">The color returned if parsing fails.</param>
        /// <returns>The parsed color when valid; otherwise, the fallback color.</returns>
        protected static Color ParseColor(string hex, Color fallback) => ColorUtility.TryParseHtmlString(hex, out var parsed) ? parsed : fallback;
    }

    #endregion

    #region Transform Gizmo Attribute

    /// <summary>
    /// Configures a transform gizmo and optionally identifies a property
    /// that provides its rotation.
    /// </summary>
    public class GizmoTransformAttribute : GizmoAttributeBase
    {
        /// <summary>
        /// Gets the name of the property associated with the gizmo's rotation.
        /// Null when no property name was supplied.
        /// </summary>
        public string RotationPropertyName { get; }

        /// <summary>
        /// Initializes the transform gizmo with the default white color.
        /// </summary>
        public GizmoTransformAttribute() : base(Color.white) { }

        /// <summary>
        /// Initializes the transform gizmo with a rotation property reference.
        /// </summary>
        /// <param name="rotationPropertyName">
        /// The name of the property containing rotation data.
        /// </param>
        public GizmoTransformAttribute(string rotationPropertyName) : base(Color.white) => RotationPropertyName = rotationPropertyName;
    }

    #endregion

    #region Cube Gizmo Attribute

    /// <summary>
    /// Configures a cube gizmo using a property that provides its size.
    /// </summary>
    public class GizmoCubeAttribute : GizmoAttributeBase
    {
        /// <summary>
        /// Gets the name of the property containing the cube's size.
        /// </summary>
        public string SizePropertyName { get; }

        /// <summary>
        /// Initializes the cube gizmo with the default cyan color.
        /// </summary>
        /// <param name="sizePropertyName">
        /// The name of the property containing the cube's size.
        /// </param>
        public GizmoCubeAttribute(string sizePropertyName) : base(Color.cyan) => SizePropertyName = sizePropertyName;

        /// <summary>
        /// Initializes the cube gizmo with a custom RGB color.
        /// </summary>
        /// <param name="sizePropertyName">The size property name.</param>
        /// <param name="r">The red color component.</param>
        /// <param name="g">The green color component.</param>
        /// <param name="b">The blue color component.</param>
        public GizmoCubeAttribute(string sizePropertyName, float r, float g, float b) : base(new Color(r, g, b, 1f)) => SizePropertyName = sizePropertyName;

        /// <summary>
        /// Initializes the cube gizmo using a hexadecimal color string.
        /// </summary>
        /// <param name="sizePropertyName">The size property name.</param>
        /// <param name="hexColor">The hexadecimal color string.</param>
        public GizmoCubeAttribute(string sizePropertyName, string hexColor) : base(ParseColor(hexColor, Color.cyan)) => SizePropertyName = sizePropertyName;
    }

    #endregion

    #region Sphere Gizmo Attribute

    /// <summary>
    /// Configures a sphere gizmo using a property that provides its radius.
    /// </summary>
    public class GizmoSphereAttribute : GizmoAttributeBase
    {
        /// <summary>
        /// Gets the name of the property containing the sphere's radius.
        /// </summary>
        public string RadiusPropertyName { get; }

        /// <summary>
        /// Initializes the sphere gizmo with the default green color.
        /// </summary>
        /// <param name="radiusPropertyName">
        /// The name of the property containing the sphere's radius.
        /// </param>
        public GizmoSphereAttribute(string radiusPropertyName) : base(Color.green) => RadiusPropertyName = radiusPropertyName;

        /// <summary>
        /// Initializes the sphere gizmo with a custom RGB color.
        /// </summary>
        /// <param name="radiusPropertyName">The radius property name.</param>
        /// <param name="r">The red color component.</param>
        /// <param name="g">The green color component.</param>
        /// <param name="b">The blue color component.</param>
        public GizmoSphereAttribute(string radiusPropertyName, float r, float g, float b) : base(new Color(r, g, b, 1f)) => RadiusPropertyName = radiusPropertyName;

        /// <summary>
        /// Initializes the sphere gizmo using a hexadecimal color string.
        /// </summary>
        /// <param name="radiusPropertyName">The radius property name.</param>
        /// <param name="hexColor">The hexadecimal color string.</param>
        public GizmoSphereAttribute(string radiusPropertyName, string hexColor) : base(ParseColor(hexColor, Color.green)) => RadiusPropertyName = radiusPropertyName;
    }

    #endregion

    #region Cone Gizmo Attribute

    /// <summary>
    /// Configures a cone gizmo using properties for its angle and distance.
    /// </summary>
    public class GizmoConeAttribute : GizmoAttributeBase
    {
        /// <summary>
        /// Gets the name of the property containing the cone's angle.
        /// </summary>
        public string AnglePropertyName { get; }

        /// <summary>
        /// Gets the name of the property containing the cone's distance.
        /// </summary>
        public string DistancePropertyName { get; }

        /// <summary>
        /// Initializes the cone gizmo with the default yellow color.
        /// </summary>
        /// <param name="anglePropertyName">The angle property name.</param>
        /// <param name="distancePropertyName">The distance property name.</param>
        public GizmoConeAttribute(string anglePropertyName, string distancePropertyName) : base(Color.yellow)
        {
            AnglePropertyName = anglePropertyName;
            DistancePropertyName = distancePropertyName;
        }

        /// <summary>
        /// Initializes the cone gizmo with a hexadecimal color string.
        /// </summary>
        /// <param name="anglePropertyName">The angle property name.</param>
        /// <param name="distancePropertyName">The distance property name.</param>
        /// <param name="hexColor">The hexadecimal color string.</param>
        public GizmoConeAttribute(string anglePropertyName, string distancePropertyName, string hexColor) : base(ParseColor(hexColor, Color.yellow))
        {
            AnglePropertyName = anglePropertyName;
            DistancePropertyName = distancePropertyName;
        }
    }

    #endregion

    #region Path Gizmo Attribute

    /// <summary>
    /// Configures the appearance and coordinate-space options of a path gizmo.
    /// </summary>
    public class GizmoPathAttribute : GizmoAttributeBase
    {
        /// <summary>
        /// Gets or sets whether the path should connect its final point
        /// back to its first point, as implemented by the path drawer.
        /// </summary>
        public bool Closed { get; set; }

        /// <summary>
        /// Gets or sets whether path coordinates should be interpreted
        /// in world space rather than local space.
        /// </summary>
        public bool WorldSpace { get; set; }

        /// <summary>
        /// Initializes the path gizmo with the default yellow color.
        /// </summary>
        public GizmoPathAttribute() : base(Color.yellow) { }

        /// <summary>
        /// Initializes the path gizmo with a hexadecimal color string.
        /// Invalid values fall back to yellow.
        /// </summary>
        /// <param name="hexColor">The hexadecimal color string.</param>
        public GizmoPathAttribute(string hexColor) : base(ParseColor(hexColor, Color.yellow)) { }
    }

    #endregion
}