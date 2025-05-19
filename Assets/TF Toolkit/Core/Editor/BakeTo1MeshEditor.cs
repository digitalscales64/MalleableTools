#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TF_Toolkit
{
    [CustomEditor(typeof(BakeTo1Mesh))]
    public class BakeTo1MeshEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            BakeTo1Mesh component = (BakeTo1Mesh)target;
            if (GUILayout.Button("Bake mesh"))
            {
                component.Bake();
            }
        }
    }
}
#endif