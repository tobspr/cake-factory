using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.UI;
using Debug = System.Diagnostics.Debug;

namespace LeTai.Asset.TranslucentImage.Editor
{
[CustomEditor(typeof(TranslucentImage))]
[CanEditMultipleObjects]
public class TranslucentImageEditor : ImageEditor
{
    SerializedProperty type;
    SerializedProperty sprite;
    SerializedProperty preserveAspect;
    SerializedProperty useSpriteMesh;
    AnimBool           showTypeAnim;

    EditorProperty     source;
    SerializedProperty spriteBlending;

    bool showMaterial = true;

    List<TranslucentImage> tiList;
    UnityEditor.Editor     materialEditor;

    bool needValidateSource;
    bool needValidateMaterial;
    bool materialUsedInDifferentSource;
    bool usingIncorrectShader;

    Shader correctShader;

    protected override void OnEnable()
    {
        base.OnEnable();

        typeof(ImageEditor).GetField("m_SpriteContent", BindingFlags.Instance | BindingFlags.NonPublic)
                           .SetValue(this, new GUIContent("Sprite"));

        sprite         = serializedObject.FindProperty("m_Sprite");
        type           = serializedObject.FindProperty("m_Type");
        preserveAspect = serializedObject.FindProperty("m_PreserveAspect");
        useSpriteMesh  = serializedObject.FindProperty("m_UseSpriteMesh");

        showTypeAnim = new AnimBool(sprite.objectReferenceValue != null);
        showTypeAnim.valueChanged.AddListener(Repaint);
        source         = new EditorProperty(serializedObject, nameof(TranslucentImage.source), "_source");
        spriteBlending = serializedObject.FindProperty("m_spriteBlending");

        correctShader = Shader.Find("UI/TranslucentImage");

        tiList = targets.Cast<TranslucentImage>().ToList();
        if (tiList.Count > 0)
        {
            CheckMaterialUsedInDifferentSource();
            CheckCorrectShader();
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        tiList = targets.Cast<TranslucentImage>().ToList();
        Debug.Assert(tiList.Count > 0, "Translucent Image Editor serializedObject target is null");

        DrawSpriteControls();
        RaycastControlsGUI();
        MaskableControlsGUI();

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        DrawSourceControls();
        EditorGUILayout.PropertyField(spriteBlending);

        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(m_Color);
        DrawMaterialControls();
        DrawMaterialProperties();

        serializedObject.ApplyModifiedProperties();

        if (needValidateSource) ValidateSource();
        if (needValidateMaterial) ValidateMaterial();
    }

    void DrawSpriteControls()
    {
        SpriteGUI();
        showTypeAnim.target = sprite.objectReferenceValue != null;
        if (EditorGUILayout.BeginFadeGroup(showTypeAnim.faded))
            TypeGUI();
        EditorGUILayout.EndFadeGroup();

        Image.Type type = (Image.Type)this.type.enumValueIndex;
        bool showNativeSize = (type == Image.Type.Simple || type == Image.Type.Filled)
                           && sprite.objectReferenceValue != null;
        SetShowNativeSize(showNativeSize, false);

        if (EditorGUILayout.BeginFadeGroup(m_ShowNativeSize.faded))
        {
            EditorGUI.indentLevel++;

            if ((Image.Type)this.type.enumValueIndex == Image.Type.Simple)
                EditorGUILayout.PropertyField(useSpriteMesh);

            EditorGUILayout.PropertyField(preserveAspect);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFadeGroup();
        NativeSizeButtonGUI();
    }

    void DrawSourceControls()
    {
        using (var changes = new EditorGUI.ChangeCheckScope())
        {
            source.Draw();
            if (changes.changed)
                needValidateSource = true;
        }

        if (!source.serializedProperty.objectReferenceValue)
        {
            var existingSources = Shims.FindObjectsOfType<TranslucentImageSource>();
            if (existingSources.Length > 0)
            {
                using (new EditorGUI.IndentLevelScope())
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel("From current Scene");
                    using (new EditorGUILayout.VerticalScope())
                        foreach (var s in existingSources)
                        {
                            if (GUILayout.Button(s.gameObject.name))
                            {
                                Undo.RecordObject(target, $"Set source to {s.gameObject.name}");
                                source.serializedProperty.objectReferenceValue = s;
                                source.CallSetters(s);
                                needValidateSource = true;
                            }
                        }
                }

                EditorGUILayout.Space();
            }
            else
            {
                EditorGUILayout.HelpBox("No Translucent Image Source(s) found in current scene", MessageType.Warning);
            }
        }
    }

    void DrawMaterialControls()
    {
        using (var changes = new EditorGUI.ChangeCheckScope())
        {
            EditorGUILayout.PropertyField(m_Material);
            if (changes.changed)
                needValidateMaterial = true;

            if (usingIncorrectShader)
            {
                EditorGUILayout.HelpBox("Material should use the \"UI/Translucent Image\" shader", MessageType.Warning);
            }
            if (materialUsedInDifferentSource)
            {
                EditorGUILayout.HelpBox("Translucent Images with different Sources" +
                                        " should also use different Materials",
                                        MessageType.Error);
            }
        }
    }

    void DrawMaterialProperties()
    {
        using (var change = new EditorGUI.ChangeCheckScope())
        {
            var targetMaterials = tiList.Select(t => t.material).Cast<Object>().ToArray();

            using (_ = new EditorGUI.IndentLevelScope())
            {
                showMaterial = EditorGUILayout.BeginFoldoutHeaderGroup(showMaterial, "Material settings");
                if (showMaterial)
                {
                    CreateCachedEditor(targetMaterials, typeof(MaterialEditor), ref materialEditor);
                    var materialProperties = MaterialEditor.GetMaterialProperties(targetMaterials);
                    TranslucentImageShaderGUI.DrawProperties((MaterialEditor)materialEditor, materialProperties, true);
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }


            if (change.changed)
            {
                foreach (var ti in tiList)
                {
                    if (ti.materialForRendering != ti.material)
                    {
                        Undo.RecordObject(ti.materialForRendering, $"Modify material {ti.material.name}");
                        TranslucentImage.CopyMaterialPropertiesTo(ti.material, ti.materialForRendering);
                    }
                }
            }
        }
    }

    void ValidateSource()
    {
        CheckMaterialUsedInDifferentSource();
        needValidateSource = false;
    }

    void ValidateMaterial()
    {
        CheckMaterialUsedInDifferentSource();
        CheckCorrectShader();
        needValidateMaterial = false;
    }

    private void CheckCorrectShader()
    {
        usingIncorrectShader = tiList.Any(ti => ti.material.shader != correctShader);
    }

    private void CheckMaterialUsedInDifferentSource()
    {
        if (!tiList[0].source)
        {
            materialUsedInDifferentSource = false;
            return;
        }

        var diffSource = Shims.FindObjectsOfType<TranslucentImage>()
                              .Where(ti => ti.source != tiList[0].source)
                              .ToList();

        if (!diffSource.Any())
        {
            materialUsedInDifferentSource = false;
            return;
        }

        var sameMat = diffSource.GroupBy(ti => ti.material).ToList();

        materialUsedInDifferentSource = sameMat.Any(group => group.Key == tiList[0].material);

        needValidateMaterial = false;
    }
}
}
