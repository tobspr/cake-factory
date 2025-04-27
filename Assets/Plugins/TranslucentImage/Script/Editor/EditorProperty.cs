using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LeTai.Asset.TranslucentImage.Editor
{
public class EditorProperty
{
    public readonly SerializedProperty serializedProperty;

    readonly SerializedObject   serializedObject;
    readonly MethodInfo         propertySetter;
    readonly SerializedProperty dirtyFlag;

    public EditorProperty(SerializedObject obj, string name, string serializedName)
    {
        serializedObject   = obj;
        serializedProperty = serializedObject.FindProperty(serializedName);
        propertySetter     = serializedObject.targetObject.GetType().GetProperty(name).SetMethod;
        dirtyFlag          = serializedObject.FindProperty("modifiedFromInspector");
    }

    public EditorProperty(SerializedObject obj, string name) : this(obj,
                                                                    name,
                                                                    char.ToLowerInvariant(name[0]) + name.Substring(1)) { }

    public void Draw(params GUILayoutOption[] options)
    {
        using (var scope = new EditorGUI.ChangeCheckScope())
        {
            EditorGUILayout.PropertyField(serializedProperty, options);

            if (!scope.changed)
                return;

            if (dirtyFlag != null)
                dirtyFlag.boolValue = true;

            serializedObject.ApplyModifiedProperties();

            if (serializedProperty.propertyType != SerializedPropertyType.Generic) // Not needed for now
            {
                var propertyValue = GetPropertyValue();
                CallSetters(propertyValue);
            }

            // In case the setter changes any serialized data
            serializedObject.Update();
        }
    }

    public void CallSetters(object value)
    {
        foreach (var target in serializedObject.targetObjects)
            propertySetter.Invoke(target, new[] { value });
    }

    object GetPropertyValue()
    {
        switch (serializedProperty.propertyType)
        {
        case SerializedPropertyType.ObjectReference:
            return serializedProperty.objectReferenceValue;
        case SerializedPropertyType.Float:
            return serializedProperty.floatValue;
        case SerializedPropertyType.Integer:
            return serializedProperty.intValue;
        case SerializedPropertyType.Rect:
            return serializedProperty.rectValue;
        case SerializedPropertyType.Enum:
            return serializedProperty.enumValueIndex;
        case SerializedPropertyType.Boolean:
            return serializedProperty.boolValue;
        case SerializedPropertyType.Color:
            return serializedProperty.colorValue;
        default: throw new NotImplementedException($"Type {serializedProperty.propertyType} is not implemented");
        }
    }
}
}
