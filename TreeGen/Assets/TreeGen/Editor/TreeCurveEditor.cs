using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

using TreeGen;


[CustomEditor(typeof(TreeCurve))]
public class TreeCurveEditor : Editor
{
	/// <summary>
	/// Returns the number of solutions and outputs those solutions into x0 and x1.
	/// </summary>
	private static int SolveQuadratic(float a, float b, float c, out float x0, out float x1)
	{
		x0 = float.NaN;
		x1 = float.NaN;

		float discr = (b * b) - (4.0f * a * c);

		if (discr < 0.0f)
			return 0;

		float denom = 0.5f / a;
		float q = -b * denom;

		if (discr == 0.0f)
		{
			x0 = q;
			return 1;
		}

		float sqrt = Mathf.Sqrt(discr) * denom;

		x0 = q + sqrt;
		x1 = q - sqrt;
		return 2;
	}
	/// <summary>
	/// Returns the number of intersections, and outputs those intersections into the given out parameters.
	/// </summary>
	private static int RaySphereIntersect(Ray r, Vector3 spherePos, float sphereR,
										  out Vector3 x0, out float t0,
										  out Vector3 x1, out float t1)
	{
		x0 = Vector3.zero;
		x1 = Vector3.zero;

		Vector3 sphereToRay = r.origin - spherePos;

		float a = Vector3.Dot(r.direction, r.direction),
			  b = 2.0f * Vector3.Dot(r.direction, sphereToRay),
			  c = Vector3.Dot(sphereToRay, sphereToRay) - (sphereR * sphereR);

		int nVals = SolveQuadratic(a, b, c, out t0, out t1);
		if (nVals > 0)
			x0 = r.origin + (r.direction * t0);
		if (nVals > 1)
			x1 = r.origin + (r.direction * t1);

		return nVals;
	}
	/// <summary>
	/// Gets the distance the ray hits the given sphere at.
	/// Returns "float.MaxValue" if there is no hit.
	/// </summary>
	private static float DistanceToSphere(Ray r, Vector3 spherePos, float sphereR)
	{
		Vector3 x0, x1;
		float t0, t1;

		int nHits = RaySphereIntersect(r, spherePos, sphereR, out x0, out t0, out x1, out t1);
		if (nHits == 0)
			return float.MaxValue;
		if (nHits == 1)
			return t0;
		if (nHits == 2)
			return Mathf.Min(t0, t1);

		throw new InvalidOperationException("More than 2 intersections??");
	}


	public static Transform NewBranch = null;
	private static MeshCollider myMC = null;


	public int SelectedPoint = -1;


	private bool pointsFolded = false;

	private bool fixUnityBullshit = false;

	private int totalTries = 0,
				totalFoliageTries = 0;

