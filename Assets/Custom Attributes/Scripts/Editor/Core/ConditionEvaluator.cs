/*
 * ---------------------------------------------------------------------------
 * Description: Evaluates conditional attributes by resolving serialized properties,
 *              reflected members, logical operators, negation, and comparison values.
 *
 * Author: Lucas Gomes Cecchini
 * Pseudonym: AGAMENOM
 * ---------------------------------------------------------------------------
 */

using System.Collections;
using UnityEditor;
using System;

using Object = UnityEngine.Object;

namespace CustomAttributes.Editing
{
    /// <summary>
    /// Evaluates conditions defined by Custom Attributes against serialized properties and reflected members.
    /// </summary>
    internal static class ConditionEvaluator
    {
        #region === Condition Evaluation ===

        /// <summary>
        /// Evaluates all conditions defined by a conditional attribute using its configured logical operator.
        /// </summary>
        /// <param name="property">The serialized property whose owner and sibling properties provide the condition context.</param>
        /// <param name="attribute">The attribute containing the condition expressions, logical operator, and optional comparison value.</param>
        /// <param name="passes">Receives the combined evaluation result when all conditions are evaluated successfully.</param>
        /// <param name="error">Receives the error message when a condition cannot be evaluated; otherwise, null.</param>
        /// <returns><see langword="true"/> when every condition can be evaluated successfully; otherwise, <see langword="false"/>.</returns>
        public static bool TryEvaluate(SerializedProperty property, ConditionAttribute attribute, out bool passes, out string error)
        {
            // Initialize the outputs so callers receive predictable values even when evaluation fails.
            passes = true;
            error = null;

            // AND starts with true because every condition must pass; OR starts with false because any passing condition is sufficient.
            bool isAnd = attribute.Logic == ConditionLogic.And;
            bool result = isAnd;

            foreach (var raw in attribute.Conditions)
            {
                // Stop at the first invalid condition instead of reporting a potentially misleading combined result.
                if (!TryEvaluateOne(property, raw, attribute, out bool value, out error)) return false;
                result = isAnd ? (result && value) : (result || value);
            }

            passes = result;
            return true;
        }

        /// <summary>
        /// Resolves and evaluates a single condition expression, including optional negation.
        /// </summary>
        /// <param name="property">The serialized property providing the context for resolving sibling properties and their owner.</param>
        /// <param name="raw">The original condition expression, optionally prefixed with an exclamation mark.</param>
        /// <param name="attribute">The attribute supplying comparison settings for the condition.</param>
        /// <param name="value">Receives the evaluated condition result, including negation when specified.</param>
        /// <param name="error">Receives an error message if the expression is invalid or cannot be resolved.</param>
        /// <returns><see langword="true"/> when the expression is resolved and evaluated successfully; otherwise, <see langword="false"/>.</returns>
        private static bool TryEvaluateOne(SerializedProperty property, string raw, ConditionAttribute attribute, out bool value, out string error)
        {
            // Default to false so an unsuccessful evaluation never accidentally appears to satisfy a condition.
            value = false;
            error = null;

            if (string.IsNullOrWhiteSpace(raw))
            {
                error = "Empty condition name.";
                return false;
            }

            // Normalize whitespace before parsing negation so expressions such as " ! IsReady " are handled consistently.
            string trimmed = raw.Trim();
            bool negate = trimmed[0] == '!';
            string name = negate ? trimmed[1..].Trim() : trimmed;
            bool result;

            // Prefer a sibling serialized property because it provides Unity's current serialized value directly.
            var sibling = PropertyReflection.FindSibling(property, name);
            if (sibling != null)
            {
                // Serialized properties support only the value types explicitly handled by TryEvaluateProperty.
                if (!TryEvaluateProperty(sibling, attribute, out result))
                {
                    error = $"Condition '{name}': property type {sibling.propertyType} is not supported.";
                    return false;
                }
            }
            else
            {
                // If no sibling property exists, resolve the member on the object that owns the inspected property.
                var owner = PropertyReflection.GetOwner(property.serializedObject.targetObject, property.propertyPath);
                if (!PropertyReflection.TryGetMember(owner, name, out var memberValue))
                {
                    error = $"Condition '{name}' was not found (field, property or method).";
                    return false;
                }

                // Reflected members use the same truthiness and comparison rules as other plain CLR values.
                result = EvaluateValue(memberValue, attribute);
            }

            // Apply negation after evaluating the underlying condition so both resolution paths behave identically.
            value = negate ? !result : result;
            return true;
        }

        #endregion

        #region === Serialized Properties ===

