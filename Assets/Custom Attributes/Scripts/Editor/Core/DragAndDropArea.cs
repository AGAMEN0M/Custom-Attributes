/*
 * ---------------------------------------------------------------------------
 * Description: Draws the drag-and-drop area of the [DragAndDrop] attribute above an
 *              array or List of UnityEngine.Object types and adds the dropped objects to
 *              it. Dropped objects are resolved against the list's element type:
 *              direct matches, components of dropped GameObjects, sub-assets of dropped
 *              assets (for example Sprites inside a Texture), and the matching assets
 *              inside dropped folders.
 *
 * Author: Lucas Gomes Cecchini
 * Pseudonym: AGAMENOM
 * ---------------------------------------------------------------------------
*/

using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using System;

using Object = UnityEngine.Object;

namespace CustomAttributes.Editing
{
    /// <summary>
    /// Draws the drop area for <see cref="DragAndDropAttribute"/> and applies dropped objects to the list.
    /// </summary>
    /// <remarks>
    /// The attribute is handled by the central Inspector instead of a PropertyDrawer because Unity applies
    /// PropertyAttribute drawers to each list element, not to the list itself.
    /// </remarks>
    internal static class DragAndDropArea
    {
        #region === Constants and Cached Styles ===

        /// <summary>
        /// Minimum height of the drop area so its label always remains readable.
        /// </summary>
        private const float MinHeight = 24f;

        /// <summary>
        /// Width in pixels of the outline drawn around the drop area.
        /// </summary>
        private const float BorderWidth = 1f;

        /// <summary>
        /// Remembers whether a dragged asset contains a matching sub-asset, because DragUpdated fires continuously
        /// and loading every sub-asset of a large file on each event would make the Editor stutter.
        /// </summary>
        private static readonly Dictionary<(Object, Type), bool> subAssetCache = new();

        /// <summary>
        /// Style used for the text displayed inside the drop area. Created lazily because GUIStyle can only be built during GUI events.
        /// </summary>
        private static GUIStyle labelStyle;

        /// <summary>
        /// Gets the centered, wrapped style used for the drop area label.
        /// </summary>
        private static GUIStyle LabelStyle
        {
            get
            {
                // Build the style once and reuse it, because allocating a GUIStyle on every repaint would create garbage.
                labelStyle ??= new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    wordWrap = true
                };

                return labelStyle;
            }
        }

        #endregion

        #region === Drawing ===

        /// <summary>
        /// Draws the drop area for a list and handles drag events that occur over it.
        /// </summary>
        /// <param name="array">The serialized array or list property that receives the dropped objects.</param>
        /// <param name="field">The reflected field of the array, used to resolve the element type.</param>
        /// <param name="attribute">The attribute that configures the label, height, duplicates, and folder handling.</param>
        public static void Draw(SerializedProperty array, FieldInfo field, DragAndDropAttribute attribute)
        {
            // Only arrays and lists of Unity objects can receive dropped objects, so report misuse instead of silently ignoring it.
            Type elementType = GetObjectElementType(array, field);
            if (elementType == null)
            {
                EditorGUILayout.HelpBox("[DragAndDrop] requires an array or List of UnityEngine.Object types.", MessageType.Warning);
                return;
            }

            // Reserve the area through the layout system so it follows the Inspector width and indentation.
            Rect rect = EditorGUI.IndentedRect(GUILayoutUtility.GetRect(0f, Mathf.Max(MinHeight, attribute.Height), GUILayout.ExpandWidth(true)));

            // Respect read-only state and disable the area for multi-object editing because every object has its own list.
            bool multipleTargets = array.serializedObject.isEditingMultipleObjects;
            bool editable = GUI.enabled && !multipleTargets;

            if (editable) HandleDragEvents(rect, array, elementType, attribute);

            // Paint only during repaint events; other events are used exclusively for input handling.
            if (Event.current.type != EventType.Repaint) return;

            // Release cached drag results once the drag has ended so dragged objects are not kept alive.
            if (subAssetCache.Count > 0 && DragAndDrop.objectReferences.Length == 0) subAssetCache.Clear();

            string label = multipleTargets ? "Multi-object editing is not supported" : attribute.Label ?? $"Drop {ObjectNames.NicifyVariableName(elementType.Name)} here";

            // Highlight the area while a drag with objects is positioned over it so the target is obvious.
            bool hovering = editable && rect.Contains(Event.current.mousePosition) && DragAndDrop.objectReferences.Length > 0;

            DrawArea(rect, label, hovering, editable);
        }

