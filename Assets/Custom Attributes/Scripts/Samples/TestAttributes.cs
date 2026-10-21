/*
 * ---------------------------------------------------------------------------
 * Description: Demonstration component showcasing the attributes provided by the
 *              CustomAttributes package. It groups practical examples of conditional
 *              Inspector fields, validation, dropdowns, automatic references, nested data,
 *              Scene view gizmos, and custom Inspector buttons.
 *
 * Using:
 * 1. Attach TestAttributes to a GameObject to explore the available attributes
 *    directly in the Unity Inspector.
 *
 * 2. Conditions:
 *    - [ShowIf(nameof(mode), Mode.Advanced)] displays a field when mode is
 *      Advanced.
 *    - [ShowIf(nameof(useExtras))] displays a field when useExtras is true.
 *    - [HideIf(nameof(useExtras))] hides a field when useExtras is true.
 *    - [ShowIf(ConditionLogic.Or, nameof(useExtras), "!isSimple")] demonstrates
 *      an OR condition and a negated boolean property.
 *    - [DisableIf("!useExtras")] disables editing when useExtras is false.
 *
 * 3. Validation and presentation:
 *    - [ReadOnly] prevents Inspector editing.
 *    - [Required("A target is mandatory!")] marks a reference as required.
 *    - [GUIColor("#FFD54F")] tints a field.
 *    - [OnValueChanged(nameof(OnSpeedChanged))] invokes a callback when the
 *      field's value changes.
 *    - [Suffix("m/s")] displays a unit suffix.
 *
 * 4. Dropdowns and references:
 *    - [TagDropdown], [SceneTagDropdown], and [LayerDropdown] provide
 *      specialized selection controls.
 *    - [Dropdown(nameof(Difficulties))] uses a member that supplies options.
 *    - [RequireInterface(typeof(IInteractable))] filters an object reference
 *      by the required interface.
 *    - [Expandable] displays a ScriptableObject's contents inline.
 *    - [GetComponent], [GetInChildren], and [GetInParent] demonstrate
 *      automatic component references.
 *    - [MinMaxSlider] edits a Vector2 or Vector2Int as a range.
 *    - [UniqueId] marks a string field for unique-ID handling.
 *
 * 5. Drag and drop lists:
 *    - [DragAndDrop] displays a drop area above an array or List of Unity objects.
 *    - Dropped GameObjects add their matching component, dropped Textures add
 *      their Sprites, and dropped folders add every matching asset inside them.
 *    - Options demonstrate custom text, height, duplicates, and folder handling.
 *
 * 6. Groups and nested data:
 *    - [FoldoutGroup("Stats")] groups related fields.
 *    - SpawnPoint demonstrates attributes inside a serializable nested class.
 *    - Arrays of SpawnPoint demonstrate per-element gizmo attributes.
 *
 * 7. Scene view gizmos:
 *    - [GizmoTransform], [GizmoCube], [GizmoSphere], [GizmoCone], and
 *      [GizmoPath] visualize positions, shapes, viewing ranges, and paths.
 *    - AlwaysShow = true requests persistent gizmo display where supported
 *      by the corresponding attribute implementation.
 *
 * 8. Inspector buttons and displayed values:
 *    - [ShowInInspector] exposes a property or private member in the Inspector.
 *    - [Button] turns methods into Inspector actions.
 *    - Button options demonstrate confirmation, placement, height, play-mode
 *      restrictions, optional parameters, coroutines, and static methods.
 *
 * Note:
 * This is a sample component, not a replacement for the package's attribute
 * implementations. The exact behavior of each attribute is defined by its
 * corresponding runtime and editor scripts.
 *
 * Author: Lucas Gomes Cecchini
 * Pseudonym: AGAMENOM
 * ---------------------------------------------------------------------------
*/

using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using System;

namespace CustomAttributes.Samples
{
    // Some private fields are intentionally assigned for Inspector examples and are not necessarily read by the component's gameplay logic.
#pragma warning disable CS0414

    #region === Example Interface ===

