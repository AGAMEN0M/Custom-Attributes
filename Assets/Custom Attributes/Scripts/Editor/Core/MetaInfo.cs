/*
 * ---------------------------------------------------------------------------
 * Description: Collects and caches Custom Attributes metadata for reflected fields,
 *              methods, and types, including conditional attributes, Inspector buttons,
 *              Inspector-only members, and nested serializable classes.
 *
 * Author: Lucas Gomes Cecchini
 * Pseudonym: AGAMENOM
 * ---------------------------------------------------------------------------
 */

using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using System.Linq;
using UnityEngine;
using System;

using Object = UnityEngine.Object;

namespace CustomAttributes.Editing
{
    #region === Field Metadata ===

    /// <summary>
    /// Stores the Custom Attributes metadata collected from a reflected field.
    /// </summary>
    /// <remarks>
    /// Metadata is built once per reflected field and cached for subsequent Inspector operations.
    /// Fields without relevant metadata share the <see cref="Empty"/> instance to avoid
    /// allocating equivalent metadata objects.
    /// </remarks>
    internal sealed class MetaInfo
    {
        #region === Cache ===

        /// <summary>
        /// Shared metadata instance returned for fields that have no relevant attributes.
        /// </summary>
        public static readonly MetaInfo Empty = new();

        /// <summary>
        /// Caches metadata by reflected field so attribute discovery is not repeated during Inspector rendering.
        /// </summary>
        private static readonly Dictionary<FieldInfo, MetaInfo> cache = new();

        #endregion

        #region === Attribute Data ===

        /// <summary>
        /// Conditional attributes that determine whether a field is visible or editable.
        /// </summary>
        public ConditionAttribute[] Conditions = Array.Empty<ConditionAttribute>();

        /// <summary>
        /// Callbacks invoked when the field's serialized value changes.
        /// </summary>
        public OnValueChangedAttribute[] OnChanged = Array.Empty<OnValueChangedAttribute>();

        /// <summary>
        /// Read-only configuration applied to the field.
        /// </summary>
        public ReadOnlyAttribute ReadOnly;

        /// <summary>
        /// Required-value validation configuration for the field.
        /// </summary>
        public RequiredAttribute Required;

        /// <summary>
        /// Custom GUI color applied while drawing the field.
        /// </summary>
        public GUIColorAttribute Color;

        /// <summary>
        /// Configuration for automatically assigning a component reference.
        /// </summary>
        public AutoReferenceAttribute AutoRef;

        /// <summary>
        /// Foldout group configuration used to organize the field in the Inspector.
        /// </summary>
        public FoldoutGroupAttribute Group;

        /// <summary>
        /// Configuration for displaying and controlling a path gizmo associated with the field.
        /// </summary>
        public GizmoPathAttribute Path;

        /// <summary>
        /// Configuration for the drag-and-drop area displayed above an array or List field.
        /// </summary>
        public DragAndDropAttribute DragDrop;

        /// <summary>
        /// Indicates whether the field declares a Unity <see cref="PropertyAttribute"/>.
        /// </summary>
        /// <remarks>
        /// This flag is used when deciding whether nested serializable fields can be drawn recursively,
        /// because Unity property attributes may require the standard property-drawing pipeline.
        /// </remarks>
        public bool HasPropertyAttribute;

        /// <summary>
        /// Indicates whether the field has metadata handled by the custom Inspector.
        /// </summary>
        /// <remarks>This value represents the attributes collected by <see cref="Build"/>; it does not include <see cref="HasPropertyAttribute"/> by itself.</remarks>
        public bool HasAny;

        #endregion

        #region === Metadata Retrieval ===

        /// <summary>
        /// Retrieves the cached metadata for a field, building it when it has not been processed before.
        /// </summary>
        /// <param name="field">The reflected field whose attributes should be collected, or null when no field is available.</param>
        /// <returns>The cached metadata for the field, or <see cref="Empty"/> when the field is null or has no relevant metadata.</returns>
        public static MetaInfo For(FieldInfo field)
        {
            // A missing reflected field has no field-level attributes to inspect.
            if (field == null) return Empty;

            // Cache lookup avoids repeatedly reflecting over the same field during Inspector repaints.
            if (!cache.TryGetValue(field, out var info))
            {
                info = Build(field);
                cache[field] = info;
            }

            return info;
        }

