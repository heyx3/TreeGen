using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEditor;


namespace TreeGen
{
	/// <summary>
	/// Allows the user to bake any number of trees into a branch mesh file and a foliage mesh file.
	/// </summary>
	public class TreeBaker : EditorWindow
	{
		[MenuItem("TreeGen/Bake selected trees into a mesh")]
		public static void OpenEditorWindow()
		{
			TreeBaker tb = GetWindow<TreeBaker>(true, "Tree Baker");

			tb.UpdateSelectedObjs();
			tb.InitMats();

			tb.ShowUtility();
		}


		private GameObject[] selectedBranches = null;
		private void UpdateSelectedObjs()
		{
			//Get all selected objects in the scene without any of their children.
			IEnumerable<GameObject> selectedObjs = Selection.GetFiltered(typeof(GameObject),
																		 SelectionMode.Editable |
																			SelectionMode.TopLevel |
																			SelectionMode.ExcludePrefab).Cast<GameObject>();

			//Filter out any objects that don't contain a tree.
			selectedBranches =
				selectedObjs.Where(go => (go.GetComponentsInChildren<TreeCurve>().FirstOrDefault() != null)).ToArray();
		}


		Material branchMat, foliageMat;
		private void InitMats()
		{
			branchMat = null;
			TreeCurve[] tcs = selectedBranches.GetComponents<TreeCurve>(true).ToArray();
			if (tcs.Length > 0)
			{
				branchMat = tcs.GetComponents<TreeCurve, MeshRenderer>().First().sharedMaterial;
			}

			foliageMat = null;
			CurveFoliage[] cfs = selectedBranches.GetComponents<CurveFoliage>(true).ToArray();
			if (cfs.Length > 0)
			{
				foliageMat = cfs.GetComponents<CurveFoliage, MeshRenderer>().First().sharedMaterial;
			}
		}


		void OnSelectionChange()
		{
			UpdateSelectedObjs();
		}
		void OnGUI()
		{
			if (selectedBranches == null || selectedBranches.Length == 0)
			{
				GUILayout.Label("No trees are selected in the scene.");
				return;
			}


			GUILayout.BeginHorizontal();
			GUILayout.Label("Branch material:");
			branchMat = (Material)EditorGUILayout.ObjectField(branchMat, typeof(Material));
			GUILayout.EndHorizontal();
			
			GUILayout.BeginHorizontal();
			GUILayout.Label("Foliage material:");
			foliageMat = (Material)EditorGUILayout.ObjectField(foliageMat, typeof(Material));
			GUILayout.EndHorizontal();


			GUILayout.Space(15.0f);


			if (GUILayout.Button("Bake"))
			{
				string folderPath = EditorUtility.SaveFolderPanel("Choose a new folder to save the baked data",
																  "", "Trees1");
				if (folderPath.Length == 0)
				{
					return;
				}
				DirectoryInfo folder = new DirectoryInfo(folderPath);
				folder.Create();
				string name = Path.GetFileNameWithoutExtension(folderPath);
				string trunkPath = Path.Combine(folderPath, name + " Trunk.obj"),
					   foliagePath = Path.Combine(folderPath, name + " Foliage.obj");
				
				Bake(trunkPath, foliagePath);
			}
		}


		/// <summary>
		/// Executes the baking.
		/// </summary>
		public void Bake(string trunkMeshPath, string foliageMeshPath)
		{
			//Get the root transform for the baked mesh.
			//If one tree is selected, use it.
			//Otherwise, just use the average position of all the selected trees.

			Matrix4x4 rootM;
			Vector3 avgPos = Vector3.zero;

			if (selectedBranches.Length > 1)
			{
				avgPos = selectedBranches[0].transform.position;
				for (int i = 1; i < selectedBranches.Length; ++i)
					avgPos += selectedBranches[i].transform.position;
				avgPos /= (float)selectedBranches.Length;

				rootM = Matrix4x4.TRS(-avgPos, Quaternion.identity, Vector3.one);
			}
			else
			{
				rootM = selectedBranches[0].transform.worldToLocalMatrix;
			}

			IEnumerable<TreeCurve> curves = selectedBranches.SelectMany(go => go.GetComponentsInChildren<TreeCurve>());
			

			//Create one big mesh for all the branches.
			Mesh msh = CreateMesh(curves.Select(tc => tc.GetComponent<MeshFilter>()), rootM);
			ExportOBJ(msh, trunkMeshPath, "Trunk");
			msh.Clear();

			//Create one big mesh for all the foliage.
			CurveFoliage[] cFs = curves.GetComponentsInChildren<TreeCurve, CurveFoliage>().RemoveDuplicates().ToArray();
			if (cFs.Length > 0)
			{
				if (cFs.Any(cf => cf.Mode == CurveFoliage.MeshModes.Point))
				{
					Debug.LogError("Can't currently output point foliage meshes to OBJ");
					cFs = new CurveFoliage[0];
				}
				else
				{
					msh = CreateMesh(cFs.Select(cf => cf.GetComponent<MeshFilter>()), rootM);
					ExportOBJ(msh, foliageMeshPath, "Foliage");
					msh.Clear();
				}
			}



			//Replace the current tree object with one that just has the baked assets.
			//Put the original objects inside the new one and deactivate it.

			Transform bakedObj = new GameObject("Baked Trees").transform;

			if (selectedBranches.Length > 1)
			{
				bakedObj.position = avgPos;
				bakedObj.rotation = Quaternion.identity;
				bakedObj.localScale = Vector3.one;
			}
			else
			{
				Transform oldObj = selectedBranches[0].transform;
				bakedObj.position = oldObj.position;
				bakedObj.rotation = oldObj.rotation;
				bakedObj.localScale = oldObj.localScale;
			}

			AssetDatabase.Refresh();

			Transform trunkChild = new GameObject("Trunk").transform;
			trunkChild.SetParent(bakedObj, false);
			MeshFilter mf = trunkChild.gameObject.AddComponent<MeshFilter>();
			mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(PathUtils.GetRelativePath(trunkMeshPath,
																						  "Assets"));
			MeshRenderer mr = trunkChild.gameObject.AddComponent<MeshRenderer>();
			mr.sharedMaterial = branchMat;

			if (cFs.Length > 0)
			{
				Transform foliageChild = new GameObject("Foliage").transform;
				foliageChild.SetParent(bakedObj, false);
				mf = foliageChild.gameObject.AddComponent<MeshFilter>();
				mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(PathUtils.GetRelativePath(foliageMeshPath,
																							  "Assets"));
				mr = foliageChild.gameObject.AddComponent<MeshRenderer>();
				mr.sharedMaterial = foliageMat;
			}
		}


