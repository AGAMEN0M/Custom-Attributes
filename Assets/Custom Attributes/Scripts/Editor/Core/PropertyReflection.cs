/*
 * ---------------------------------------------------------------------------
 * Description: Provides reflection utilities for resolving Unity SerializedProperties
 *              to their corresponding fields, navigating nested objects and collection
 *              elements, locating sibling properties, reading members by name, and
 *              invoking callbacks. Field lookups are cached to reduce repeated
 *              reflection work while drawing custom Inspectors.
 *
 * Author: Lucas Gomes Cecchini
 * Pseudonym: AGAMENOM
 * ---------------------------------------------------------------------------
*/

using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using System;

using Object = UnityEngine.Object;

namespace CustomAttributes.Editing
{
    /// <summary>
    /// Provides shared reflection helpers for resolving serialized fields, navigating
    /// object ownership, accessing members dynamically, and invoking callbacks.
    /// </summary>
    /// <remarks>
    /// This class centralizes reflection logic used by the custom Inspector system.
    /// Keeping property-path handling and member lookup in one place avoids duplicating
    /// Unity-specific path parsing throughout the editor implementation.
    /// </remarks>
    internal static class PropertyReflection
    {
        #region === Reflection Configuration ===

        /// <summary>
        /// Includes instance and static members, regardless of their visibility.
        /// </summary>
        /// <remarks>
        /// Private and inherited fields may contain metadata required by custom
        /// attributes, so reflection must not be restricted to public members.
        /// BindingFlags.DeclaredOnly is added separately when traversing each
        /// inheritance level to control lookup order.
        /// </remarks>
        public const BindingFlags AllMembers = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>
        /// Matches Unity's array and list element segments in serialized property paths.
        /// </summary>
        /// <remarks>
        /// Unity represents collection elements using paths such as
        /// "items.Array.data[0]". Replacing the index-specific segment with "[]"
        /// allows fields belonging to different elements of the same collection
        /// to share a cache entry.
        /// </remarks>
        private static readonly Regex ArrayIndexRegex = new(@"\.Array\.data\[\d+\]", RegexOptions.Compiled);

        /// <summary>
        /// Caches field-resolution results by root type and normalized property path.
        /// </summary>
        /// <remarks>
        /// The field value can be null when resolution fails. Dictionary membership
        /// therefore distinguishes a cached failure from a path that has not yet
        /// been evaluated.
        /// </remarks>
        private static readonly Dictionary<(Type, string), FieldInfo> fieldCache = new();

        #endregion

        #region === Field Resolution ===