        /// <summary>
        /// Collects supported metadata attributes from a reflected field.
        /// </summary>
        /// <param name="field">The reflected field whose attributes should be inspected.</param>
        /// <returns>The populated metadata object, or <see cref="Empty"/> when no relevant attributes are present.</returns>
        private static MetaInfo Build(FieldInfo field)
        {
            // Collect the package's metadata attributes separately from Unity's PropertyAttribute-derived attributes.
            var attributes = field.GetCustomAttributes(typeof(MetaAttribute), true);
            var path = field.GetCustomAttribute<GizmoPathAttribute>(true);
            bool hasPropertyAttribute = field.IsDefined(typeof(PropertyAttribute), true);

            // Avoid allocating a metadata object when the field has nothing for this system to process.
            if (attributes.Length == 0 && path == null && !hasPropertyAttribute) return Empty;

            var info = new MetaInfo { Path = path, HasPropertyAttribute = hasPropertyAttribute };

            // Conditions and callbacks can occur multiple times on a field, so preserve every occurrence in order.
            var conditions = new List<ConditionAttribute>();
            var callbacks = new List<OnValueChangedAttribute>();

            foreach (var attribute in attributes)
            {
                // Store each supported attribute in its corresponding slot; repeated single-value attributes retain the last encountered instance.
                switch (attribute)
                {
                    case ConditionAttribute condition: conditions.Add(condition); break;
                    case OnValueChangedAttribute callback: callbacks.Add(callback); break;
                    case ReadOnlyAttribute readOnly: info.ReadOnly = readOnly; break;
                    case RequiredAttribute required: info.Required = required; break;
                    case GUIColorAttribute color: info.Color = color; break;
                    case AutoReferenceAttribute autoRef: info.AutoRef = autoRef; break;
                    case FoldoutGroupAttribute group: info.Group = group; break;
                    case DragAndDropAttribute dragDrop: info.DragDrop = dragDrop; break;
                }
            }

            // Convert the variable-length collections into arrays for efficient repeated access while drawing the Inspector.
            info.Conditions = conditions.ToArray();
            info.OnChanged = callbacks.ToArray();

            // HasAny excludes Unity PropertyAttribute metadata, which is tracked separately for nested-property drawing decisions.
            info.HasAny = conditions.Count > 0 || callbacks.Count > 0 || info.ReadOnly != null || info.Required != null || info.Color != null || info.AutoRef != null || info.Group != null || info.Path != null || info.DragDrop != null;
            return info;
        }

        #endregion
    }

    #endregion

    #region === Button Metadata ===

    /// <summary>
    /// Stores the reflected method information required to render and invoke an Inspector button.
    /// </summary>
    internal sealed class ButtonInfo
    {
        #region === Button Data ===

        /// <summary>
        /// The reflected method invoked when the button is activated.
        /// </summary>
        public readonly MethodInfo Method;

        /// <summary>
        /// The attribute that configures the button's label, position, execution mode, and appearance.
        /// </summary>
        public readonly ButtonAttribute Attribute;

        /// <summary>
        /// The reflected parameters accepted by the method.
        /// </summary>
        public readonly ParameterInfo[] Parameters;

        /// <summary>
        /// The formatted label displayed on the Inspector button.
        /// </summary>
        public readonly string Label;

        /// <summary>
        /// Indicates whether the method returns an <see cref="IEnumerator"/> compatible with coroutine execution.
        /// </summary>
        public readonly bool IsCoroutine;

        /// <summary>
        /// Indicates whether all method parameters can be represented by the supported Inspector controls.
        /// </summary>
        public readonly bool SupportedParameters;

        #endregion

        #region === Initialization ===

        /// <summary>
        /// Creates button metadata from a method decorated with <see cref="ButtonAttribute"/>.
        /// </summary>
        /// <param name="method">The reflected method that defines the button action.</param>
        public ButtonInfo(MethodInfo method)
        {
            Method = method;

            // Read the attribute and signature once so Inspector drawing does not need to repeat reflection.
            Attribute = method.GetCustomAttribute<ButtonAttribute>();
            Parameters = method.GetParameters();

            // Prefer an explicitly configured label; otherwise, make the method name more readable for the Inspector.
            Label = TypeFeatures.FormatName(Attribute.Label ?? method.Name);

            // Identify coroutine-returning methods so the Inspector can restrict them to Play Mode.
            IsCoroutine = typeof(IEnumerator).IsAssignableFrom(method.ReturnType);

            // Generic method definitions cannot be invoked like ordinary closed methods, and every parameter must have a supported control.
            SupportedParameters = !method.IsGenericMethodDefinition && Parameters.All(p => IsSupported(p.ParameterType));
        }

        #endregion

        #region === Parameter Validation ===

        /// <summary>
        /// Determines whether a parameter type can be edited using the available Inspector controls.
        /// </summary>
        /// <param name="t">The parameter type to validate.</param>
        /// <returns><see langword="true"/> when the type has a supported control; otherwise, <see langword="false"/>.</returns>
        private static bool IsSupported(Type t) => t == typeof(int) || t == typeof(float) || t == typeof(double) || t == typeof(bool) || t == typeof(string) || t == typeof(Vector2) || t == typeof(Vector3) || t == typeof(Color) || t.IsEnum || typeof(Object).IsAssignableFrom(t);