		/// <summary>
		/// Combines all of the given meshes together into one mesh.
		/// </summary>
		/// <param name="inRoot">
		/// If not null, all mesh data will be converted to its local space.
		/// Otherwise, all mesh data will be converted to world space.
		/// </param>
		private static Mesh CreateMesh(IEnumerable<MeshFilter> meshes, Matrix4x4? localTransf = null)
		{
			if (!localTransf.HasValue)
				localTransf = Matrix4x4.identity;

			//Count the total vertices and indices.
			int nVerts = 0,
				nIndices = 0;
			foreach (MeshFilter mf in meshes)
			{
				nVerts += mf.sharedMesh.vertexCount;
				nIndices += mf.sharedMesh.triangles.Length;
			}

			//Create arrays to hold the new mesh data.
			Vector3[] poses = new Vector3[nVerts],
					  normals = new Vector3[nVerts];
			Vector4[] tangents = new Vector4[nVerts];
			Vector2[] uvs = new Vector2[nVerts];
			int[] indices = new int[nIndices];

			
			//Add each mesh's data to the new mesh.
			int vOffset = 0,
				iOffset = 0;
			foreach (MeshFilter mf in meshes)
			{
				Matrix4x4 toWorld = mf.transform.localToWorldMatrix;

				Vector3[] mPoses = mf.sharedMesh.vertices,
						  mNormals = mf.sharedMesh.normals;
				Vector4[] mTangents = mf.sharedMesh.tangents;
				Vector2[] mUVs = mf.sharedMesh.uv;

				for (int i = 0; i < mf.sharedMesh.vertexCount; ++i)
				{
					poses[vOffset + i] = localTransf.Value.MultiplyPoint(toWorld.MultiplyPoint(mPoses[i]));
					normals[vOffset + i] = localTransf.Value.MultiplyVector(toWorld.MultiplyVector(mNormals[i]));

					float f = mTangents[i].w;
					Vector3 tang = new Vector3(mTangents[i].x, mTangents[i].y, mTangents[i].z);
					tang = localTransf.Value.MultiplyVector(toWorld.MultiplyVector(tang));
					tangents[vOffset + i] = new Vector4(tang.x, tang.y, tang.z, f);

					uvs[vOffset + i] = mUVs[i];
				}

				int[] mIndices = mf.sharedMesh.triangles;
				for (int i = 0; i < mIndices.Length; ++i)
				{
					indices[iOffset + i] = vOffset + mIndices[i];
				}

				vOffset += mPoses.Length;
				iOffset += mIndices.Length;
			}
		

			//Generate the new mesh.

			Mesh outM = new Mesh();

			outM.vertices = poses;
			outM.normals = normals;
			outM.tangents = tangents;
			outM.uv = uvs;
			outM.triangles = indices;

			outM.UploadMeshData(false);
			return outM;
		}
		/// <summary>
		/// Exports the given mesh to an OBJ file.
		/// </summary>
		/// <param name="meshName">The name this mesh will have in the OBJ file.</param>
		private static void ExportOBJ(Mesh m, string filePath, string meshName)
		{
			System.Text.StringBuilder sb = new System.Text.StringBuilder();

			//Generate the vertex data.
			//Note that for some reason, we need to flip the X axis of the mesh data.
			sb.Append("g ").Append(meshName).Append("\n");
			foreach (Vector3 v in m.vertices)
			{
				sb.Append(string.Format("v {0} {1} {2}\n", -v.x, v.y, v.z));
			}
			sb.Append("\n");
			foreach (Vector3 v in m.normals)
			{
				sb.Append(string.Format("vn {0} {1} {2}\n", -v.x, v.y, v.z));
			}
			sb.Append("\n");
			foreach (Vector3 v in m.uv)
			{
				sb.Append(string.Format("vt {0} {1}\n", v.x, v.y));
			}

			//Generate the material data.
			sb.Append("\n");
			sb.Append("usemtl ").Append("DummyMat\n");
			sb.Append("usemap ").Append("DummyMat\n");

			//Generate the index data.
			int[] triangles = m.triangles;
			for (int i = 0; i < triangles.Length; i += 3)
			{
				sb.Append(string.Format("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\n",
						  triangles[i + 2] + 1,
						  triangles[i + 1] + 1,
						  triangles[i] + 1));
			}

			//Write out the actual file.
			using (StreamWriter sw = new StreamWriter(filePath))
			{
				sw.Write(sb.ToString());
			}
		}
	}
}