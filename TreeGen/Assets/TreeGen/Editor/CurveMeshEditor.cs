using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

using TreeGen;


[CustomEditor(typeof(CurveMesh))]
public class CurveMeshEditor : Editor
{
	[MenuItem("Test/Test")]
	private static void A()
	{
		var cms = Selection.GetFiltered(typeof(GameObject), SelectionMode.Editable | SelectionMode.TopLevel | SelectionMode.ExcludePrefab).Cast<GameObject>().Select(go => go.GetComponent<CurveMesh>()).ToArray();
		cms[0].Curve.Points[0] = Vector3.one * -42.4242f;
		Debug.Log(cms[1].Curve.Points[1]);
	}


	private int totalTries = 0,
				totalFoliageTries = 0;


	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();

		CurveMesh cm = (CurveMesh)target;

		if (cm.Mesh == null)
			return;

		GUILayout.Space(15.0f);

		if (GUILayout.Button("Generate Foliage"))
		{
			GameObject foliageO = new GameObject("Foliage");

			Transform fTR = foliageO.transform;
			fTR.parent = cm.transform;
			fTR.localPosition = Vector3.zero;
			fTR.localRotation = Quaternion.identity;
			fTR.localScale = Vector3.one;

			foliageO.AddComponent<MeshFilter>();
			foliageO.AddComponent<MeshRenderer>();
			foliageO.AddComponent<CurveFoliage>();

			Selection.activeObject = foliageO;
		}

		GUILayout.Space(25.0f);

		GUILayout.Label("Tris in this component: " + (cm.Mesh.triangles.Length / 3));

		GUILayout.BeginHorizontal();
		GUILayout.Label("Tris in children: " + totalTries);
		if (GUILayout.Button("Recalculate"))
		{
			totalTries = 0;
			foreach (CurveMesh cm2 in cm.gameObject.GetComponentsInChildren<CurveMesh>())
				if (cm2 != cm)
					totalTries += cm2.Mesh.triangles.Length / 3;
		}
		GUILayout.EndHorizontal();

		GUILayout.BeginHorizontal();
		GUILayout.Label("Total foliage tris: " + totalFoliageTries);
		if (GUILayout.Button("Recalculate"))
		{
			totalFoliageTries = 0;
			foreach (CurveFoliage cf in cm.gameObject.GetComponentsInChildren<CurveFoliage>())
			{
				totalFoliageTries += cf.MyMesh.triangles.Length / 3;
			}
		}
		GUILayout.EndHorizontal();
	}
}