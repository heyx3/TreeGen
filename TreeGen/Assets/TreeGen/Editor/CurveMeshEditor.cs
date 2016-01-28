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
		}

		GUILayout.Space(25.0f);

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

		GUILayout.Space(15.0f);

		if (GUILayout.Button("Bake to Folder"))
		{
			string path = EditorUtility.SaveFolderPanel("Choose a new folder to save the baked data", "",
														"Tree1");
			BakeToFile(path);
		}
	}

	private void BakeToFile(string folderPath)
	{
		CurveMesh thisCM = (CurveMesh)target;

		//Set up the folder structure.
		DirectoryInfo folder = new DirectoryInfo(folderPath);
		folder.Create();
		string name = Path.GetFileNameWithoutExtension(folderPath);
		string trunkPath = Path.Combine(folderPath, name + " Trunk.obj"),
			   foliagePath = Path.Combine(folderPath, name + " Foliage.obj"),
			   prefabPath = Path.Combine(folderPath, name + "Original.prefab");

		//Create one big mesh for all the branches.
		CurveMesh[] cMs = thisCM.GetComponentsInChildren<CurveMesh>();
		Mesh msh = CreateMesh(cMs.Select(cm => cm.GetComponent<MeshFilter>()),
							  thisCM.transform);
		Material trunkMat = thisCM.GetComponent<MeshRenderer>().sharedMaterial;
		ExportOBJ(msh, trunkPath, "Trunk", trunkMat.name);
		msh.Clear();

		//Create one big mesh for all the foliage.
		CurveFoliage[] cFs = thisCM.GetComponentsInChildren<CurveFoliage>();
		if (cFs.Length > 0)
		{
			if (cFs.Any(cf => cf.Mode == CurveFoliage.MeshModes.Point))
			{
				Debug.LogError("Can't currently output point meshes to OBJ");
				cFs = new CurveFoliage[0];
			}
			else
			{
				msh = CreateMesh(cFs.Select(cf => cf.GetComponent<MeshFilter>()),
								 thisCM.transform);
				ExportOBJ(msh, foliagePath, "Foliage", "green");
				msh.Clear();
			}
		}


		//Export the current tree object to a prefab, then replace it
		//    with a new object that just has the baked meshes.
		
		PrefabUtility.CreatePrefab(PathUtils.GetRelativePath(prefabPath, "Assets"),
								   thisCM.gameObject);
		
		Transform bakedObj = new GameObject("Baked " + thisCM.gameObject.name).transform;
		
		Transform oldObj = thisCM.transform;
		bakedObj.position = oldObj.position;
		bakedObj.rotation = oldObj.rotation;
		bakedObj.localScale = oldObj.localScale;
		
		Transform trunkChild = new GameObject("Trunk").transform;
		trunkChild.SetParent(bakedObj, false);
		MeshFilter mf = trunkChild.gameObject.AddComponent<MeshFilter>();
		mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(PathUtils.GetRelativePath(trunkPath, "Assets"));
		MeshRenderer mr = trunkChild.gameObject.AddComponent<MeshRenderer>();
		mr.sharedMaterial = thisCM.GetComponent<MeshRenderer>().sharedMaterial;

		if (cFs.Length > 0)
		{
			Transform foliageChild = new GameObject("Foliage").transform;
			foliageChild.SetParent(bakedObj, false);
			mf = foliageChild.gameObject.AddComponent<MeshFilter>();
			mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(PathUtils.GetRelativePath(foliagePath,
																						  "Assets"));
			mr = foliageChild.gameObject.AddComponent<MeshRenderer>();
			mr.sharedMaterial = cFs[0].GetComponent<MeshRenderer>().sharedMaterial;
		}

		DestroyImmediate(oldObj.gameObject);
	}
	private Mesh CreateMesh(IEnumerable<MeshFilter> meshes, Transform root)
	{
		//TODO: Fix. Step through line 184.

		Matrix4x4 toRootLocal = root.worldToLocalMatrix;


		int nVerts = 0,
			nIndices = 0;
		foreach (MeshFilter mf in meshes)
		{
			nVerts += mf.sharedMesh.vertexCount;
			nIndices += mf.sharedMesh.triangles.Length;
		}

		Vector3[] poses = new Vector3[nVerts],
				  normals = new Vector3[nVerts];
		Vector4[] tangents = new Vector4[nVerts];
		Vector2[] uvs = new Vector2[nVerts];
		int[] indices = new int[nIndices];
		int vOffset = 0,
			iOffset = 0;
		foreach (MeshFilter mf in meshes)
		{
			Matrix4x4 toWorld = mf.transform.localToWorldMatrix;

			Vector3[] mPoses = mf.sharedMesh.vertices,
					  mNormals = mf.sharedMesh.normals;
			Vector4[] mTangents = mf.sharedMesh.tangents;
			Vector2[] mUVs = mf.sharedMesh.uv;
			int[] mIndices = mf.sharedMesh.triangles;

			for (int i = 0; i < mf.sharedMesh.vertexCount; ++i)
			{
				poses[vOffset + i] = toRootLocal.MultiplyPoint(toWorld.MultiplyPoint(mPoses[i]));
				normals[vOffset + i] = toRootLocal.MultiplyPoint(toWorld.MultiplyPoint(mNormals[i]));
				tangents[vOffset + i] = toRootLocal.MultiplyPoint(toWorld.MultiplyPoint(mTangents[i]));
				uvs[vOffset + i] = mUVs[i];
				indices[iOffset + i] = mIndices[i];
			}
			vOffset += poses.Length;
			iOffset += mIndices.Length;
		}
		

		Mesh outM = new Mesh();

		outM.vertices = poses;
		outM.normals = normals;
		outM.tangents = tangents;
		outM.uv = uvs;
		outM.triangles = indices;

		outM.UploadMeshData(true);
		return outM;
	}
	private void ExportOBJ(Mesh m, string filePath, string meshName, string materialName)
	{
		System.Text.StringBuilder sb = new System.Text.StringBuilder();

		//Write vertex data.
		sb.Append("g ").Append(meshName).Append("\n");
		foreach (Vector3 v in m.vertices)
		{
			sb.Append(string.Format("v {0} {1} {2}\n", v.x, v.y, v.z));
		}
		sb.Append("\n");
		foreach (Vector3 v in m.normals)
		{
			sb.Append(string.Format("vn {0} {1} {2}\n", v.x, v.y, v.z));
		}
		sb.Append("\n");
		foreach (Vector3 v in m.uv)
		{
			sb.Append(string.Format("vt {0} {1}\n", v.x, v.y));
		}

		//Write material data.
		sb.Append("\n");
		sb.Append("usemtl ").Append(materialName).Append("\n");
		sb.Append("usemap ").Append(materialName).Append("\n");

		int[] triangles = m.triangles;
		for (int i = 0; i < triangles.Length; i += 3)
		{
			sb.Append(string.Format("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\n",
				triangles[i] + 1, triangles[i + 1] + 1, triangles[i + 2] + 1));
		}

		//Write the string out to a file.
		using (StreamWriter sw = new StreamWriter(filePath))
		{
			sw.Write(sb.ToString());
		}
	}
}