using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(MaterialAndPass))]
public class MaterialAndPassPropertyDrawer : PropertyDrawer
{
    public static readonly GUIContent materialLabel = EditorGUIUtility.TrTextContent("Material");
    
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var materialProperty = property.FindPropertyRelative("_material");
        var materialPassProperty = property.FindPropertyRelative("_index");

        EditorGUI.BeginProperty(position, label, property);
        EditorGUI.BeginChangeCheck();
        Material material = EditorGUILayout.ObjectField(materialLabel, materialProperty.objectReferenceValue,
            typeof(Material), allowSceneObjects: false) as Material;
        if (EditorGUI.EndChangeCheck())
            materialProperty.objectReferenceValue = material;

        DisplayPassPopup(material, materialPassProperty);

        EditorGUI.EndProperty();
    }

    void DisplayPassPopup(Material material, SerializedProperty materialPassProperty)
    {
        if (material != null)
        {
            int passCount = material.passCount;
            if (passCount == 0)
                return;

            string[] labels = new string[passCount];
            int[] options = new int[passCount];
            for (int i = 0; i < passCount; ++i)
            {
                string passName = material.GetPassName(i);
                if (passName.Length == 0)
                    passName = "Unnamed Pass";

                labels[i] = string.Format("{0}: {1}", i, passName);
                options[i] = i;
            }

            EditorGUI.BeginChangeCheck();
            int option = EditorGUILayout.IntPopup("Material Pass", materialPassProperty.intValue, labels, options);
            if (EditorGUI.EndChangeCheck())
                materialPassProperty.intValue = option;
        }
    }
}