        /// <summary>
        /// Paints the background, outline, and label of the drop area.
        /// </summary>
        /// <param name="rect">The area to paint.</param>
        /// <param name="label">The text displayed in the center of the area.</param>
        /// <param name="hovering">Whether a drag is currently over the area.</param>
        /// <param name="editable">Whether the area accepts drops.</param>
        private static void DrawArea(Rect rect, string label, bool hovering, bool editable)
        {
            Color background = hovering ? new Color(0.2f, 0.5f, 0.9f, 0.35f) : new Color(0.15f, 0.15f, 0.15f, 0.5f);
            Color border = hovering ? new Color(0.3f, 0.6f, 1f, 1f) : new Color(0.5f, 0.5f, 0.5f, 0.6f);

            EditorGUI.DrawRect(rect, background);

            // Draw the outline as four thin rectangles because IMGUI has no dedicated outline primitive.
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, BorderWidth), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - BorderWidth, rect.width, BorderWidth), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, BorderWidth, rect.height), border);
            EditorGUI.DrawRect(new Rect(rect.xMax - BorderWidth, rect.y, BorderWidth, rect.height), border);

            // Dim the text when the area is disabled so it does not look like it accepts drops.
            Color previous = GUI.contentColor;
            GUI.contentColor = new Color(1f, 1f, 1f, editable ? 0.85f : 0.4f);
            GUI.Label(rect, label, LabelStyle);
            GUI.contentColor = previous;
        }

        #endregion

        #region === Drag Handling ===

        /// <summary>
        /// Updates the drag cursor and adds the dropped objects when the user releases them over the area.
        /// </summary>
        /// <param name="rect">The drop area.</param>
        /// <param name="array">The list that receives the objects.</param>
        /// <param name="elementType">The Unity object type stored by the list.</param>
        /// <param name="attribute">The attribute that configures duplicates and folder handling.</param>
        private static void HandleDragEvents(Rect rect, SerializedProperty array, Type elementType, DragAndDropAttribute attribute)
        {
            Event evt = Event.current;

            // Ignore every event except drag events that happen over the area.
            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform) return;
            if (!rect.Contains(evt.mousePosition)) return;

            if (evt.type == EventType.DragUpdated)
            {
                // Use a cheap check while dragging because folder scanning on every update would make the Editor stutter.
                bool acceptable = CanAccept(DragAndDrop.objectReferences, elementType, attribute.AcceptFolders);
                DragAndDrop.visualMode = acceptable ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;

                evt.Use();
                HandleUtility.Repaint();
                return;
            }

            // Resolve the final objects only when the drop is performed, including the more expensive folder and sub-asset searches.
            var resolved = Collect(DragAndDrop.objectReferences, elementType, attribute.AcceptFolders);

            if (resolved.Count == 0)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                evt.Use();
                return;
            }

            DragAndDrop.AcceptDrag();
            evt.Use();

            // Apply the changes through the SerializedProperty so Undo, dirty state, and prefab overrides are handled by Unity.
            int added = AddToList(array, resolved, attribute.AllowDuplicates);

            // The list length changed before the list was drawn in this event, so stop this GUI pass to avoid control-count mismatches.
            if (added > 0) GUIUtility.ExitGUI();
        }

        /// <summary>
        /// Performs a fast check of whether at least one dragged object could be accepted.
        /// </summary>
        /// <param name="dragged">The objects currently being dragged.</param>
        /// <param name="elementType">The Unity object type stored by the list.</param>
        /// <param name="acceptFolders">Whether folders are accepted optimistically.</param>
        /// <returns><see langword="true"/> when the drop is likely to add at least one object; otherwise, <see langword="false"/>.</returns>
        private static bool CanAccept(Object[] dragged, Type elementType, bool acceptFolders)
        {
            foreach (var obj in dragged)
            {
                if (obj == null) continue;

                // Folders are accepted optimistically; their contents are only scanned when the drop is performed.
                if (acceptFolders && IsFolder(obj)) return true;
                if (TryResolveDirect(obj, elementType) != null) return true;

                // Assets can still provide a match through sub-assets, such as Sprites inside a Texture.
                if (EditorUtility.IsPersistent(obj) && HasSubAsset(obj, elementType)) return true;
            }

            return false;
        }

        #endregion

        #region === Object Resolution ===

        /// <summary>
        /// Resolves every dragged object into the objects that should be added to the list.
        /// </summary>
        /// <param name="dragged">The dragged objects.</param>
        /// <param name="elementType">The Unity object type stored by the list.</param>
        /// <param name="acceptFolders">Whether the contents of dragged folders are searched.</param>
        /// <returns>The distinct objects that match the element type, in drop order.</returns>
        private static List<Object> Collect(Object[] dragged, Type elementType, bool acceptFolders)
        {
            var results = new List<Object>();

            foreach (var obj in dragged)
            {
                if (obj == null) continue;

                // Folders are expanded into the assets they contain instead of being treated as objects themselves.
                if (acceptFolders && IsFolder(obj))
                {
                    AddDistinct(results, FindInFolder(AssetDatabase.GetAssetPath(obj), elementType));
                    continue;
                }

                // Prefer a direct match or a component, and only look into sub-assets when neither applies.
                var direct = TryResolveDirect(obj, elementType);
                if (direct != null)
                {
                    AddDistinct(results, new[] { direct });
                    continue;
                }

                if (EditorUtility.IsPersistent(obj)) AddDistinct(results, FindSubAssets(obj, elementType));
            }

            return results;
        }

        /// <summary>
        /// Converts a dragged object into the list's element type without searching other assets.
        /// </summary>
        /// <param name="obj">The dragged object.</param>
        /// <param name="elementType">The Unity object type stored by the list.</param>
        /// <returns>The matching object, or <see langword="null"/> when the object cannot represent the element type.</returns>
        private static Object TryResolveDirect(Object obj, Type elementType)
        {
            // An object that already has the requested type is used as-is.
            if (elementType.IsInstanceOfType(obj)) return obj;

            bool wantsComponent = typeof(Component).IsAssignableFrom(elementType);

            // Dropping a GameObject (or one of its components) into a component list picks the matching component.
            if (wantsComponent)
            {
                if (obj is GameObject go) return go.GetComponent(elementType);
                if (obj is Component component) return component.GetComponent(elementType);
            }

            // Dropping a component into a GameObject list uses the component's GameObject.
            if (elementType == typeof(GameObject) && obj is Component owner) return owner.gameObject;

            return null;
        }

        /// <summary>
        /// Determines whether a dropped asset contains at least one sub-asset that matches the element type, caching the answer.
        /// </summary>
        /// <param name="obj">The dropped asset.</param>
        /// <param name="elementType">The Unity object type stored by the list.</param>
        /// <returns><see langword="true"/> when a matching sub-asset exists; otherwise, <see langword="false"/>.</returns>
        private static bool HasSubAsset(Object obj, Type elementType)
        {
            var key = (obj, elementType);

            // Reuse the previous answer for the same dragged asset to keep continuous drag updates cheap.
            if (!subAssetCache.TryGetValue(key, out bool result))
            {
                result = FindSubAssets(obj, elementType).Count > 0;
                subAssetCache[key] = result;
            }

            return result;
        }

        /// <summary>
        /// Finds the sub-assets of a dropped asset that match the element type.
        /// </summary>
        /// <param name="obj">The dropped asset.</param>
        /// <param name="elementType">The Unity object type stored by the list.</param>
        /// <returns>The matching sub-assets, such as every Sprite contained in a Texture.</returns>
        private static List<Object> FindSubAssets(Object obj, Type elementType)
        {
            var matches = new List<Object>();
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) return matches;

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset != null && elementType.IsInstanceOfType(asset)) matches.Add(asset);
            }

            return matches;
        }

        /// <summary>
        /// Finds the assets that match the element type inside a folder and its subfolders.
        /// </summary>
        /// <param name="folder">The project-relative folder path.</param>
        /// <param name="elementType">The Unity object type stored by the list.</param>
        /// <returns>The matching assets found in the folder.</returns>
        private static List<Object> FindInFolder(string folder, Type elementType)
        {
            var matches = new List<Object>();

            // Components and GameObjects live inside prefabs, so search prefabs and resolve the component from the prefab root.
            bool searchPrefabs = typeof(Component).IsAssignableFrom(elementType) || elementType == typeof(GameObject);
            string filter = searchPrefabs ? "t:Prefab" : $"t:{elementType.Name}";

            foreach (var guid in AssetDatabase.FindAssets(filter, new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (searchPrefabs)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Object resolved = prefab != null ? TryResolveDirect(prefab, elementType) : null;
                    if (resolved != null) matches.Add(resolved);
                    continue;
                }

                // Load every object in the file so sub-assets (for example Sprites in a Texture) are included.
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset != null && elementType.IsInstanceOfType(asset)) matches.Add(asset);
                }
            }

            return matches;
        }

        /// <summary>
        /// Determines whether an object is a project folder.
        /// </summary>
        /// <param name="obj">The object to inspect.</param>
        /// <returns><see langword="true"/> when the object is a valid project folder; otherwise, <see langword="false"/>.</returns>
        private static bool IsFolder(Object obj)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            return !string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path);
        }

        /// <summary>
        /// Adds objects to a result list while skipping any object it already contains.
        /// </summary>
        /// <param name="results">The list that receives the objects.</param>
        /// <param name="items">The candidate objects.</param>
        private static void AddDistinct(List<Object> results, IEnumerable<Object> items)
        {
            foreach (var item in items)
            {
                if (!results.Contains(item)) results.Add(item);
            }
        }

        #endregion

        #region === List Modification ===

        /// <summary>
        /// Adds objects to the serialized list, reusing empty slots before growing it.
        /// </summary>
        /// <param name="array">The serialized array or list property.</param>
        /// <param name="items">The objects to add.</param>
        /// <param name="allowDuplicates">Whether objects already present in the list may be added again.</param>
        /// <returns>The number of objects that were added.</returns>
        private static int AddToList(SerializedProperty array, List<Object> items, bool allowDuplicates)
        {
            // Assets cannot keep references to scene objects, so those drops must be rejected up front.
            bool targetIsAsset = EditorUtility.IsPersistent(array.serializedObject.targetObject);
            int added = 0;

            foreach (var item in items)
            {
                if (!allowDuplicates && Contains(array, item)) continue;

                if (targetIsAsset && !EditorUtility.IsPersistent(item))
                {
                    Debug.LogWarning($"[DragAndDrop] '{item.name}' is a scene object and cannot be referenced from an asset.", array.serializedObject.targetObject);
                    continue;
                }

                // Reuse an empty slot when one exists; otherwise append a new element at the end.
                int slot = FindEmptySlot(array);
                if (slot < 0)
                {
                    slot = array.arraySize;
                    array.InsertArrayElementAtIndex(slot);
                }

                array.GetArrayElementAtIndex(slot).objectReferenceValue = item;
                added++;
            }

            // Apply once for the whole drop so it becomes a single Undo step.
            if (added > 0) array.serializedObject.ApplyModifiedProperties();

            return added;
        }

        /// <summary>
        /// Determines whether the list already contains an object.
        /// </summary>
        /// <param name="array">The serialized array or list property.</param>
        /// <param name="item">The object to search for.</param>
        /// <returns><see langword="true"/> when an element references the object; otherwise, <see langword="false"/>.</returns>
        private static bool Contains(SerializedProperty array, Object item)
        {
            for (int i = 0; i < array.arraySize; i++)
            {
                if (array.GetArrayElementAtIndex(i).objectReferenceValue == item) return true;
            }

            return false;
        }

        /// <summary>
        /// Finds the first element that does not reference an object.
        /// </summary>
        /// <param name="array">The serialized array or list property.</param>
        /// <returns>The index of the first empty element, or -1 when every element is assigned.</returns>
        private static int FindEmptySlot(SerializedProperty array)
        {
            for (int i = 0; i < array.arraySize; i++)
            {
                if (array.GetArrayElementAtIndex(i).objectReferenceValue == null) return i;
            }

            return -1;
        }

        #endregion

        #region === Type Resolution ===

        /// <summary>
        /// Resolves the Unity object type stored by an array or list field.
        /// </summary>
        /// <param name="array">The serialized property being drawn.</param>
        /// <param name="field">The reflected field of the property.</param>
        /// <returns>The element type, or <see langword="null"/> when the field is not an array or list of Unity objects.</returns>
        private static Type GetObjectElementType(SerializedProperty array, FieldInfo field)
        {
            if (field == null || !array.isArray) return null;

            Type elementType = PropertyReflection.GetElementType(field.FieldType);
            return elementType != null && typeof(Object).IsAssignableFrom(elementType) ? elementType : null;
        }

        #endregion
    }
}