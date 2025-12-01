/*
 * ---------------------------------------------------------------------------
 * Description: Example MonoBehaviour demonstrating the usage of custom Unity
 *              attributes such as TagDropdown, SceneTagDropdown, ReadOnly,
 *              HighlightEmptyReference, ConditionalHide, Button, GizmoTransform,
 *              GizmoCube, and GizmoSphere.
 * 
 * Author:      Lucas Gomes Cecchini
 * Pseudonym:   AGAMENOM
 * ---------------------------------------------------------------------------
*/

using UnityEngine;
using System;

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

    [SerializeField, ConditionalHide("testCustomAttributes.hide"), Tooltip("Hidden when the nested 'hide' value is true.")]
    private string conditionalHide2 = "Test Attributes";
    public TestCustomAttributes testCustomAttributes = new() { hide = true };

    [Space(10)]

    [SerializeField, ConditionalHide("hide1", "hide2"), Tooltip("Hidden if any provided boolean condition is false.")]
    private string conditionalHide3 = "Test Attributes";
    public bool hide1 = true;
    public bool hide2 = true;

    [Space(10)]

    [SerializeField, ConditionalHide(false, "hide3", "hide4"), Tooltip("Hidden only if both conditions evaluate to false.")]
    private string conditionalHide4 = "Test Attributes";
    public bool hide3 = false;
    public bool hide4 = true;

    #endregion

    #region === Gizmo Transform Examples ===

    [Header("Gizmo Transform")]
    [GizmoTransform, Tooltip("Edit this Vector3 position using gizmo handles in the Scene View.")]
    public Vector3 position1 = Vector3.one;

    [Space(10)]

    [GizmoTransform(nameof(rotation)), Tooltip("Edit this position and its associated rotation in the Scene View.")]
    public Vector3 position2 = -Vector3.one;

    [Tooltip("Rotation used when the gizmo rotation mode is active.")]
    public Quaternion rotation = Quaternion.identity;

    [Space(10)]

    [Tooltip("Array containing multiple gizmo-editable transform entries.")]
    public TestGizmoAttributes[] gizmoTransform = new TestGizmoAttributes[1];

    #endregion

    #region === Gizmo Cube Examples ===

    [Header("Gizmo Cube")]
    [GizmoCube(nameof(cubeSize1)), Tooltip("Offset of the first cube gizmo in local space.")]
    public Vector3 cubeOffset1 = Vector3.zero;

    [Tooltip("Size of the first cube gizmo in local space.")]
    public Vector3 cubeSize1 = Vector3.one;

    [Space(10)]

    [GizmoCube(nameof(cubeSize2), 1, 0, 0), Tooltip("Offset of the second cube gizmo, displayed in red.")]
    public Vector3 cubeOffset2 = Vector3.up;

    [Tooltip("Size of the second cube gizmo in local space.")]
    public Vector3 cubeSize2 = Vector3.one;

    [Space(10)]

    [Tooltip("Array containing multiple gizmo-editable cube entries.")]
    public TestGizmoCubeAttributes[] gizmoCube = new TestGizmoCubeAttributes[1];

    #endregion

    #region === Gizmo Sphere Examples ===

    [Header("Gizmo Sphere")]

    [GizmoSphere(nameof(radius1)), Tooltip("Offset da esfera em espaço local. Este ponto define o centro da esfera exibida na Scene View.")]
    public Vector3 sphereOffset1 = Vector3.down;

    [Tooltip("Raio da esfera exibida e editável na Scene View.")]
    public float radius1 = 1f;

    [Space(10)]

    [GizmoSphere(nameof(radius2), 1, 0, 0), Tooltip("Offset da segunda esfera, exibida na cor vermelha.")]
    public Vector3 sphereOffset2 = Vector3.up;

    [Tooltip("Raio da segunda esfera gizmo.")]
    public float radius2 = 1f;

    [Space(10)]

    [Tooltip("Lista contendo múltiplos elementos de esferas editáveis via Gizmo.")]
    public TestGizmoSphereAttributes[] gizmoSphere = new TestGizmoSphereAttributes[1];

    #endregion

    #region === Button Example ===

    /// <summary>
    /// Demonstrates a button in the Inspector using the Button attribute.
    /// Clicking this button logs a message to the Console.
    /// </summary>
    [Button(nameof(TestAttribute))]
    private void TestAttribute() => Debug.Log("Test Attribute", this);

    #endregion

    #region === Serializable Class ===

    /// <summary>
    /// Serializable class used to test nested ConditionalHide attributes.
    /// </summary>
    [Serializable]
    public class TestCustomAttributes
    {
        public bool hide;
    }

    /// <summary>
    /// Serializable class used for testing multiple gizmo-editable items such as transform offsets and rotations.
    /// </summary>
    [Serializable]
    public class TestGizmoAttributes
    {
        [GizmoTransform(nameof(rotationTest)), Tooltip("Editable position using gizmo handles in the Scene View.")]
        public Vector3 positionTest = Vector3.down;

        [Tooltip("Editable rotation when the gizmo rotation mode is active.")]
        public Quaternion rotationTest = Quaternion.identity;
    }

    /// <summary>
    /// Serializable class used for testing gizmo-editable cube items inside arrays or nested structures.
    /// </summary>
    [Serializable]
    public class TestGizmoCubeAttributes
    {
        [GizmoCube(nameof(cubeSize), 0, 1, 0), Tooltip("Offset of this cube gizmo, displayed in green.")]
        public Vector3 cubeOffset = Vector3.down;

        [Tooltip("Size of this cube gizmo in local space.")]
        public Vector3 cubeSize = Vector3.one;
    }

    /// <summary>
    /// Atributos usados para testar múltiplas esferas gizmo dentro de arrays ou estruturas aninhadas.
    /// </summary>
    [Serializable]
    public class TestGizmoSphereAttributes
    {
        [GizmoSphere(nameof(radius), 0, 0, 1), Tooltip("Offset da esfera em espaço local dentro deste item da lista.")]
        public Vector3 sphereOffset = Vector3.zero;

        [Tooltip("Raio da esfera deste item da lista.")]
        public float radius = 1f;
    }

    #endregion

#pragma warning restore CS0414
}