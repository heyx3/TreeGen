using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;

using TreeGen;


[CustomEditor(typeof(UnityCurve))]
public class UnityCurveEditor : Editor
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


	public int SelectedPoint = -1;
	

	public static Transform NewBranch = null;
	private static MeshCollider myMC = null;

	private bool fixUnityBullshit = false;


	public override void OnInspectorGUI()
	{
		UnityCurve curve = (UnityCurve)target;

		GUILayout.Label("Points:");
		for (int i = 0; i < curve.Curve.Points.Count; ++i)
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
			if (i > 0 && i < (curve.Curve.Points.Count - 1) && GUILayout.Button("Delete"))
			{
				curve.Curve.Points.RemoveAt(i);
				i -= 1;
				SceneView.RepaintAll();
			}

			GUILayout.EndHorizontal();
		}

		GUILayout.Space(10.0f);

		if (GUILayout.Button("Add Control Point"))
		{
			Undo.RecordObject(curve, "Add Control Point");
			curve.Curve.AddControlPoint();
			SceneView.RepaintAll();
		}

		GUILayout.Space(5.0f);

		if (NewBranch != null)
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
		else
		{
			if (GUILayout.Button("Place new branch"))
			{
				fixUnityBullshit = true;
				SetInspectorLocked(true);
				AddBranchTo(curve, curve);
			}
			if (GUILayout.Button("Copy this branch"))
			{
				SetInspectorLocked(true);

				Transform parent = curve.transform.parent;
				if (parent == null)
				{
					Debug.LogError("This object does not have a parent");
				}
				else
				{
					UnityCurve ucP = parent.GetComponent<UnityCurve>();
					if (ucP == null)
					{
						Debug.LogError("Immediate parent object \"" + parent.gameObject.name +
									   "\" doesn't have a UnityCurve component");
					}
					else
					{
						AddBranchTo(ucP, curve);
					}
				}
			}
		}


		GUILayout.Space(25.0f);


		curve.DrawCurveGizmo = EditorGUILayout.Toggle("Draw Curve?", curve.DrawCurveGizmo);
		if (!curve.DrawCurveGizmo)
		{
			SceneView.RepaintAll();
		}


		Color oldCol = curve.GizmoColor;
		float oldSize = curve.GizmoSize;

		curve.GizmoColor = EditorGUILayout.ColorField("Gizmo Color", curve.GizmoColor);
		curve.GizmoSize = EditorGUILayout.FloatField("Gizmo Size", curve.GizmoSize);
		
		if (oldSize != curve.GizmoSize || oldCol != curve.GizmoColor)
		{
			SceneView.RepaintAll();
		}


		if (curve.DrawCurveGizmo)
		{
			int oldLine = curve.GizmoLineQuality;
			float oldPerp = curve.GizmoLinePerpScale;

			curve.GizmoLineQuality = EditorGUILayout.IntSlider("Gizmo Line Quality",
															   curve.GizmoLineQuality, 1, 100);
			curve.GizmoLinePerpScale = EditorGUILayout.FloatField("Gizmo Line Perp Scale",
																  curve.GizmoLinePerpScale);

			if (oldLine != curve.GizmoLineQuality || oldPerp != curve.GizmoLinePerpScale)
			{
				SceneView.RepaintAll();
			}
		}
	}
	public void OnSceneGUI()
	{
		UnityCurve curve = (UnityCurve)target;

		//Place the new branch.
		if (NewBranch != null)
		{
			CurveMesh cm = curve.GetComponent<CurveMesh>();
			if (cm != null)
			{
				RaycastHit hitInfo = new RaycastHit();
				Ray r = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
				if (myMC.Raycast(r, out hitInfo, 99999.0f))
				{
					NewBranch.position = hitInfo.point;
					NewBranch.rotation = Quaternion.FromToRotation((curve.Curve.Points[curve.Curve.Points.Count - 1] -
																	curve.Curve.Points[0]).normalized,
																   hitInfo.normal);

					UnityCurve uc = NewBranch.GetComponent<UnityCurve>();
					uc.Curve.Points[0] = NewBranch.worldToLocalMatrix.MultiplyPoint(hitInfo.point);
				}
			}

			if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
			{
				Undo.RegisterCreatedObjectUndo(NewBranch.gameObject, "Create Branch");

				NewBranch = null;
				DestroyImmediate(myMC);
				SetInspectorLocked(false);
			}
		}
		//Edit the curve points.
		else
		{
			bool movedPoint = false;
			if (SelectedPoint >= 0 && SelectedPoint < curve.Curve.Points.Count)
			{
				Transform tr = curve.transform;
				Matrix4x4 trMat = tr.localToWorldMatrix;

				Vector3 oldPos = curve.Curve.Points[SelectedPoint];
				Vector3 oldPosTr = trMat.MultiplyPoint(oldPos);

				Vector3 newPos = Handles.PositionHandle(oldPosTr,
														Tools.pivotRotation == PivotRotation.Global ?
															Quaternion.identity :
															tr.rotation);
				curve.Curve.Points[SelectedPoint] = trMat.inverse.MultiplyPoint(newPos);

				if (oldPos != curve.Curve.Points[SelectedPoint])
				{
					movedPoint = true;
					Undo.RecordObject(curve, "Edit curve on \"" + curve.gameObject.name + "\"");

					curve.OnValidate();

					//Update the curve's mesh if it exists.
					CurveMesh cm = curve.GetComponent<CurveMesh>();
					if (cm != null)
					{
						cm.OnValidate();
					}
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

					Matrix4x4 toWorld = curve.transform.localToWorldMatrix;

					for (int i = 0; i < curve.Curve.Points.Count; ++i)
					{
						Vector3 worldP = toWorld.MultiplyPoint(curve.Curve.Points[i]);
						float tempDist = DistanceToSphere(mRay, worldP, curve.GizmoSize);
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
	}
	private void OnDestroy()
	{
		if (fixUnityBullshit)
		{
			fixUnityBullshit = false;
			Debug.Log("Quit");
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
	
	private static void AddBranchTo(UnityCurve root, UnityCurve toCopy)
	{
		GameObject bo = new GameObject("Branch");
			
		UnityCurve uc = bo.AddComponent<UnityCurve>();
		uc.DrawCurveGizmo = false;
		uc.Curve = new Curve(toCopy.Curve.Points.ToList());

		CurveMesh cmOld = toCopy.GetComponent<CurveMesh>();
		if (cmOld != null)
		{
			CurveMesh cm = bo.AddComponent<CurveMesh>();
			cm.RadiusAlongCurve = new AnimationCurve(cmOld.RadiusAlongCurve.keys.ToArray());
			cm.RadiusAroundCurve = new AnimationCurve(cmOld.RadiusAroundCurve.keys.ToArray());
			cm.RadiusVarianceAlongCurve = new AnimationCurve(cmOld.RadiusVarianceAlongCurve.keys.ToArray());
			cm.CurveDivisionsAlong = cmOld.CurveDivisionsAlong;
			cm.CurveDivisionsAround = cmOld.CurveDivisionsAround;

			cm.OnValidate();
		}

		MeshRenderer mrOld = toCopy.GetComponent<MeshRenderer>();
		if (mrOld != null)
		{
			MeshRenderer mr = bo.AddComponent<MeshRenderer>();
			mr.sharedMaterial = mrOld.sharedMaterial;
		}

		//Add a mesh collider to this curve for ray-casting.
		myMC = root.GetComponent<MeshCollider>();
		if (myMC == null)
		{
			myMC = root.gameObject.AddComponent<MeshCollider>();
		}

		NewBranch = bo.transform;
		if (root == toCopy)
		{
			NewBranch.localScale = root.transform.localScale * 0.5f;
		}
		else
		{
			NewBranch.localScale = toCopy.transform.localScale;
		}
		NewBranch.parent = root.transform;
	}
}