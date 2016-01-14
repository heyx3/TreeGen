using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace TreeGen
{
	public class UnityCurve : MonoBehaviour
	{
		public Curve Curve;

		public bool DrawCurveGizmo = true;
		public Color GizmoColor = Color.white;
		public float GizmoSize = 0.175f;
		public int GizmoLineQuality = 8;
		public float GizmoLinePerpScale = 0.04f;


		void OnDrawGizmos()
		{
			Matrix4x4 toWorld = transform.localToWorldMatrix;

			Gizmos.color = GizmoColor;

			List<Vector3> transformedPoses = Curve.Points.Select(v => toWorld.MultiplyPoint(v)).ToList();
			Curve curve = new Curve(transformedPoses);

			//Draw the control points.
			for (int i = 0; i < curve.Points.Count; ++i)
			{
				Gizmos.DrawSphere(curve.Points[i], GizmoSize);
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
			float increment = 1.0f / (float)GizmoLineQuality;

			for (int i = 0; i < GizmoLineQuality; ++i)
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

		public void OnValidate()
		{
			//Move the transform and curve so that the object is positioned at the first point.
			if (Curve.Points.Count > 0)
			{
				Transform tr = transform;
				Matrix4x4 toWorld = tr.localToWorldMatrix;

				Vector3 worldP0 = toWorld.MultiplyPoint(Curve.Points[0]),
						worldPos = tr.position;

				tr.position = worldP0;
				for (int i = Curve.Points.Count - 1; i >= 0; --i)
					Curve.Points[i] -= Curve.Points[0];
			}

			CurveMesh m = GetComponent<CurveMesh>();
			if (m != null)
			{
				m.OnValidate();
			}
		}
	}
}