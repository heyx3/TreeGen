using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;


namespace TreeGen
{
	public static class TreeDuplicator
	{
		[MenuItem("TreeGen/Duplicate Selected Trees")]
		public static void DuplicateSelected()
		{
			IEnumerable<GameObject> selectedObjs = Selection.GetFiltered(typeof(GameObject),
																		 SelectionMode.Editable |
																			SelectionMode.TopLevel |
																			SelectionMode.ExcludePrefab).Cast<GameObject>();

			List<GameObject> newObjs = new List<GameObject>();
			foreach (GameObject go in selectedObjs)
			{
				CurveMesh cm = go.GetComponent<CurveMesh>();
				if (cm == null)
				{
					Debug.LogWarning("Selected object \"" + go.name + "\" isn't a tree and was ignored.");
					continue;
				}

				Transform tr = go.transform;

				GameObject go2 = GameObject.Instantiate<GameObject>(go);
				go2.name = go.name + " 2";
				newObjs.Add(go2);
				
				Transform tr2 = go2.transform;
				tr2.position = tr.position;
				tr2.rotation = tr.rotation;
				tr2.localScale = tr.localScale;

				//Remove the reference to the original object's trunk/foliage meshes.
				foreach (CurveMesh cm2 in go2.GetComponentsInChildren<CurveMesh>().ToArray())
				{
					cm2.GetComponent<MeshFilter>().sharedMesh = null;
					cm2.OnValidate();
				}
				foreach (CurveFoliage cf in go2.GetComponentsInChildren<CurveFoliage>().ToArray())
				{
					cf.GetComponent<MeshFilter>().sharedMesh = null;
					cf.OnValidate();
				}
			}

			//Select the duplicates.
			Selection.objects = newObjs.ToArray();
		}
	}
}