using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace TreeGen
{
	[RequireComponent(typeof(MeshFilter))]
	[ExecuteInEditMode]
	public class TreeCurve : MonoBehaviour
	{
		public Curve Curve = new Curve(Vector3.zero, Vector3.one);

		public AnimationCurve RadiusAlongCurve = new AnimationCurve(new Keyframe(0.0f, 0.15f),
																	new Keyframe(1.0f, 0.15f)),
							  RadiusAroundCurve = new AnimationCurve(new Keyframe(0.0f, 1.0f),
																	 new Keyframe(1.0f, 1.0f)),
							  RadiusVarianceAlongCurve = new AnimationCurve(new Keyframe(0.0f, 0.02f),
																			new Keyframe(1.0f, 0.02f));

		public float RadiusScale = 1.0f;
		public int CurveDivisionsAlong = 30,
				   CurveDivisionsAround = 5;

		public int Seed = 978342;

		public bool DrawCurveGizmo = true;
		public Color GizmoColor = Color.white;
		public float GizmoSphereRadius = 0.175f;
		public int GizmoCurveSegments = 8;
		public float GizmoLinePerpScale = 0.04f;


		public Mesh Mesh { get { return mf.sharedMesh; } }
		public Transform Tr { get; private set; }

		private MeshFilter mf = null;


		void Awake()
		{
			Tr = transform;
			mf = GetComponent<MeshFilter>();
			mf.sharedMesh = new Mesh();
		}
		public void OnValidate()
		{
			//Make sure this component has been initialized properly.
			if (mf == null || mf.sharedMesh == null)
			{
				Awake();
			}

			//Move the transform and curve so that the object is positioned at the first point.
			if (Curve.Points.Count > 0)
			{
				Matrix4x4 toWorld = Tr.localToWorldMatrix;
				Vector3 worldP0 = toWorld.MultiplyPoint(Curve.Points[0]);

				Tr.position = worldP0;
				for (int i = Curve.Points.Count - 1; i >= 0; --i)
					Curve.Points[i] -= Curve.Points[0];
			}


			CurveDivisionsAlong = Mathf.Max(2, CurveDivisionsAlong);
			CurveDivisionsAround = Mathf.Max(3, CurveDivisionsAround);

			//Regenerate the mesh.
			CurveMeshGenerator.GenerateMesh(mf.sharedMesh, Curve, RadiusScale,
											RadiusAlongCurve, RadiusAroundCurve, RadiusVarianceAlongCurve,
											Seed, CurveDivisionsAlong, CurveDivisionsAround);
		}

		void OnDrawGizmos()
		{
			Matrix4x4 toWorld = transform.localToWorldMatrix;

			Gizmos.color = GizmoColor;

			List<Vector3> transformedPoses = Curve.Points.Select(v => toWorld.MultiplyPoint(v)).ToList();
			Curve curve = new Curve(transformedPoses);

			//Draw the control points.
			for (int i = 0; i < curve.Points.Count; ++i)
			{
				Gizmos.DrawSphere(curve.Points[i], GizmoSphereRadius);
				if (i > 0)
				{
					Gizmos.color = new Color(GizmoColor.r, GizmoColor.g, GizmoColor.b,
											 GizmoColor.a * 0.25f);
					Gizmos.DrawLine(curve.Points[i - 1], curve.Points[i]);
					Gizmos.color = GizmoColor;
				}
			}
			
			if (!DrawCurveGizmo)
				return;


			//Draw the curve.

			Vector3[] precalcValues = curve.PreCalculateValues();

			Vector3 prev = curve.Points[0];
			float increment = 1.0f / (float)GizmoCurveSegments;

			for (int i = 0; i < GizmoCurveSegments; ++i)
			{
				float t = increment * (float)(i + 1);
				var valAndDer = curve.GetValueAndDerivative(t, precalcValues);

				Gizmos.DrawLine(prev, valAndDer.Value);

				Vector3 midpoint = (prev + valAndDer.Value) / 2.0f;
				Gizmos.DrawLine(midpoint,
								midpoint + (valAndDer.Perpendicular * GizmoLinePerpScale));
				Gizmos.DrawLine(midpoint,
								midpoint + (valAndDer.Derivative * GizmoLinePerpScale));

				prev = valAndDer.Value;
			}
		}
	}
}