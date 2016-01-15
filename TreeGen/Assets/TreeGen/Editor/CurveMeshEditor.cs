using System;
using UnityEngine;
using UnityEditor;

using TreeGen;


[CustomEditor(typeof(CurveMesh))]
public class CurveMeshEditor : Editor
{
	private int totalTries = 0;


	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();

		CurveMesh cm = (CurveMesh)target;

		if (cm.Mesh == null)
			return;

		GUILayout.Space(15.0f);

		GUILayout.Label("Tris: " + (cm.Mesh.triangles.Length / 3));

		GUILayout.BeginHorizontal();
		GUILayout.Label("Total tris: " + totalTries);
		if (GUILayout.Button("Recalculate"))
		{
			totalTries = 0;
			foreach (CurveMesh cm2 in cm.gameObject.GetComponentsInChildren<CurveMesh>())
				totalTries += cm2.Mesh.triangles.Length / 3;
		}
		GUILayout.EndHorizontal();
	}
}