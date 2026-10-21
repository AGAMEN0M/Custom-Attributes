/*
 * ---------------------------------------------------------------------------
 * Description: Provides custom Unity Inspectors that render Custom Attributes metadata,
 *              conditional fields, grouped properties, callbacks, buttons, drag-and-drop list areas,
 *              and Inspector-only members.
 *
 * Author: Lucas Gomes Cecchini
 * Pseudonym: AGAMENOM
 * ---------------------------------------------------------------------------
 */

using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEditor;
using System;

using Object = UnityEngine.Object;

namespace CustomAttributes.Editing
{
    /// <summary>
    /// Provides the custom Inspector for MonoBehaviour-derived components.
    /// </summary>
    [CanEditMultipleObjects]
    [CustomEditor(typeof(MonoBehaviour), true)]
    public class MonoBehaviourInspector : AttributeInspector { }

    /// <summary>
    /// Provides the custom Inspector for ScriptableObject-derived assets.
    /// </summary>
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ScriptableObject), true)]
    public class ScriptableObjectInspector : AttributeInspector { }

    /// <summary>
    /// Draws Custom Attributes metadata and custom members in the Unity Inspector.
    /// </summary>
    public abstract class AttributeInspector : Editor
    {
        #region === Inspector Lifecycle and State ===

        private TypeFeatures features; // Cache the target type's metadata so reflection-based attribute discovery is not repeated during every Inspector repaint.
        private readonly Dictionary<MethodInfo, object[]> buttonArgs = new(); // Retain editable argument values for each button method between Inspector redraws.

        /// <summary>
        /// Initializes the Inspector's cached metadata when the Editor enables this instance.
        /// </summary>
        protected virtual void OnEnable()
        {
            // A target may be unavailable during certain Editor lifecycle transitions, so only build metadata when one exists.
            if (target != null) features = TypeFeatures.For(target.GetType());
        }

        /// <summary>
        /// Determines whether the Inspector requires continuous repainting while the application is playing.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> when the application is playing and at least one Inspector-only member is available; otherwise, <see langword="false"/>.
        /// </returns>
        public override bool RequiresConstantRepaint() => Application.isPlaying && features != null && features.ShowMembers.Length > 0;

        /// <summary>
        /// Draws the default Inspector or the custom fields, buttons, and Inspector-only members.
        /// </summary>
        public override void OnInspectorGUI()
        {
            // Preserve Unity's default Inspector whenever the target has no metadata requiring custom rendering.
            if (features == null || !features.HasAny)
            {
                base.OnInspectorGUI();
                return;
            }

            // Synchronize the serialized representation before reading or drawing field values.
            serializedObject.UpdateIfRequiredOrScript();

            // Keep button placement consistent, with field controls and Inspector-only members between the two button groups.
            DrawButtons(ButtonPosition.Top);
            DrawFields();
            DrawShowInInspectorMembers();
            DrawButtons(ButtonPosition.Bottom);

            // Commit serialized field changes after the custom Inspector has finished drawing.
            serializedObject.ApplyModifiedProperties();
        }

        #endregion

        #region === Fields ===

        /// <summary>
        /// Collects serialized fields and draws them individually or within foldout groups.
        /// </summary>
        private void DrawFields()
        {
            // Snapshot each property and its group metadata before drawing; SerializedProperty iterators are reused while traversing.
            var entries = new List<(SerializedProperty prop, FoldoutGroupAttribute group)>();

            // Traverse visible serialized properties to preserve Unity's normal Inspector field order and visibility rules.
            var iterator = serializedObject.GetIterator();
            for (bool enter = true; iterator.NextVisible(enter); enter = false)
            {
                var copy = iterator.Copy(); // Copy the current property because subsequent iterator movement would otherwise change the property being stored.

                // Unity's script reference is not a user-defined field, so it has no corresponding reflected field metadata.
                var field = copy.propertyPath == "m_Script" ? null : PropertyReflection.GetFieldInfo(copy);
                entries.Add((copy, MetaInfo.For(field).Group));
            }

            // Track group names rather than attributes so each named foldout is rendered only once.
            var drawnGroups = new HashSet<string>();
            foreach (var (prop, group) in entries)
            {
                // Ungrouped fields retain their original position in the serialized property sequence.
                if (group == null)
                {
                    DrawProperty(prop);
                    continue;
                }

                // The first encountered member determines where its group is drawn in the Inspector.
                if (!drawnGroups.Add(group.GroupName)) continue;

                // Gather all properties with this group name so fields in the same group appear together.
                var members = entries.Where(e => e.group != null && e.group.GroupName == group.GroupName).Select(e => e.prop).ToList();
                DrawGroup(group, members);
            }
        }

        /// <summary>
        /// Draws a foldout group when at least one of its fields passes the visibility conditions.
        /// </summary>
        /// <param name="group">The attribute defining the foldout group's name and default expansion state.</param>
        /// <param name="members">The serialized properties assigned to this group.</param>
        private void DrawGroup(FoldoutGroupAttribute group, List<SerializedProperty> members)
        {
            // Do not display an empty group when every member is hidden by its conditional metadata.
            if (!members.Any(IsVisible)) return;

            // Include the inspected type in the key so identically named groups on different types keep separate states.
            string key = $"CustomAttributes.Group.{target.GetType().FullName}.{group.GroupName}";

            // SessionState preserves the foldout state across Inspector redraws without adding it to the saved asset or scene.
            bool open = SessionState.GetBool(key, group.DefaultExpanded);
            open = EditorGUILayout.BeginFoldoutHeaderGroup(open, group.GroupName);

            // Persist the latest user selection so the foldout does not reset during subsequent redraws.
            SessionState.SetBool(key, open);

            if (open)
            {
                EditorGUI.indentLevel++;
                foreach (var member in members) DrawProperty(member);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        /// <summary>
        /// Determines whether a serialized property passes its visibility conditions.
        /// </summary>
        /// <param name="prop">The serialized property whose conditional metadata must be evaluated.</param>
        /// <returns><see langword="true"/> when the property is visible; otherwise, <see langword="false"/>.</returns>
        private static bool IsVisible(SerializedProperty prop)
        {
            var field = PropertyReflection.GetFieldInfo(prop);
            Evaluate(prop, MetaInfo.For(field), out bool visible, out _, out _);
            return visible;
        }

        /// <summary>
        /// Evaluates the conditions that control a property's visibility and editability.
        /// </summary>
        /// <param name="prop">The serialized property being evaluated.</param>
        /// <param name="meta">The metadata containing the property's conditional attributes.</param>
        /// <param name="visible">Receives whether the property should be displayed.</param>
        /// <param name="enabled">Receives whether the property should be editable.</param>
        /// <param name="error">Receives the first condition-evaluation error, if one occurs.</param>
        private static void Evaluate(SerializedProperty prop, MetaInfo meta, out bool visible, out bool enabled, out string error)
        {
            // Conditions are permissive by default; attributes can restrict visibility or editability.
            visible = true;
            enabled = true;
            error = null;

            foreach (var condition in meta.Conditions)
            {
                // A failed evaluation is different from a condition that evaluates successfully to false.
                // Preserve the first error for display while allowing the remaining conditions to be evaluated.
                if (!ConditionEvaluator.TryEvaluate(prop, condition, out bool passes, out string message))
                {
                    error ??= message;
                    continue;
                }

                // Combine conditions cumulatively so any restrictive condition can hide or disable the property.
                switch (condition.Effect)
                {
                    case ConditionEffect.Show: visible &= passes; break;
                    case ConditionEffect.Hide: visible &= !passes; break;
                    case ConditionEffect.Enable: enabled &= passes; break;
                    case ConditionEffect.Disable: enabled &= !passes; break;
                }
            }
        }

        /// <summary>
        /// Draws a serialized property while applying its metadata, validation, and callbacks.
        /// </summary>
        /// <param name="prop">The serialized property to draw.</param>
        private void DrawProperty(SerializedProperty prop)
        {
            // Keep Unity's script reference visible but prevent users from changing the script assigned to the component or asset.
            if (prop.propertyPath == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(prop);
                return;
            }

            var field = PropertyReflection.GetFieldInfo(prop);
            var meta = MetaInfo.For(field);

            // Resolve all conditional attributes before drawing so hidden fields do not render their controls or warnings.
            Evaluate(prop, meta, out bool visible, out bool enabled, out string error);
            if (!visible) return;
            if (meta.AutoRef != null) TryAutoReference(prop, field, meta.AutoRef);
            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);

            // Validate required values before selecting the field appearance and drawing its control.
            bool missing = meta.Required != null && IsEmpty(prop);
            if (missing) EditorGUILayout.HelpBox(meta.Required.Message ?? $"'{prop.displayName}' is required.", MessageType.Error);
            if (meta.ReadOnly != null && IsReadOnlyNow(meta.ReadOnly.Mode)) enabled = false;

            // Preserve the current GUI tint so this field does not affect subsequent controls.
            Color previousColor = GUI.color;
            if (missing) GUI.color = new Color(1f, 0.55f, 0.55f);
            else if (meta.Color != null) GUI.color = meta.Color.Color;

            EditorGUI.BeginChangeCheck();

            using (new EditorGUI.DisabledScope(!enabled))
            {
                // Draw the path tool separately because it provides a Scene View interaction rather than a serialized field value.
                if (meta.Path != null) DrawPathToggle(prop, meta.Path);

                // Draw the drop area above the list so objects can be dragged in without opening the list's elements.
                if (meta.DragDrop != null) DragAndDropArea.Draw(prop, field, meta.DragDrop);

                // Recurse into eligible serializable objects so their nested fields can use the same metadata pipeline.
                if (ShouldRecurse(prop, field)) DrawNested(prop);
                else EditorGUILayout.PropertyField(prop, true);
            }

            // Capture the change state before restoring the GUI color to avoid leaking presentation changes to other controls.
            bool changed = EditorGUI.EndChangeCheck();
            GUI.color = previousColor;

            // Invoke callbacks only when Unity reports an actual GUI change for this property.
            if (changed && meta.OnChanged.Length > 0) InvokeOnChanged(prop, field, meta.OnChanged);
        }

        /// <summary>
        /// Determines whether a serialized object should be drawn recursively to support nested metadata.
        /// </summary>
        /// <param name="prop">The serialized property being inspected.</param>
        /// <param name="field">The reflected field corresponding to the property.</param>
        /// <returns>
        /// <see langword="true"/> when the property is eligible for recursive drawing and its nested type contains metadata;
        /// otherwise, <see langword="false"/>.
        /// </returns>
        private static bool ShouldRecurse(SerializedProperty prop, FieldInfo field) => field != null && prop.propertyType == SerializedPropertyType.Generic && !prop.isArray && !MetaInfo.For(field).HasPropertyAttribute && TypeFeatures.NestedHasMeta(field.FieldType);

        /// <summary>
        /// Draws the visible children of an expandable serialized property.
        /// </summary>
        /// <param name="prop">The parent serialized property whose children should be drawn.</param>
        private void DrawNested(SerializedProperty prop)
        {
            // Use a foldout for the parent so nested controls can be expanded only when needed.
            prop.isExpanded = EditorGUILayout.Foldout(prop.isExpanded, prop.displayName, true);
            if (!prop.isExpanded) return;

            EditorGUI.indentLevel++;

            // Use an independent iterator and record the subtree boundary to avoid modifying the parent property traversal.
            var child = prop.Copy();
            var end = prop.GetEndProperty();

            // Enter the first child once, then continue through visible descendants until the parent subtree ends.
            bool enter = true;
            while (child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end))
            {
                enter = false;
                DrawProperty(child.Copy()); // Copy each child before passing it onward because nested drawing can perform additional iterator operations.
            }

            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// Determines whether a serialized property currently has an empty value.
        /// </summary>
        /// <param name="prop">The serialized property whose value should be checked.</param>
        /// <returns><see langword="true"/> when the property matches a supported empty-value case; otherwise, <see langword="false"/>.</returns>
        private static bool IsEmpty(SerializedProperty prop) => prop.propertyType switch
        {
            SerializedPropertyType.ObjectReference => prop.objectReferenceValue == null,
            SerializedPropertyType.String => string.IsNullOrEmpty(prop.stringValue),
            SerializedPropertyType.ManagedReference => string.IsNullOrEmpty(prop.managedReferenceFullTypename),
            _ => prop.isArray && prop.arraySize == 0
        };

        /// <summary>
        /// Determines whether the configured read-only mode is active.
        /// </summary>
        /// <param name="mode">The read-only mode configured by the attribute.</param>
        /// <returns><see langword="true"/> when the property should be read-only in the current application state; otherwise, <see langword="false"/>.</returns>
        private static bool IsReadOnlyNow(ReadOnlyMode mode) => mode switch
        {
            ReadOnlyMode.WhenPlaying => Application.isPlaying,
            ReadOnlyMode.WhenEditing => !Application.isPlaying,
            _ => true
        };

        /// <summary>
        /// Attempts to assign a missing component reference while editing a single target.
        /// </summary>
        /// <param name="prop">The serialized object-reference property to populate.</param>
        /// <param name="field">The reflected field that defines the expected component type.</param>
        /// <param name="attribute">The attribute specifying where and how to search for the component.</param>
        private void TryAutoReference(SerializedProperty prop, FieldInfo field, AutoReferenceAttribute attribute)
        {
            // Avoid implicit assignments during Play Mode or multi-object editing, where automatic changes could be surprising or ambiguous.
            if (Application.isPlaying || targets.Length != 1) return;

            // Only populate an unassigned Unity object reference on a Component target.
            if (prop.propertyType != SerializedPropertyType.ObjectReference || prop.objectReferenceValue != null) return;
            if (target is not Component component) return;

            // Select the search scope from the attribute while using the field type as the required component type.
            var type = field.FieldType;
            var found = attribute.Source switch
            {
                AutoReferenceSource.Children => component.GetComponentInChildren(type, attribute.IncludeInactive),
                AutoReferenceSource.Parent => component.GetComponentInParent(type, attribute.IncludeInactive),
                _ => component.GetComponent(type)
            };

            // Leave existing assignments untouched and only write a reference when a matching component is found.
            if (found != null) prop.objectReferenceValue = found;
        }

        /// <summary>
        /// Applies pending serialized changes and invokes the configured callbacks for affected targets.
        /// </summary>
        /// <param name="prop">The changed serialized property.</param>
        /// <param name="field">The reflected field associated with the property.</param>
        /// <param name="callbacks">The callbacks configured to run when the value changes.</param>
        private void InvokeOnChanged(SerializedProperty prop, FieldInfo field, OnValueChangedAttribute[] callbacks)
        {
            string path = prop.propertyPath;
            serializedObject.ApplyModifiedProperties();

            foreach (var t in targets)
            {
                // Resolve the actual object that owns the field, including nested serializable objects.
                object owner = PropertyReflection.GetOwner(t, path);
                if (owner == null) continue;

                // Multiple callback attributes can be attached to the same field, so invoke each configured method.
                foreach (var callback in callbacks)
                {
                    PropertyReflection.InvokeCallback(owner, field, callback.MethodName);
                }
            }

            serializedObject.Update();
        }

        /// <summary>
        /// Draws the control that activates or deactivates a path gizmo for a property.
        /// </summary>
        /// <param name="prop">The serialized property associated with the path tool.</param>
        /// <param name="attribute">The attribute defining the path visualization behavior.</param>
        private void DrawPathToggle(SerializedProperty prop, GizmoPathAttribute attribute)
        {
            // Path gizmos require a scene component; assets and other target types cannot provide a component transform.
            if (target is not Component) return;
            bool active = GizmoSession.IsActive(prop, PathTool.ToolId);

            // Reserve a compact control beside the property's associated path tooling.
            Rect row = EditorGUILayout.GetControlRect(false, 20f);
            Rect button = new(row.x, row.y, 34f, 20f);

            // Toggle the session state only when the user changes the current button value.
            if (GUI.Toggle(button, active, GizmoIcons.Path, EditorStyles.miniButton) != active) GizmoSession.Toggle(prop, new PathTool(attribute));
        }

        #endregion

        #region === ShowInInspector ===

        /// <summary>
        /// Draws members marked for display in the Inspector.
        /// </summary>
        private void DrawShowInInspectorMembers()
        {
            // Avoid adding spacing or drawing a section when no members have been registered for display.
            if (features.ShowMembers.Length == 0) return;

            bool headerDrawn = false;

            foreach (var member in features.ShowMembers)
            {
                var attribute = member.GetCustomAttribute<ShowInInspectorAttribute>();
                if (attribute.OnlyInPlayMode && !Application.isPlaying) continue; // Members restricted to Play Mode are omitted entirely while editing.

                // Add separation only when the first eligible member is about to be drawn.
                if (!headerDrawn)
                {
                    EditorGUILayout.Space();
                    headerDrawn = true;
                }

                object value;
                Type type;

                // Reflection can fail when a getter throws or a member is inaccessible; report the error without breaking the entire Inspector.
                try
                {
                    if (member is FieldInfo field)
                    {
                        // Static members belong to the type itself and therefore do not use the inspected target as their owner.
                        value = field.GetValue(field.IsStatic ? null : target);
                        type = field.FieldType;
                    }
                    else
                    {
                        // Use the getter's static status to determine whether the property needs a target instance.
                        var property = (PropertyInfo)member;
                        value = property.GetValue(property.GetMethod.IsStatic ? null : target);
                        type = property.PropertyType;
                    }
                }
                catch (Exception e)
                {
                    // Unwrap reflection invocation errors when possible to expose the underlying getter exception.
                    EditorGUILayout.HelpBox(e.InnerException?.Message ?? e.Message, MessageType.Error);
                    continue;
                }

                // These members are displayed for inspection only; prevent edits through their rendered controls.
                using (new EditorGUI.DisabledScope(true))
                {
                    DrawValue(attribute.Label ?? ObjectNames.NicifyVariableName(member.Name), value, type);
                }
            }
        }

        /// <summary>
        /// Draws a read-only Inspector control appropriate for the supplied value type.
        /// </summary>
        /// <param name="label">The label displayed beside the value.</param>
        /// <param name="value">The current value returned by the reflected member.</param>
        /// <param name="type">The declared type of the reflected member.</param>
        private static void DrawValue(string label, object value, Type type)
        {
            // Use native controls for common types so values retain familiar Unity Inspector formatting.
            if (type == typeof(int)) EditorGUILayout.IntField(label, (int)value);
            else if (type == typeof(float)) EditorGUILayout.FloatField(label, (float)value);
            else if (type == typeof(double)) EditorGUILayout.DoubleField(label, (double)value);
            else if (type == typeof(bool)) EditorGUILayout.Toggle(label, (bool)value);
            else if (type == typeof(string)) EditorGUILayout.TextField(label, (string)value);
            else if (type == typeof(Vector2)) EditorGUILayout.Vector2Field(label, (Vector2)value);
            else if (type == typeof(Vector3)) EditorGUILayout.Vector3Field(label, (Vector3)value);
            else if (type == typeof(Color)) EditorGUILayout.ColorField(label, (Color)value);
            else if (type.IsEnum) EditorGUILayout.EnumPopup(label, (Enum)value);
            else if (typeof(Object).IsAssignableFrom(type)) EditorGUILayout.ObjectField(label, value as Object, type, true);
            else if (value is ICollection collection) EditorGUILayout.LabelField(label, $"{collection.Count} items");
            else EditorGUILayout.LabelField(label, value != null ? value.ToString() : "null");
        }

        #endregion

        #region === Buttons ===

        /// <summary>
        /// Draws buttons assigned to the requested Inspector position.
        /// </summary>
        /// <param name="position">The Inspector position whose buttons should be drawn.</param>
        private void DrawButtons(ButtonPosition position)
        {
            // Preserve the metadata-defined button order while filtering by the requested position.
            foreach (var button in features.Buttons)
            {
                if (button.Attribute.Position == position) DrawButton(button);
            }
        }

        /// <summary>
        /// Draws a button and any supported parameter controls.
        /// </summary>
        /// <param name="button">The metadata describing the button method and its presentation options.</param>
        private void DrawButton(ButtonInfo button)
        {
            // Coroutines require Play Mode, regardless of a less restrictive mode configured on the attribute.
            var mode = button.IsCoroutine ? ButtonMode.PlayModeOnly : button.Attribute.Mode;
            bool enabled = button.SupportedParameters && (mode == ButtonMode.Always || (mode == ButtonMode.PlayModeOnly && Application.isPlaying) || (mode == ButtonMode.EditModeOnly && !Application.isPlaying));

            var args = GetArgs(button);
            bool hasParameters = button.Parameters.Length > 0;

            if (hasParameters)
            {
                // Group parameter controls with their button so the user can configure arguments before invoking the method.
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                if (!button.SupportedParameters)
                {
                    // Explain why the method cannot be invoked instead of presenting controls that cannot represent its arguments.
                    EditorGUILayout.HelpBox("This method has parameters of an unsupported type.", MessageType.Warning);
                }
                else
                {
                    // Keep the edited arguments in the cache so values survive subsequent Inspector repaints.
                    for (int i = 0; i < button.Parameters.Length; i++)
                    {
                        args[i] = ParameterField(button.Parameters[i], args[i]);
                    }
                }
            }

            bool clicked;
            using (new EditorGUI.DisabledScope(!enabled))
            {
                // Apply a custom height only when one was explicitly configured; otherwise use Unity's default button sizing.
                var options = button.Attribute.Height > 0f ? new[] { GUILayout.Height(button.Attribute.Height) } : Array.Empty<GUILayoutOption>();
                clicked = GUILayout.Button(button.Label, options);
            }

            // Close the optional parameter container before invoking the method, as invocation may exit the current GUI cycle.
            if (hasParameters) EditorGUILayout.EndVertical();
            if (clicked) Invoke(button, args);
        }

        /// <summary>
        /// Returns the cached argument values for a button, initializing defaults when needed.
        /// </summary>
        /// <param name="button">The button whose method arguments are requested.</param>
        /// <returns>The cached argument array for the method.</returns>
        private object[] GetArgs(ButtonInfo button)
        {
            if (buttonArgs.TryGetValue(button.Method, out object[] args)) return args;

            // Initialize arguments once so their values remain stable while the Inspector is repeatedly drawn.
            args = new object[button.Parameters.Length];
            for (int i = 0; i < args.Length; i++) args[i] = DefaultValue(button.Parameters[i]);

            buttonArgs[button.Method] = args;
            return args;
        }

        /// <summary>
        /// Returns the declared default value or a type-appropriate fallback for a method parameter.
        /// </summary>
        /// <param name="parameter">The reflected parameter whose initial value is required.</param>
        /// <returns>The parameter's declared default value or a fallback compatible with its type.</returns>
        private static object DefaultValue(ParameterInfo parameter)
        {
            var type = parameter.ParameterType;

            // Prefer an explicitly declared default, but reject reflection sentinel values that do not represent usable arguments.
            if (parameter.HasDefaultValue)
            {
                object value = parameter.DefaultValue;
                if (value != null && value is not DBNull && value is not Missing)
                {
                    // Reflection may expose enum defaults as their underlying numeric type, so convert them to the declared enum type.
                    if (type.IsEnum && !type.IsInstanceOfType(value)) return Enum.ToObject(type, value);
                    return value;
                }
            }

            // Use an empty string for string parameters, default-initialized values for value types, and null for reference types.
            if (type == typeof(string)) return string.Empty;
            if (type.IsValueType) return Activator.CreateInstance(type);
            return null;
        }

        /// <summary>
        /// Draws an editable parameter control and returns its current value.
        /// </summary>
        /// <param name="parameter">The reflected parameter whose value is being edited.</param>
        /// <param name="value">The current cached value for the parameter.</param>
        /// <returns>The updated value returned by the corresponding Inspector control, or the original value for unsupported types.</returns>
        private static object ParameterField(ParameterInfo parameter, object value)
        {
            // Convert the parameter name to Unity's Inspector label style for consistency with serialized fields.
            string label = ObjectNames.NicifyVariableName(parameter.Name);
            var type = parameter.ParameterType;

            // Use matching native controls for supported types so the returned object remains compatible with the method signature.
            if (type == typeof(int)) return EditorGUILayout.IntField(label, (int)value);
            if (type == typeof(float)) return EditorGUILayout.FloatField(label, (float)value);
            if (type == typeof(double)) return EditorGUILayout.DoubleField(label, (double)value);
            if (type == typeof(bool)) return EditorGUILayout.Toggle(label, (bool)value);
            if (type == typeof(string)) return EditorGUILayout.TextField(label, (string)value);
            if (type == typeof(Vector2)) return EditorGUILayout.Vector2Field(label, (Vector2)value);
            if (type == typeof(Vector3)) return EditorGUILayout.Vector3Field(label, (Vector3)value);
            if (type == typeof(Color)) return EditorGUILayout.ColorField(label, (Color)value);
            if (type.IsEnum) return EditorGUILayout.EnumPopup(label, (Enum)value);
            if (typeof(Object).IsAssignableFrom(type))
            {
                // Restrict object selection to the declared parameter type to avoid passing incompatible references to reflection.
                return EditorGUILayout.ObjectField(label, value as Object, type, true);
            }
            return value; // Unsupported types retain their cached value; DrawButton reports unsupported parameter sets to the user.
        }

        /// <summary>
        /// Invokes the selected button method after applying any configured confirmation prompt.
        /// </summary>
        /// <param name="button">The button metadata describing the method to invoke.</param>
        /// <param name="args">The argument values collected from the Inspector controls.</param>
        private void Invoke(ButtonInfo button, object[] args)
        {
            // Confirmation allows potentially destructive Inspector actions to be cancelled before any method is invoked.
            if (button.Attribute.Confirm && !EditorUtility.DisplayDialog("Confirm", $"Run '{button.Label}'?", "Run", "Cancel")) return;

            Undo.RecordObjects(targets, $"Invoke {button.Label}");

            // Static methods are invoked once without an instance; instance methods run separately for each selected target.
            if (button.Method.IsStatic) Run(button, null, args);
            else foreach (var t in targets) Run(button, t, args);

            GUIUtility.ExitGUI();
        }

        /// <summary>
        /// Invokes the reflected method and records changes to the affected Unity object.
        /// </summary>
        /// <param name="button">The metadata for the method being invoked.</param>
        /// <param name="t">The target object, or null when invoking a static method.</param>
        /// <param name="args">The argument values supplied to the reflected method.</param>
        private static void Run(ButtonInfo button, Object t, object[] args)
        {
            try
            {
                // Pass a copy of the argument array so reflection cannot replace entries in the cached Inspector arguments.
                object[] callArgs = args.Length > 0 ? (object[])args.Clone() : null;
                object result = button.Method.Invoke(t, callArgs);

                // Start returned coroutines only when the target can host them; other targets receive a diagnostic warning.
                if (result is IEnumerator routine)
                {
                    if (t is MonoBehaviour behaviour) behaviour.StartCoroutine(routine);
                    else Debug.LogWarning($"[Button] '{button.Method.Name}' returns IEnumerator but the target is not a MonoBehaviour.");
                }
            }
            catch (TargetInvocationException e)
            {
                Debug.LogException(e.InnerException ?? e, t);
            }
            catch (Exception e)
            {
                Debug.LogException(e, t);
            }

            if (t == null) return;

            // Record changes for Unity's serialization, prefab overrides, and scene dirty tracking.
            EditorUtility.SetDirty(t);
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);

            // Mark the owning scene dirty so edit-mode changes can be saved.
            if (!Application.isPlaying && t is Component component && component.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
        }

        #endregion
    }
}