using System;
using UnityEngine;
using UnityEditor;

using TreeGen;


[CustomEditor(typeof(CurveMesh))]
public class CurveMeshEditor : Editor
{
	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();

		CurveMesh cm = (CurveMesh)target;

		if (cm.Mesh == null)
			return;

		GUILayout.Space(15.0f);

		GUILayout.Label("Tris: " + (cm.Mesh.triangles.Length / 3));
	}
}