	public override void OnInspectorGUI()
	{
		TreeCurve tc = (TreeCurve)target;


		EditorGUI.BeginChangeCheck();

		tc.RadiusAlongCurve = EditorGUILayout.CurveField("Radius along curve", tc.RadiusAlongCurve);
		tc.RadiusAroundCurve = EditorGUILayout.CurveField("Radius around curve", tc.RadiusAroundCurve);
		tc.RadiusVarianceAlongCurve = EditorGUILayout.CurveField("Radius around curve",
																 tc.RadiusVarianceAlongCurve);

		tc.RadiusScale = EditorGUILayout.FloatField("Radius scale", tc.RadiusScale);

		tc.Seed = EditorGUILayout.IntField("Seed", tc.Seed);

		tc.CurveDivisionsAlong = EditorGUILayout.IntField("Divisions along curve", tc.CurveDivisionsAlong);
		tc.CurveDivisionsAround = EditorGUILayout.IntField("Divisions around curve", tc.CurveDivisionsAround);


		GUILayout.Space(10.0f);


		//Curve editing.
		pointsFolded = EditorGUILayout.Foldout(pointsFolded, "Curve");
		if (pointsFolded)
		{
			for (int i = 0; i < tc.Curve.Points.Count; ++i)
			{
				GUILayout.BeginHorizontal();

				if (i == SelectedPoint)
				{
					GUILayout.Label("*" + (i + 1) + "*: ");
				}
				else
				{
					GUILayout.Label((i + 1).ToString() + ": ");
				}

				if (GUILayout.Button("Select"))
				{
					SelectedPoint = i;
					SceneView.RepaintAll();
				}
				if (i > 0 && i < (tc.Curve.Points.Count - 1) && GUILayout.Button("Delete"))
				{
					Undo.RecordObject(tc, "Delete curve point");
					tc.Curve.Points.RemoveAt(i);
					i -= 1;
					SceneView.RepaintAll();
				}

				GUILayout.EndHorizontal();
			}

			GUILayout.Space(10.0f);

			if (GUILayout.Button("Add Control Point"))
			{
				Undo.RecordObject(tc, "Add Control Point");
				tc.Curve.AddControlPoint();
				SceneView.RepaintAll();
			}
		}


		GUILayout.Space(15.0f);


		//Branch creation.
		if (NewBranch == null)
		{
			if (GUILayout.Button("Place new branch"))
			{
				fixUnityBullshit = true;
				SetInspectorLocked(true);
				AddBranchTo(tc, tc);
			}
			if (GUILayout.Button("Copy this branch"))
			{
				SetInspectorLocked(true);

				Transform parent = tc.transform.parent;
				if (parent == null)
				{
					Debug.LogError("This object doesn't have a parent!");
				}
				else
				{
					TreeCurve tcP = parent.GetComponent<TreeCurve>();
					if (tcP == null)
					{
						Debug.LogError("Immediate parent object \"" + parent.gameObject.name +
										"\" doesn't have a UnityCurve component");
					}
					else
					{
						AddBranchTo(tcP, tc);
					}
				}
			}
		}
		else
		{
			if (GUILayout.Button("Cancel new branch"))
			{
				SetInspectorLocked(false);
				DestroyImmediate(NewBranch.gameObject);
				DestroyImmediate(myMC);
				NewBranch = null;
			}
			GUILayout.Space(12.0f);
		}


		if (tc.Mesh == null)
			return;


		//Foliage.
		GUILayout.Space(15.0f);

		if (GUILayout.Button("Generate Foliage"))
		{
			GameObject foliageO = new GameObject("Foliage");

			Transform fTR = foliageO.transform;
			fTR.parent = tc.transform;
			fTR.localPosition = Vector3.zero;
			fTR.localRotation = Quaternion.identity;
			fTR.localScale = Vector3.one;

			foliageO.AddComponent<MeshFilter>();
			foliageO.AddComponent<MeshRenderer>();
			foliageO.AddComponent<CurveFoliage>();

			Selection.activeObject = foliageO;
		}


		GUILayout.Space(25.0f);


		//Triangle counting.

		GUILayout.Label("Tris in this component: " + (tc.Mesh.triangles.Length / 3));

		GUILayout.BeginHorizontal();
		GUILayout.Label("Tris in children: " + totalTries);
		if (GUILayout.Button("Recalculate"))
		{
			totalTries = 0;
			foreach (TreeCurve tc2 in tc.gameObject.GetComponentsInChildren<TreeCurve>())
				if (tc2 != tc)
					totalTries += tc2.Mesh.triangles.Length / 3;
		}
		GUILayout.EndHorizontal();

		GUILayout.BeginHorizontal();
		GUILayout.Label("Total foliage tris: " + totalFoliageTries);
		if (GUILayout.Button("Recalculate"))
		{
			totalFoliageTries = 0;
			foreach (CurveFoliage cf in tc.gameObject.GetComponentsInChildren<CurveFoliage>())
			{
				totalFoliageTries += cf.MyMesh.triangles.Length / 3;
			}
		}
		GUILayout.EndHorizontal();


		if (EditorGUI.EndChangeCheck())
		{
			tc.OnValidate();
			SceneView.RepaintAll();
		}


		GUILayout.Space(25.0f);


		//Gizmo drawing.

		tc.DrawCurveGizmo = EditorGUILayout.Toggle("Draw Curve?", tc.DrawCurveGizmo);
		if (!tc.DrawCurveGizmo)
		{
			SceneView.RepaintAll();
		}


		Color oldCol = tc.GizmoColor;
		float oldSize = tc.GizmoSphereRadius;

		tc.GizmoColor = EditorGUILayout.ColorField("Gizmo Color", tc.GizmoColor);
		tc.GizmoSphereRadius = EditorGUILayout.FloatField("Gizmo Sphere Raius", tc.GizmoSphereRadius);
		
		if (oldSize != tc.GizmoSphereRadius || oldCol != tc.GizmoColor)
		{
			SceneView.RepaintAll();
		}


		if (tc.DrawCurveGizmo)
		{
			int oldLine = tc.GizmoCurveSegments;
			float oldPerp = tc.GizmoLinePerpScale;

			tc.GizmoCurveSegments = EditorGUILayout.IntSlider("Gizmo Line Quality",
															   tc.GizmoCurveSegments, 1, 100);
			tc.GizmoLinePerpScale = EditorGUILayout.FloatField("Gizmo Line Perp Scale",
																  tc.GizmoLinePerpScale);

			if (oldLine != tc.GizmoCurveSegments || oldPerp != tc.GizmoLinePerpScale)
			{
				SceneView.RepaintAll();
			}
		}
	}
	public void OnSceneGUI()
	{
		TreeCurve tc = (TreeCurve)target;

		//Either edit the curve points or update the new branch being placed down.
		if (NewBranch == null)
		{
			//Edit the curve points.

			bool movedPoint = false;

			if (SelectedPoint >= 0 && SelectedPoint < tc.Curve.Points.Count)
			{
				Transform tr = tc.transform;
				Matrix4x4 trMat = tr.localToWorldMatrix;

				Vector3 oldPos = tc.Curve.Points[SelectedPoint];
				Vector3 oldPosTr = trMat.MultiplyPoint(oldPos);

				Vector3 newPos = Handles.PositionHandle(oldPosTr,
														Tools.pivotRotation == PivotRotation.Global ?
															Quaternion.identity :
															tr.rotation);
				tc.Curve.Points[SelectedPoint] = trMat.inverse.MultiplyPoint(newPos);

				if (oldPos != tc.Curve.Points[SelectedPoint])
				{
					movedPoint = true;
					Undo.RecordObject(tc, "Edit curve on \"" + tc.gameObject.name + "\"");

					tc.OnValidate();

					//Update any foliage meshes.
					for (int i = 0; i < tr.childCount; ++i)
					{
						CurveFoliage fol = tr.GetChild(i).GetComponent<CurveFoliage>();
						if (fol != null)
							fol.OnValidate();
					}

					SceneView.RepaintAll();
				}
			}

			//See if a new point was clicked.
			if (!movedPoint)
			{
				if (Event.current.type == EventType.MouseDown)
				{
					//Get the closest point that was intersected.

					Ray mRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
					float dist = float.MaxValue;
					int point = -1;

					Matrix4x4 toWorld = tc.transform.localToWorldMatrix;

					for (int i = 0; i < tc.Curve.Points.Count; ++i)
					{
						Vector3 worldP = toWorld.MultiplyPoint(tc.Curve.Points[i]);
						float tempDist = DistanceToSphere(mRay, worldP, tc.GizmoSphereRadius);
						if (tempDist < dist)
						{
							point = i;
							dist = tempDist;
						}
					}

					if (point > -1)
					{
						SelectedPoint = point;
					}
				}
			}
		}
		else
		{
			//Update the new branch being placed down.

			RaycastHit hitInfo = new RaycastHit();
			Ray r = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
			if (myMC.Raycast(r, out hitInfo, 999999.0f))
			{
				NewBranch.position = hitInfo.point;
				NewBranch.rotation = Quaternion.FromToRotation((tc.Curve.Points[tc.Curve.Points.Count - 1] -
																 tc.Curve.Points[0]).normalized,
															   hitInfo.normal);

				TreeCurve branchTC = NewBranch.GetComponent<TreeCurve>();
				branchTC.Curve.Points[0] = NewBranch.worldToLocalMatrix.MultiplyPoint(hitInfo.point);
			}

			if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
			{
				Undo.RegisterCreatedObjectUndo(NewBranch.gameObject, "Create Branch");

				NewBranch = null;
				DestroyImmediate(myMC);
				SetInspectorLocked(false);
			}
		}
	}