        #endregion
    }

    #endregion

    #region === Type Features ===

    /// <summary>
    /// Collects the custom Inspector features declared by a type and its user-defined base classes.
    /// </summary>
    /// <remarks>The collected metadata includes button methods, members marked with <see cref="ShowInInspectorAttribute"/>, and whether fields contain metadata that requires custom property rendering.</remarks>
    internal sealed class TypeFeatures
    {
        #region === Feature Data ===

        /// <summary>
        /// The Inspector buttons discovered on the type and its base classes.
        /// </summary>
        public ButtonInfo[] Buttons = Array.Empty<ButtonInfo>();

        /// <summary>
        /// The fields and properties marked for display in the Inspector.
        /// </summary>
        public MemberInfo[] ShowMembers = Array.Empty<MemberInfo>();

        /// <summary>
        /// Indicates whether any field or nested serializable field requires custom metadata rendering.
        /// </summary>
        public bool HasFieldMeta;

        /// <summary>
        /// Indicates whether the type has any custom Inspector feature.
        /// </summary>
        /// <remarks>
        /// This includes field metadata, button methods, and members marked for Inspector display.
        /// </remarks>
        public bool HasAny => HasFieldMeta || Buttons.Length > 0 || ShowMembers.Length > 0;

        #endregion

        #region === Caches and Name Formatting ===

        /// <summary>
        /// Caches discovered features by type to avoid repeating reflection for each Inspector instance.
        /// </summary>
        private static readonly Dictionary<Type, TypeFeatures> cache = new();

        /// <summary>
        /// Caches whether a serializable type contains nested metadata.
        /// </summary>
        private static readonly Dictionary<Type, bool> nestedCache = new();

        /// <summary>
        /// Separates lowercase or numeric characters from a following uppercase character when formatting names.
        /// </summary>
        private static readonly Regex lowerUpper = new(@"([a-z0-9])([A-Z])", RegexOptions.Compiled);

        /// <summary>
        /// Separates an acronym from a following capitalized word when formatting names.
        /// </summary>
        private static readonly Regex acronym = new(@"([A-Z]+)([A-Z][a-z])", RegexOptions.Compiled);

        /// <summary>
        /// Retrieves cached features for a type, building the metadata when necessary.
        /// </summary>
        /// <param name="type">The type whose custom Inspector features should be discovered.</param>
        /// <returns>The cached or newly built feature metadata for the specified type.</returns>
        public static TypeFeatures For(Type type)
        {
            // Type-level caching keeps repeated Inspector initialization from rescanning the same inheritance hierarchy.
            if (!cache.TryGetValue(type, out var features))
            {
                features = Build(type);
                cache[type] = features;
            }

            return features;
        }

        /// <summary>
        /// Converts a code-style identifier into a more readable label.
        /// </summary>
        /// <param name="name">The identifier to format.</param>
        /// <returns>The formatted label with underscores replaced and word boundaries separated by spaces.</returns>
        public static string FormatName(string name)
        {
            // Replace underscores first so they act as explicit word separators before capitalization is processed.
            string result = name.Replace('_', ' ');

            // Separate ordinary camelCase or PascalCase word boundaries.
            result = lowerUpper.Replace(result, "$1 $2");

            // Keep acronym sequences together while separating the next capitalized word, such as "XMLParser" into "XML Parser".
            result = acronym.Replace(result, "$1 $2");

            return result.Trim();
        }

        #endregion

        #region === Feature Discovery ===

        /// <summary>
        /// Discovers custom Inspector features declared by a type and its user-defined base classes.
        /// </summary>
        /// <param name="type">The type whose declared members should be inspected.</param>
        /// <returns>The collected button, Inspector-member, and field-metadata information.</returns>
        private static TypeFeatures Build(Type type)
        {
            var result = new TypeFeatures();
            var buttons = new List<ButtonInfo>();
            var members = new List<MemberInfo>();

            // Inspect only members declared at each hierarchy level to avoid collecting inherited members more than once.
            // PropertyReflection.AllMembers supplies the common reflection flags used by the rest of the package.
            const BindingFlags flags = PropertyReflection.AllMembers | BindingFlags.DeclaredOnly;

            // Stop before Unity's engine base classes; their internal members are not part of the user's custom Inspector metadata.
            for (var t = type; t != null && !IsEngineBase(t); t = t.BaseType)
            {
                // Sort methods by metadata token to keep button order stable within each declaring type.
                var levelButtons = t.GetMethods(flags).Where(m => m.IsDefined(typeof(ButtonAttribute), false)).OrderBy(m => m.MetadataToken).Select(m => new ButtonInfo(m)).ToList();
                buttons.InsertRange(0, levelButtons);

                foreach (var field in t.GetFields(flags))
                {
                    // Only explicitly marked fields are added to the read-only Inspector member list.
                    if (field.IsDefined(typeof(ShowInInspectorAttribute), false)) members.Add(field);

                    // Nested metadata also requires custom drawing even when the containing field has no direct metadata.
                    if (MetaInfo.For(field).HasAny || NestedHasMeta(field.FieldType)) result.HasFieldMeta = true;
                }

                foreach (var property in t.GetProperties(flags))
                {
                    // Exclude indexers because they require arguments and cannot be read as ordinary Inspector values.
                    if (property.CanRead && property.GetIndexParameters().Length == 0 && property.IsDefined(typeof(ShowInInspectorAttribute), false)) members.Add(property);
                }
            }

            // Store the completed collections as arrays for efficient use by the Inspector.
            result.Buttons = buttons.ToArray();
            result.ShowMembers = members.ToArray();
            return result;
        }

