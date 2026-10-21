# 🎯 Custom Attributes for Unity

A collection of **custom attributes and editor tools** for Unity that enhances the Inspector and Scene workflow.

With this package, you can make your Inspector more powerful, intuitive, and developer-friendly — similar to built-in Unity attributes, but extended.

---

# ✨ Features

* 🧩 Custom Inspector Attributes
* 🎮 Scene View Gizmo Editing (Transform, Cube, Sphere, Cone, Path)
* 🏷️ Tag, Scene, Layer and Custom Dropdowns
* 👁️ Conditional Fields (ShowIf, HideIf, EnableIf, DisableIf)
* 🔒 Read-Only and Required Fields
* ⚠️ Missing Reference Highlight
* 🔘 Inspector Buttons (parameters, coroutines, confirmation)
* 📂 Foldout Groups and Inspector-only Values
* 🔗 Automatic Component References
* 📥 Drag and Drop Lists
* 🎨 Decorators (Info Box, Title, Line, Label, Suffix)

---

# 📦 Download

[Unity Asset Store]()

[Custom Attributes - Package v0.0.6](https://github.com/AGAMEN0M/Custom-Attributes/releases/download/v0.0.6/Custom.Attributes.-.by.Lucas_AGAMENOM.unitypackage)
 / 
[Documentation](https://github.com/user-attachments/files/33749882/Custom.Attributes.Documentation.pdf)

---

# 📺 Tutorial

🎥 Video tutorials:
[Click Here.](https://www.youtube.com/playlist?list=PL5hnfx09yM4I_6OdJvShZ0rRtYF9jv6Cd)

---

# 🚀 Usage Examples

### Button Attribute

```csharp
[Button("Reset Health", Confirm = true)]
private void ResetHealth()
{
    Debug.Log("Button clicked.");
}
```

### Conditional Fields

```csharp
public bool isEnabled;

[ShowIf(nameof(isEnabled))]
public string shownField;

[HideIf(nameof(isEnabled))]
public string hiddenField;
```

### Required and Read-Only

```csharp
[Required("A target is required.")]
public Transform target;

[ReadOnly(ReadOnlyMode.WhenPlaying)]
public float currentSpeed;
```

### Dropdowns

```csharp
[TagDropdown]
public string tagName;

[SceneTagDropdown]
public string sceneName;

[LayerDropdown]
public int layerIndex;
```

### Gizmos

```csharp
[GizmoSphere(nameof(radius), AlwaysShow = true)]
public Vector3 center;
public float radius = 1f;
```

---

# 🔄 Version

**Current version:** `v0.0.6`

---

# 📁 Old Versions

[Old Versions - Package](https://github.com/AGAMEN0M/Custom-Attributes/releases)

---

# 🤝 Contributing

Suggestions, improvements, and bug reports are very welcome!

Feel free to open an issue or contribute to help improve this package.