	private void OnDestroy()
	{
		if (fixUnityBullshit)
		{
			fixUnityBullshit = false;
			return;
		}

		if (NewBranch != null)
		{
			DestroyImmediate(NewBranch.gameObject);
			NewBranch = null;
		}
		if (myMC != null)
		{
			DestroyImmediate(myMC);
			myMC = null;
		}
	}

	private static void SetInspectorLocked(bool val)
	{
		ActiveEditorTracker.sharedTracker.isLocked = val;
	}
	private static void AddBranchTo(TreeCurve root, TreeCurve toCopy)
	{
		GameObject go = new GameObject("Branch");

		TreeCurve tc = go.AddComponent<TreeCurve>();
		tc.DrawCurveGizmo = false;
		tc.Curve = new Curve(toCopy.Curve.Points.ToList());

		tc.RadiusAlongCurve = new AnimationCurve(toCopy.RadiusAlongCurve.keys.ToArray());
		tc.RadiusAroundCurve = new AnimationCurve(toCopy.RadiusAroundCurve.keys.ToArray());
		tc.RadiusVarianceAlongCurve = new AnimationCurve(toCopy.RadiusVarianceAlongCurve.keys.ToArray());
		if (root == toCopy)
		{
			tc.RadiusScale = toCopy.RadiusScale * 0.5f;
		}
		else
		{
			tc.RadiusScale = toCopy.RadiusScale;
		}
		tc.CurveDivisionsAlong = toCopy.CurveDivisionsAlong;
		tc.CurveDivisionsAround = toCopy.CurveDivisionsAround;
		tc.OnValidate();

		MeshRenderer mrOld = toCopy.GetComponent<MeshRenderer>();
		if (mrOld != null)
		{
			MeshRenderer mr = go.AddComponent<MeshRenderer>();
			mr.sharedMaterial = mrOld.sharedMaterial;
		}

		//Add a mesh collider for ray-casting.
		myMC = root.GetComponent<MeshCollider>();
		if (myMC == null)
		{
			myMC = root.gameObject.AddComponent<MeshCollider>();
		}

		//Set the scale to be equal to the copy, or half of the copy if the copy is also the root.
		NewBranch = go.transform;
		NewBranch.localScale = toCopy.transform.localScale;
		NewBranch.parent = root.transform;
	}
}