        /// <summary>
        /// Determines whether a type is one of the Unity or CLR base types excluded from custom feature discovery.
        /// </summary>
        /// <param name="t">The type to check.</param>
        /// <returns><see langword="true"/> when the type is an excluded engine or CLR base type; otherwise, <see langword="false"/>.</returns>
        private static bool IsEngineBase(Type t) => t == typeof(object) || t == typeof(MonoBehaviour) || t == typeof(Behaviour) || t == typeof(Component) || t == typeof(ScriptableObject) || t == typeof(Object);

        #endregion

        #region === Nested Serializable Classes ===

        /// <summary>
        /// Determines whether a type contains metadata that requires custom drawing of nested serialized fields.
        /// </summary>
        /// <param name="type">The type to inspect for nested metadata.</param>
        /// <returns><see langword="true"/> when a supported nested serializable type contains relevant metadata; otherwise, <see langword="false"/>.</returns>
        public static bool NestedHasMeta(Type type) => NestedHasMeta(type, 0);

        /// <summary>
        /// Recursively checks a serializable type and its base classes for fields with Custom Attributes metadata.
        /// </summary>
        /// <param name="type">The nested type currently being inspected.</param>
        /// <param name="depth">The current recursion depth, used to limit nested type traversal.</param>
        /// <returns><see langword="true"/> when relevant metadata is found; otherwise, <see langword="false"/>.</returns>
        private static bool NestedHasMeta(Type type, int depth)
        {
            // Limit recursion to prevent deeply nested type graphs from causing excessive reflection work.
            if (depth > 3 || !IsNestedSerializable(type)) return false;
            if (nestedCache.TryGetValue(type, out bool cached)) return cached;

            nestedCache[type] = false;

            // Inspect instance fields declared at each level, including private fields that may carry serialization metadata.
            bool result = false;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            for (var t = type; t != null && t != typeof(object) && !result; t = t.BaseType)
            {
                foreach (var field in t.GetFields(flags))
                {
                    // A nested type qualifies if one of its fields has metadata directly or contains another qualifying nested type.
                    if (MetaInfo.For(field).HasAny || NestedHasMeta(field.FieldType, depth + 1))
                    {
                        result = true;
                        break;
                    }
                }
            }

            // Cache the completed result so future checks do not repeat the recursive traversal.
            nestedCache[type] = result;
            return result;
        }

        /// <summary>
        /// Determines whether a type is eligible for nested serializable metadata inspection.
        /// </summary>
        /// <param name="t">The candidate type to validate.</param>
        /// <returns><see langword="true"/> when the type is a supported user-defined serializable class or value type; otherwise, <see langword="false"/>.</returns>
        private static bool IsNestedSerializable(Type t)
        {
            // Exclude missing types, arrays, primitives, and enums because they do not represent nested field containers here.
            if (t == null || t.IsArray || t.IsPrimitive || t.IsEnum) return false;

            // Only classes and value types can be traversed as nested field containers.
            if (!(t.IsClass || t.IsValueType)) return false;

            // Require an explicit Serializable attribute rather than recursively inspecting arbitrary CLR types.
            if (!t.IsDefined(typeof(SerializableAttribute), false)) return false;

            // UnityEngine.Object instances have their own serialization and Inspector behavior.
            if (typeof(Object).IsAssignableFrom(t)) return false;

            // Avoid traversing Unity and framework implementation types as if they were user-defined serializable data containers.
            string ns = t.Namespace ?? string.Empty;

            return !ns.StartsWith("UnityEngine", StringComparison.Ordinal) && !ns.StartsWith("System", StringComparison.Ordinal);
        }

        #endregion
    }

    #endregion
}