    /// <summary>
    /// Example interface used to demonstrate the RequireInterface attribute.
    /// Any object intended for the example must provide an Interact method.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Performs the interaction represented by the implementing object.
        /// </summary>
        void Interact();
    }

    #endregion

    #region === Main Sample Component ===

    /// <summary>
    /// Central demonstration component for the CustomAttributes package.
    /// Each section provides example fields or methods that can be inspected
    /// and tested directly in the Unity Editor.
    /// </summary>
    public class TestAttributes : MonoBehaviour
    {
        #region === Shared Coroutine Wait ===

        /// <summary>
        /// Reusable delay for the countdown example.
        /// Caching the wait instruction avoids allocating a new instance
        /// each time the coroutine reaches a delay.
        /// </summary>
        private static readonly WaitForSeconds _waitForSeconds1 = new(1f);

        #endregion

        #region === Shared Example Types ===

        /// <summary>
        /// Difficulty or configuration mode used by conditional examples.
        /// </summary>
        public enum Mode { Simple, Advanced }

        /// <summary>
        /// Serializable spawn-point data used to demonstrate attributes
        /// on fields inside nested serializable classes and arrays.
        /// </summary>
        [Serializable]
        public class SpawnPoint
        {
            /// <summary>
            /// Controls whether this spawn point's conditional fields are shown.
            /// </summary>
            public bool enabled = true;

            /// <summary>
            /// Local position offset for the spawn point.
            /// The sphere gizmo uses radius to determine its size and color
            /// to distinguish this example in the Scene view.
            /// </summary>
            [ShowIf(nameof(enabled))]
            [GizmoSphere(nameof(radius), "#FF8800")]
            public Vector3 offset;

            /// <summary>
            /// Sphere radius, shown only while this spawn point is enabled.
            /// </summary>
            [ShowIf(nameof(enabled))]
            public float radius = 1f;

            /// <summary>
            /// Example identifier displayed without allowing direct Inspector
            /// editing. UniqueId demonstrates the package's ID attribute.
            /// </summary>
            [ReadOnly, UniqueId]
            public string id;
        }

        #endregion

        #region === Conditions ===

        /// <summary>
        /// Heading that separates the conditional-display examples in the Inspector.
        /// </summary>
        [Title("Conditions")]
        [InfoBox("Change 'mode' and 'useExtras' to see fields appear and disappear.")]
        public Mode mode;

        /// <summary>
        /// Main boolean switch used by several conditional attributes below.
        /// Change it in the Inspector to compare their behavior.
        /// </summary>
        public bool useExtras;

        /// <summary>
        /// Visible when mode equals Mode.Advanced.
        /// The enum comparison demonstrates a condition against a specific value.
        /// </summary>
        [ShowIf(nameof(mode), Mode.Advanced)]
        public float advancedValue = 5f;

        /// <summary>
        /// Demonstrates a boolean visibility condition combined with a
        /// reference-highlighting attribute.
        /// </summary>
        [ShowIf(nameof(useExtras)), HighlightEmptyReference]
        public Transform extraTarget;

        /// <summary>
        /// Demonstrates the inverse presentation of useExtras:
        /// this field is intended to be hidden when extras are enabled.
        /// </summary>
        [HideIf(nameof(useExtras))]
        public string shownWhenExtrasOff = "Extras are off";

        /// <summary>
        /// Demonstrates OR-based conditional display.
        /// The field is eligible to be shown when useExtras is true
        /// or the isSimple condition evaluates to false.
        /// </summary>
        [ShowIf(ConditionLogic.Or, nameof(useExtras), "!isSimple")]
        public int orCondition;

        /// <summary>
        /// Demonstrates a negated boolean condition for disabling a field.
        /// When useExtras is false, the field is intended to be disabled.
        /// </summary>
        [DisableIf("!useExtras")]
        public float disabledWithoutExtras = 1f;

        /// <summary>
        /// Read-only helper property used by the condition above.
        /// It reports whether the selected mode is Simple.
        /// </summary>
        private bool IsSimple => mode == Mode.Simple;

        #endregion

        #region === Read Only / Required / Tint / Callbacks ===

        /// <summary>
        /// Heading for validation and field-presentation examples.
        /// </summary>
        [Title("Validation")]

        /// <summary>
        /// Demonstrates a serialized private string with a tag-selection
        /// dropdown and read-only presentation.
        /// </summary>
        [ReadOnly, TagDropdown, SerializeField]
        private string readOnlyTag = "Untagged";

        /// <summary>
        /// Example of a reference marked as required with a custom message.
        /// </summary>
        [Required("A target is mandatory!")]
        public Transform requiredTarget;

        /// <summary>
        /// Demonstrates a field tint specified using a hexadecimal color.
        /// </summary>
        [GUIColor("#FFD54F")]
        public float tintedField = 1f;

        /// <summary>
        /// Demonstrates a value-change callback and a unit suffix.
        /// Editing speed invokes OnSpeedChanged through the attribute system.
        /// </summary>
        [OnValueChanged(nameof(OnSpeedChanged)), Suffix("m/s")]
        public float speed = 3f;

        /// <summary>
        /// Demonstrates a read-only field whose editing restriction is tied
        /// to the current Editor state.
        /// </summary>
        [ReadOnly(ReadOnlyMode.WhenPlaying)]
        public int lockedWhilePlaying = 10;

        /// <summary>
        /// Receives the new speed value when the attribute's change callback
        /// is invoked, then writes a diagnostic message to the Console.
        /// </summary>
        /// <param name="newValue">The updated speed value.</param>
        private void OnSpeedChanged(float newValue) => Debug.Log($"Speed changed to {newValue}", this);

        #endregion

        #region === Dropdowns / References ===

        /// <summary>
        /// Heading for dropdown, object-reference, and range-slider examples.
        /// </summary>
        [Title("Dropdowns & references")]

        /// <summary>
        /// Demonstrates selecting a Unity tag into a string field.
        /// </summary>
        [TagDropdown]
        public string tagName;

        /// <summary>
        /// Demonstrates the scene-tag dropdown provided by the package.
        /// </summary>
        [SceneTagDropdown]
        public string sceneName;

        /// <summary>
        /// Demonstrates layer selection stored as an integer index.
        /// </summary>
        [LayerDropdown]
        public int layerIndex;

        /// <summary>
        /// Demonstrates layer selection stored as a string.
        /// </summary>
        [LayerDropdown]
        public string layerName;

        /// <summary>
        /// Uses the Difficulties member as the source of dropdown options.
        /// The initial value is one of the options supplied by that member.
        /// </summary>
        [Dropdown(nameof(Difficulties))]
        public string difficulty = "Normal";

        /// <summary>
        /// Uses the Levels member as the source of integer dropdown options.
        /// </summary>
        [Dropdown(nameof(Levels))]
        public int level = 1;

        /// <summary>
        /// Supplies the string options for the difficulty dropdown.
        /// </summary>
        private string[] Difficulties => new[] { "Easy", "Normal", "Hard" };

        /// <summary>
        /// Supplies integer options from 1 through 5 for the level dropdown.
        /// An iterator allows values to be yielded one at a time.
        /// </summary>
        private IEnumerable<int> Levels()
        {
            for (int i = 1; i <= 5; i++) yield return i;
        }

        /// <summary>
        /// Demonstrates an object reference constrained to objects that
        /// implement the IInteractable interface.
        /// </summary>
        [RequireInterface(typeof(IInteractable))]
        public UnityEngine.Object interactable;

        /// <summary>
        /// Demonstrates expandable Inspector presentation for a ScriptableObject.
        /// </summary>
        [Expandable]
        public ScriptableObject config;

        /// <summary>
        /// Demonstrates automatic component lookup on the same GameObject.
        /// </summary>
        [GetComponent]
        public Rigidbody autoBody;

        /// <summary>
        /// Demonstrates automatic component lookup among child objects.
        /// </summary>
        [GetInChildren]
        public Collider autoChildCollider;

        /// <summary>
        /// Demonstrates automatic component lookup among parent objects.
        /// </summary>
        [GetInParent]
        public Canvas autoParentCanvas;

        /// <summary>
        /// Demonstrates a floating-point range represented by a Vector2.
        /// The slider limits are 0 and 100.
        /// </summary>
        [MinMaxSlider(0f, 100f)]
        public Vector2 damageRange = new(10f, 40f);

        /// <summary>
        /// Demonstrates an integer range represented by a Vector2Int.
        /// The slider limits are 0 and 10.
        /// </summary>
        [MinMaxSlider(0, 10)]
        public Vector2Int waveRange = new(2, 6);

        /// <summary>
        /// Additional unique-ID example outside the nested SpawnPoint type.
        /// </summary>
        [UniqueId]
        public string saveId;

        #endregion

        #region === Drag And Drop Lists ===

        /// <summary>
        /// Heading for the drag-and-drop list examples.
        /// </summary>
        [Title("Drag and drop lists")]

        /// <summary>
        /// Demonstrates a drop area with a custom text above a List of materials.
        /// Drag materials from the Project window into the area to add them.
        /// </summary>
        [DragAndDrop("Drop Materials here")]
        public List<Material> materials = new();

        /// <summary>
        /// Demonstrates an array of components. Dragging a GameObject adds its matching
        /// Collider component.
        /// </summary>
        [DragAndDrop]
        public Collider[] colliders;

        /// <summary>
        /// Demonstrates dropping a Texture that contains several Sprites, which adds every Sprite,
        /// and allows the same Sprite to be added more than once in a shorter drop area.
        /// </summary>
        [DragAndDrop(Height = 40f, AllowDuplicates = true)]
        public List<Sprite> sprites = new();

        /// <summary>
        /// Demonstrates combining the drop area with other meta attributes.
        /// The whole field, including its drop area, is hidden unless useExtras is true.
        /// Dropping a project folder is disabled for this list.
        /// </summary>
        [ShowIf(nameof(useExtras))]
        [DragAndDrop("Drop AudioClips here", AcceptFolders = false)]
        public List<AudioClip> clips = new();

        #endregion

        #region === Groups / Nested Data ===

        /// <summary>
        /// Demonstrates grouping multiple fields under a shared foldout.
        /// </summary>
        [FoldoutGroup("Stats")]
        public float health = 100f;

        /// <summary>
        /// Shares the Stats foldout with health.
        /// </summary>
        [FoldoutGroup("Stats")]
        public float armor = 25f;

        /// <summary>
        /// Combines a foldout group with a visibility condition.
        /// The field is shown according to the useExtras condition.
        /// </summary>
        [FoldoutGroup("Stats"), ShowIf(nameof(useExtras))]
        public float extraStat = 1f;

        /// <summary>
        /// Single nested SpawnPoint instance.
        /// This example uses metadata attributes inside a serializable class
        /// and initializes representative values for the Inspector.
        /// </summary>
        [Title("Nested class (meta attributes work inside it)")]
        public SpawnPoint singleSpawn = new()
        {
            enabled = true,
            offset = new Vector3(0, 0, -5),
            radius = 1,
            id = "69c8240bfb3c45c5a3ede064de4d7e65"
        };

        /// <summary>
        /// Array of nested SpawnPoint instances.
        /// Each element provides its own values for the attributes on SpawnPoint.
        /// </summary>
        [Header("Array of nested classes (gizmo attributes work per element)")]
        public SpawnPoint[] spawnPoints = new SpawnPoint[1]
        {
            new()
            {
                enabled = true,
                offset = new Vector3(0, 0, -10),
                radius = 1,
                id = "67481af1bc804b2ab51aa271bb7afd9b"
            }
        };

        #endregion

        #region === Gizmos ===

        /// <summary>
        /// Heading for Scene view visualization examples.
        /// </summary>
        [Title("Gizmos")]

        /// <summary>
        /// Demonstrates a position gizmo associated with a rotation field.
        /// AlwaysShow requests that the gizmo remain visible where supported.
        /// </summary>
        [GizmoTransform(nameof(handRotation), AlwaysShow = true)]
        public Vector3 handPosition = new(0f, 0f, -15f);

        /// <summary>
        /// Rotation associated with the handPosition transform gizmo.
        /// </summary>
        public Quaternion handRotation = Quaternion.identity;

        /// <summary>
        /// Demonstrates a cube gizmo using boxSize to determine its dimensions.
        /// </summary>
        [GizmoCube(nameof(boxSize), AlwaysShow = true)]
        public Vector3 boxCenter = new(0f, 0f, -20f);

        /// <summary>
        /// Dimensions of the cube gizmo centered at boxCenter.
        /// </summary>
        public Vector3 boxSize = Vector3.one;

        /// <summary>
        /// Demonstrates a sphere gizmo with a hexadecimal color and radius field.
        /// </summary>
        [GizmoSphere(nameof(sphereRadius), "#00FFAA", AlwaysShow = true)]
        public Vector3 sphereCenter = new(0f, 0f, -25f);

        /// <summary>
        /// Radius used by the sphere gizmo.
        /// </summary>
        public float sphereRadius = 0.75f;

        /// <summary>
        /// Demonstrates a cone or field-of-view gizmo.
        /// The attribute receives the angle and distance through member names.
        /// </summary>
        [GizmoCone(nameof(viewAngle), nameof(viewDistance), AlwaysShow = true)]
        public Vector3 eyeOffset = new(0f, 1.6f, 5f);

        /// <summary>
        /// Viewing angle used by the cone gizmo, constrained to 1–360 degrees.
        /// </summary>
        [Range(1f, 360f)]
        public float viewAngle = 90f;

        /// <summary>
        /// Viewing distance used by the cone gizmo.
        /// </summary>
        public float viewDistance = 5f;

        /// <summary>
        /// Demonstrates a closed path gizmo built from a sequence of positions.
        /// The points form a rectangular patrol route.
        /// </summary>
        [GizmoPath(Closed = true, AlwaysShow = true)]
        public Vector3[] patrolPoints =
        {
            new(-2f, 0f, -2f),
            new(2f, 0f, -2f),
            new(2f, 0f, 2f),
            new(-2f, 0f, 2f)
        };

        #endregion

        #region === Buttons / Debug Members ===

        /// <summary>
        /// Example of exposing a public read-only property in the Inspector.
        /// The value is obtained from Unity's real-time startup clock.
        /// </summary>
        [ShowInInspector]
        public float TimeSinceStart => Time.realtimeSinceStartup;

        /// <summary>
        /// Example of a private property displayed only during Play Mode.
        /// </summary>
        [ShowInInspector(OnlyInPlayMode = true)]
        private int Frame => Time.frameCount;

        /// <summary>
        /// Simple Inspector button that writes a diagnostic message.
        /// </summary>
        [Button]
        private void SimpleButton() => Debug.Log("Simple button clicked", this);

        /// <summary>
        /// Demonstrates a named button with confirmation enabled.
        /// When invoked, the method restores the example health value.
        /// </summary>
        [Button("Reset Health", Confirm = true)]
        private void ResetHealth() => health = 100f;

        /// <summary>
        /// Demonstrates a button with custom placement and height, plus
        /// optional parameters of different types.
        /// The attribute implementation determines how these parameters
        /// are presented and supplied when the button is invoked.
        /// </summary>
        [Button(Position = ButtonPosition.Bottom, Height = 28f)]
        private void ButtonWithParameters(string message = "Hello", int times = 2, Mode chosenMode = Mode.Advanced)
        {
            for (int i = 0; i < times; i++) Debug.Log($"{message} ({chosenMode})", this);
        }

        /// <summary>
        /// Demonstrates a button restricted to Play Mode that invokes a coroutine.
        /// The coroutine logs a countdown with one-second delays and then
        /// reports completion.
        /// </summary>
        [Button(Mode = ButtonMode.PlayModeOnly)]
        private IEnumerator CountdownCoroutine()
        {
            for (int i = 3; i > 0; i--)
            {
                Debug.Log($"Countdown: {i}", this);
                yield return _waitForSeconds1;
            }

            Debug.Log("Done!", this);
        }

        /// <summary>
        /// Demonstrates a static button restricted to Edit Mode.
        /// Since the method is static, it does not require a component instance.
        /// </summary>
        [Button(Mode = ButtonMode.EditModeOnly)]
        private static void StaticEditModeButton() => Debug.Log("Static method called");

        #endregion
    }

    #endregion

#pragma warning restore CS0414
}