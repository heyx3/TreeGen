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
				Undo.RecordObject(curve, "Edit curve on \"" + curve.gameObject.name + "\"");

				curve.OnValidate();

				CurveMesh cm = curve.GetComponent<CurveMesh>();
				if (cm != null)
				{
					cm.OnValidate();
				}

				SceneView.RepaintAll();
			}
		}

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
	}
	private void OnDestroy()
	{
		if (fixUnityBullshit)
		{
			fixUnityBullshit = false;
			Debug.Log("Quit");
			return;
		}

		Debug.Log("OnDestroyCurveEditor");

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