/*
 * ---------------------------------------------------------------------------
 * Description: Example MonoBehaviour demonstrating the usage of custom Unity
 *              attributes such as TagDropdown, SceneTagDropdown, ReadOnly,
 *              HighlightEmptyReference, ConditionalHide, and Button.
 * 
 * Author:      Lucas Gomes Cecchini
 * Pseudonym:   AGAMENOM
 * ---------------------------------------------------------------------------
*/

using UnityEngine;

public class TestAttributes : MonoBehaviour
{
#pragma warning disable CS0414

    #region === Tag Dropdown Example ===

    [Header("Tag Dropdown")]
    [SerializeField, TagDropdown, Tooltip("Select a Unity tag.")]
    private string tagTest;

    #endregion

    #region === Scene Tag Dropdown Example ===

    [Header("Scene Tag Dropdown")]
    [SerializeField, SceneTagDropdown, Tooltip("Select a scene from Build Settings.")]
    private string sceneTest;

    #endregion

    #region === ReadOnly Example ===

    [Header("Read Only")]
    [ReadOnly, Tooltip("This field is read-only in the Inspector.")]
    public string readOnly = "Test Attributes";

    #endregion

    #region === Highlight Empty Reference Example ===

    [Header("Highlight Empty Reference")]
    [SerializeField, HighlightEmptyReference, Tooltip("This field highlights if empty.")]
    private GameObject gameObjectTest;

    #endregion

    #region === Conditional Hide Examples ===

    [Header("Conditional Hide")]
    [SerializeField, ConditionalHide("hide"), Tooltip("Hidden when 'hide' is true.")]
    private string conditionalHide1 = "Test Attributes";
    public bool hide = true;

    [Space(10)]

    [SerializeField, ConditionalHide("testCustomAttributes.hide"), Tooltip("Hidden when nested object 'hide' is true.")]
    private string conditionalHide2 = "Test Attributes";
    public TestCustomAttributes testCustomAttributes = new() { hide = true };

    [Space(10)]

    [SerializeField, ConditionalHide("hide1", "hide2"), Tooltip("Hidden if any condition is false.")]
    private string conditionalHide3 = "Test Attributes";
    public bool hide1 = true;
    public bool hide2 = true;

    [Space(10)]

    [SerializeField, ConditionalHide(false, "hide3", "hide4"), Tooltip("Hidden if both conditions are false.")]
    private string conditionalHide4 = "Test Attributes";
    public bool hide3 = false;
    public bool hide4 = true;

    #endregion

    #region === Button Example ===

    /// <summary>
    /// Demonstrates a button in the Inspector using the Button attribute.
    /// Clicking this button logs a message to the Console.
    /// </summary>
    [Button(nameof(TestAttribute))]
    private void TestAttribute() => Debug.Log("Test Attribute", this);

    #endregion

    #region === Nested Serializable Class ===

    /// <summary>
    /// Serializable class used to test nested ConditionalHide attributes.
    /// </summary>
    [System.Serializable]
    public class TestCustomAttributes
    {
        public bool hide;
    }

    #endregion

#pragma warning restore CS0414
}