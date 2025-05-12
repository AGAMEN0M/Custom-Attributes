using UnityEngine;

public class TestAttributes : MonoBehaviour
{
#pragma warning disable CS0414
    [Header("Tag Dropdown")]
    [SerializeField][TagDropdown] private string tagTest;
    [Space(10)]
    [Header("Scene Tag Dropdown")]
    [SerializeField][SceneTagDropdown] private string sceneTest;
    [Space(10)]
    [Header("Read Only")]
    [ReadOnly] public string readOnly = "Test Attributes";
    [Space(10)]
    [Header("Highlight Empty Reference")]
    [SerializeField][HighlightEmptyReference] private GameObject gameObjectTest;
    [Space(10)]
    [Header("Conditional Hide")]
    [SerializeField][ConditionalHide("hide")] private string conditionalHide1 = "Test Attributes";
    public bool hide = true;
    [Space(10)]
    [SerializeField][ConditionalHide("testCustomAttributes.hide")] private string conditionalHide2 = "Test Attributes";
    public TestCustomAttributes testCustomAttributes = new() { hide = true };
    [Space(10)]
    [SerializeField][ConditionalHide("hide1", "hide2")] private string conditionalHide3 = "Test Attributes";
    public bool hide1 = true;
    public bool hide2 = true;
    [Space(10)]
    [SerializeField][ConditionalHide(false, "hide3", "hide4")] private string conditionalHide4 = "Test Attributes";
    public bool hide3 = false;
    public bool hide4 = true;

    [Button(nameof(TestAttribute))]
    private void TestAttribute() => Debug.Log("Test Attribute");

    [System.Serializable]
    public class TestCustomAttributes
    {
        public bool hide;
    }
#pragma warning restore CS0414
}