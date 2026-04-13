# 🎯 Custom Attributes for Unity

A collection of **custom attributes and editor tools** for Unity that enhances the Inspector and Scene workflow.

With this package, you can make your Inspector more powerful, intuitive, and developer-friendly — similar to built-in Unity attributes, but extended.

---

# ✨ Features

* 🧩 Custom Inspector Attributes
* 🎮 Scene View Gizmo Editing (Cube, Sphere, Transform)
* 🏷️ Tag Dropdown Selection
* 🎬 Scene Dropdown (Build Settings)
* 👁️ Conditional Field Visibility
* 🔒 Read-Only Fields
* ⚠️ Missing Reference Highlight
* 🔘 Inspector Buttons (call methods directly)

---

# 📦 Download

[Custom Attributes - Package v0.0.5](https://drive.google.com/file/d/13rC0Oh7ZTPxSlVE4f2LuTwKPcASsqJ-U/view?usp=drive_link)
 / 
[Documentation](https://drive.google.com/file/d/1-jbcWP2oFwGaxFjKdyMcywoLPM8UGk4i/view?usp=drive_link)

---

# 📺 Tutorial

🎥 Video tutorials:
[Click Here.](https://www.youtube.com/playlist?list=PL5hnfx09yM4I_6OdJvShZ0rRtYF9jv6Cd)

---

# 🚀 Usage Examples

### Button Attribute

```csharp
[Button(nameof(MyMethod))]
private void MyMethod()
{
    Debug.Log("Button clicked.");
}
```

### Conditional Hide

```csharp
[ConditionalHide("isEnabled")]
public string hiddenField;

public bool isEnabled;
```

### Tag Dropdown

```csharp
[TagDropdown]
public string tagName;
```

### Scene Dropdown

```csharp
[SceneTagDropdown]
public string sceneName;
```

---

# 🔄 Version

**Current version:** `v0.0.5`

---

# 📁 Old Versions

[Old Versions - Package](https://drive.google.com/drive/folders/11oOED_mjsBatoCkEb2e89NtRYpUq3ABZ)

---

# 🤝 Contributing

Suggestions, improvements, and bug reports are very welcome!

Feel free to open an issue or contribute to help improve this package.