        /// <summary>
        /// Finds a field by name, searching the specified type and its base classes.
        /// </summary>
        /// <param name="type">The type from which the search begins.</param>
        /// <param name="name">The exact name of the field to locate.</param>
        /// <returns>The first matching field found in the inheritance hierarchy, or null if no matching field exists.</returns>
        public static FieldInfo FindField(Type type, string name)
        {
            // Search one inheritance level at a time so a field declared on a derived type takes precedence over a field with the same name on a base type.
            for (var t = type; t != null; t = t.BaseType)
            {
                var field = t.GetField(name, AllMembers | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }

            return null;
        }

        /// <summary>
        /// Gets the element type of an array or a generic IList implementation.
        /// </summary>
        /// <param name="type">The array or collection type to inspect.</param>
        /// <returns>The element type when it can be determined; otherwise, null.</returns>
        public static Type GetElementType(Type type)
        {
            if (type == null) return null;
            if (type.IsArray) return type.GetElementType();
            if (type.IsGenericType && typeof(IList).IsAssignableFrom(type)) return type.GetGenericArguments()[0];
            return null;
        }

        /// <summary>
        /// Resolves a Unity serialized property path to its corresponding field.
        /// </summary>
        /// <param name="property">The serialized property whose backing field is required.</param>
        /// <returns>The resolved field, or null if the target or any path segment cannot be resolved.</returns>
        /// <remarks>Collection indices are normalized before lookup. This means the cache stores a reusable field definition rather than a specific collection element.</remarks>
        public static FieldInfo GetFieldInfo(SerializedProperty property)
        {
            var target = property.serializedObject.targetObject;
            if (target == null) return null;

            Type root = target.GetType();

            // Normalize collection indices because each element uses the same field type.
            string path = ArrayIndexRegex.Replace(property.propertyPath, "[]");
            var key = (root, path);

            if (fieldCache.TryGetValue(key, out var cached)) return cached;

            Type current = root;
            FieldInfo field = null;

            foreach (var token in path.Split('.'))
            {
                bool isElement = token.EndsWith("[]", StringComparison.Ordinal);
                string name = isElement ? token[..^2] : token;

                field = FindField(current, name);
                if (field == null) break;

                current = field.FieldType;
                if (isElement)
                {
                    // Move from the collection field to the element type before resolving the next path segment. If the collection type is unsupported, the remaining path cannot be resolved safely.
                    current = GetElementType(current);
                    if (current == null)
                    {
                        field = null;
                        break;
                    }
                }
            }

            // Cache unsuccessful resolutions too, avoiding repeated reflection for property paths that cannot be mapped to a field.
            fieldCache[key] = field;
            return field;
        }

        #endregion

        #region === Owner Instance Resolution ===

        /// <summary>
        /// Resolves the object that owns the final member in a serialized property path.
        /// </summary>
        /// <param name="target">The root Unity object containing the serialized data.</param>
        /// <param name="propertyPath">The Unity serialized property path to traverse.</param>
        /// <returns>The object that owns the final path segment, or null if traversal fails.</returns>
        /// <remarks>The final path segment is intentionally not evaluated: the returned object is the owner on which that member is declared, rather than the member's value.</remarks>
        public static object GetOwner(Object target, string propertyPath)
        {
            object current = target;

            // Convert Unity's collection notation into a form Step can parse.
            string[] tokens = propertyPath.Replace(".Array.data[", "[").Split('.');
            for (int i = 0; i < tokens.Length - 1 && current != null; i++) current = Step(current, tokens[i]);
            return current;
        }

        /// <summary>
        /// Resolves one field segment, optionally indexing into a collection.
        /// </summary>
        /// <param name="obj">The current object in the traversal.</param>
        /// <param name="token">A field name, optionally followed by a collection index.</param>
        /// <returns>The resolved field value or collection element, or null if the segment is invalid.</returns>
        private static object Step(object obj, string token)
        {
            int bracket = token.IndexOf('[');
            string name = bracket >= 0 ? token[..bracket] : token;

            var field = FindField(obj.GetType(), name);
            if (field == null) return null;

            object value = field.GetValue(obj);

            if (bracket >= 0)
            {
                // A bracketed segment must refer to an IList with a valid numeric index.
                // Return null instead of indexing an incompatible or out-of-range collection.
                if (value is not IList list) return null;
                if (!int.TryParse(token.Substring(bracket + 1, token.Length - bracket - 2), out int index)) return null;
                if (index < 0 || index >= list.Count) return null;
                value = list[index];
            }

            return value;
        }

        #endregion

        #region === Sibling Property Resolution ===

        /// <summary>
        /// Finds a property relative to the same parent, falling back to the root object.
        /// </summary>
        /// <param name="property">The property whose sibling should be located.</param>
        /// <param name="name">The name of the sibling property.</param>
        /// <returns>The matching serialized property, or null if it cannot be found.</returns>
        /// <remarks>
        /// For properties inside a collection element, the element index is removed
        /// before determining the parent path. This allows the lookup to target a
        /// sibling field on the containing object rather than inside the element itself.
        /// </remarks>
        public static SerializedProperty FindSibling(SerializedProperty property, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            string parent = property.propertyPath;

            if (parent.EndsWith("]", StringComparison.Ordinal))
            {
                int arrayIndex = parent.LastIndexOf(".Array.data[", StringComparison.Ordinal);
                if (arrayIndex >= 0) parent = parent[..arrayIndex];
            }

            int dot = parent.LastIndexOf('.');
            if (dot >= 0)
            {
                var relative = property.serializedObject.FindProperty(parent[..(dot + 1)] + name);
                if (relative != null) return relative;
            }

            // If no sibling exists at the relative path, attempt a root-level lookup.
            return property.serializedObject.FindProperty(name);
        }

        #endregion

        #region === Member Lookup by Name ===

        /// <summary>
        /// Reads a field, readable property, or parameterless method by name.
        /// </summary>
        /// <param name="owner">The object whose type hierarchy will be searched.</param>
        /// <param name="name">The name of the member to retrieve.</param>
        /// <param name="value">Receives the retrieved value when the lookup succeeds.</param>
        /// <returns>True if a supported member was found and evaluated; otherwise, false.</returns>
        /// <remarks>
        /// Each inheritance level is checked from derived to base. Fields are preferred
        /// over properties, and properties over methods at the same level. Methods must
        /// have no parameters and return a value; indexed properties are excluded because
        /// they cannot be read without supplying index arguments.
        /// </remarks>
        public static bool TryGetMember(object owner, string name, out object value)
        {
            value = null;
            if (owner == null || string.IsNullOrEmpty(name)) return false;

            const BindingFlags flags = AllMembers | BindingFlags.DeclaredOnly;

            try
            {
                for (var t = owner.GetType(); t != null; t = t.BaseType)
                {
                    var field = t.GetField(name, flags);
                    if (field != null)
                    {
                        value = field.GetValue(field.IsStatic ? null : owner);
                        return true;
                    }

                    var property = t.GetProperty(name, flags);
                    if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
                    {
                        value = property.GetValue(property.GetMethod.IsStatic ? null : owner);
                        return true;
                    }

                    var method = t.GetMethod(name, flags, null, Type.EmptyTypes, null);
                    if (method != null && method.ReturnType != typeof(void))
                    {
                        value = method.Invoke(method.IsStatic ? null : owner, null);
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : e);
            }

            return false;
        }

        /// <summary>
        /// Attempts to retrieve a named member and convert the result through type matching.
        /// </summary>
        /// <typeparam name="T">The expected result type.</typeparam>
        /// <param name="owner">The object whose member should be read.</param>
        /// <param name="name">The name of the field, property, or method.</param>
        /// <param name="result">Receives the value when it is compatible with T.</param>
        /// <returns>True if the member was successfully retrieved and its value is of type T; otherwise, false.</returns>
        public static bool TryRead<T>(object owner, string name, out T result)
        {
            result = default;
            if (TryGetMember(owner, name, out object value) && value is T typed)
            {
                result = typed;
                return true;
            }
            return false;
        }

        #endregion

        #region === Callback Invocation ===

        /// <summary>
        /// Invokes a named callback on an object, optionally passing the current field value.
        /// </summary>
        /// <param name="owner">The object that owns the callback.</param>
        /// <param name="field">The field whose value may be passed to the callback.</param>
        /// <param name="methodName">The name of the callback method.</param>
        /// <remarks>
        /// The search accepts methods with zero or one parameter and stops at the first
        /// matching method in the inheritance hierarchy. If one parameter is present,
        /// the field's current value is passed as the sole argument.
        /// </remarks>
        public static void InvokeCallback(object owner, FieldInfo field, string methodName)
        {
            MethodInfo method = null;

            for (Type t = owner.GetType(); t != null && method == null; t = t.BaseType)
            {
                foreach (var candidate in t.GetMethods(AllMembers | BindingFlags.DeclaredOnly))
                {
                    if (candidate.Name == methodName && candidate.GetParameters().Length <= 1)
                    {
                        method = candidate;
                        break;
                    }
                }
            }

            if (method == null)
            {
                Debug.LogWarning($"[OnValueChanged] Method '{methodName}' (0 or 1 parameter) not found on {owner.GetType().Name}.");
                return;
            }

            try
            {
                // Supply the field value only when the selected callback declares one parameter.
                var args = method.GetParameters().Length == 1 ? new[] { field.GetValue(owner) } : null;
                method.Invoke(method.IsStatic ? null : owner, args);
            }
            catch (Exception e)
            {
                Debug.LogException(e is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : e);
            }
        }

        #endregion
    }
}