        /// <summary>
        /// Evaluates a supported serialized property using its current value and optional comparison value.
        /// </summary>
        /// <param name="p">The serialized property whose value should be evaluated.</param>
        /// <param name="attribute">The attribute providing the optional comparison value.</param>
        /// <param name="result">Receives the resulting Boolean condition value.</param>
        /// <returns><see langword="true"/> when the property's type is supported; otherwise, <see langword="false"/>.</returns>
        private static bool TryEvaluateProperty(SerializedProperty p, ConditionAttribute attribute, out bool result)
        {
            // Cache comparison settings because all supported property types use the same comparison configuration.
            bool hasCompare = attribute.HasCompareValue;
            object compare = attribute.CompareValue;
            result = false;

            switch (p.propertyType)
            {
                // Without an explicit comparison, a Boolean condition uses its actual value.
                case SerializedPropertyType.Boolean:
                    result = hasCompare ? CompareEquals(p.boolValue, compare) : p.boolValue;
                    return true;

                // Treat enum values as numeric flags here: zero is false, while any nonzero underlying value is true.
                case SerializedPropertyType.Enum:
                    result = hasCompare ? CompareEquals(p.enumValueFlag, compare) : p.enumValueFlag != 0;
                    return true;

                // Integer-based conditions are false at zero unless an explicit comparison defines another requirement.
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                    result = hasCompare ? CompareEquals(p.intValue, compare) : p.intValue != 0;
                    return true;

                // Use a small-value check instead of exact equality with zero for floating-point truthiness.
                case SerializedPropertyType.Float:
                    result = hasCompare ? CompareEquals(p.floatValue, compare) : Math.Abs(p.floatValue) > float.Epsilon;
                    return true;

                // An empty or null string is false when no explicit comparison value is supplied.
                case SerializedPropertyType.String:
                    result = hasCompare ? CompareEquals(p.stringValue, compare) : !string.IsNullOrEmpty(p.stringValue);
                    return true;

                // Unity object references use Unity's null semantics, which also account for destroyed objects.
                case SerializedPropertyType.ObjectReference:
                    result = hasCompare ? CompareEquals(p.objectReferenceValue, compare) : p.objectReferenceValue != null;
                    return true;

                default:
                    return false;
            }
        }

        #endregion

        #region === Plain Values ===

        /// <summary>
        /// Evaluates a reflected or otherwise unboxed value using the attribute's comparison or truthiness rules.
        /// </summary>
        /// <param name="value">The value obtained from a reflected field, property, or method.</param>
        /// <param name="attribute">The attribute supplying the optional comparison value.</param>
        /// <returns>The Boolean result of the comparison or truthiness evaluation.</returns>
        private static bool EvaluateValue(object value, ConditionAttribute attribute)
        {
            if (attribute.HasCompareValue) return CompareEquals(value, attribute.CompareValue);
            return IsTruthy(value);
        }

        /// <summary>
        /// Determines whether a plain CLR value should be interpreted as true.
        /// </summary>
        /// <param name="value">The value to interpret.</param>
        /// <returns><see langword="true"/> when the value satisfies its type-specific truthiness rule; otherwise, <see langword="false"/>.</returns>
        private static bool IsTruthy(object value)
        {
            // Handle common types explicitly because their intended truthiness differs from general object existence.
            switch (value)
            {
                case null: return false;
                case bool b: return b;
                case string s: return s.Length > 0;
                case Object o: return o != null;
                case Enum e: return Convert.ToInt64(e) != 0;
                case ICollection c: return c.Count > 0;
            }

            // Numeric types are evaluated by magnitude; values close enough to zero are treated as false.
            if (TryToDouble(value, out double number)) return Math.Abs(number) > double.Epsilon;
            return true;
        }

        /// <summary>
        /// Compares two values using null-aware, Boolean, string, and numeric comparison rules.
        /// </summary>
        /// <param name="a">The first value to compare.</param>
        /// <param name="b">The second value to compare.</param>
        /// <returns><see langword="true"/> when the values are considered equal; otherwise, <see langword="false"/>.</returns>
        private static bool CompareEquals(object a, object b)
        {
            // Unity objects can compare equal to null after destruction even when their managed wrappers still exist.
            bool aNull = a == null || (a is Object ua && ua == null);
            bool bNull = b == null || (b is Object ub && ub == null);

            // Normalize ordinary null and Unity's destroyed-object null semantics before comparing the values themselves.
            if (aNull || bNull) return aNull && bNull;
            if (a is bool boolA && b is bool boolB) return boolA == boolB;

            // If either operand is a string, use ordinary equality rather than converting numeric-looking text to a number.
            if (a is string || b is string) return Equals(a, b);

            // Compare numeric-compatible values using a tolerance to avoid relying on exact floating-point equality.
            if (TryToDouble(a, out double numberA) && TryToDouble(b, out double numberB)) return Math.Abs(numberA - numberB) < 1e-6;

            // Fall back to the values' standard equality implementation for all remaining types.
            return Equals(a, b);
        }

        /// <summary>
        /// Attempts to convert a non-string, non-Boolean value to a double.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="number">Receives the converted number when conversion succeeds, or zero otherwise.</param>
        /// <returns><see langword="true"/> when the value supports a successful numeric conversion; otherwise, <see langword="false"/>.</returns>
        private static bool TryToDouble(object value, out double number)
        {
            number = 0;

            // Strings and Booleans have separate comparison semantics and must not be treated as numeric values.
            if (value == null || value is string || value is bool) return false;

            try
            {
                // IConvertible supports the standard CLR numeric types and other compatible convertible values.
                if (value is IConvertible convertible)
                {
                    number = convertible.ToDouble(null);
                    return true;
                }
            }
            catch (Exception)
            {
                // Conversion can fail for incompatible values or custom IConvertible implementations; let the caller use its fallback.
            }
            return false;
        }

        #endregion
    }
}