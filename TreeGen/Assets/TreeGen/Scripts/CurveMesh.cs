using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace TreeGen
{
	[RequireComponent(typeof(UnityCurve))]
	[RequireComponent(typeof(MeshFilter))]
	[ExecuteInEditMode]
	public class CurveMesh : MonoBehaviour
	{
		public AnimationCurve RadiusAlongCurve = new AnimationCurve(new Keyframe(0.0f, 0.15f),
																	new Keyframe(1.0f, 0.15f)),
							  RadiusAroundCurve = new AnimationCurve(new Keyframe(0.0f, 1.0f),
																	 new Keyframe(1.0f, 1.0f)),
							  RadiusVarianceAlongCurve = new AnimationCurve(new Keyframe(0.0f, 0.02f),
																			new Keyframe(1.0f, 0.02f));
		public int CurveDivisionsAlong = 30,
				   CurveDivisionsAround = 5;
		public int Seed = 978342;


		public Curve Curve { get { return uc.Curve; } }
		public Mesh Mesh { get { return mf.sharedMesh; } }

		private UnityCurve uc = null;
		private MeshFilter mf = null;


		void Awake()
		{
			uc = GetComponent<UnityCurve>();
			mf = GetComponent<MeshFilter>();

			mf.sharedMesh = new Mesh();
		}

		public void OnValidate()
		{
			if (mf == null || mf.sharedMesh == null)
			{
				Awake();
			}

			CurveDivisionsAlong = Mathf.Max(2, CurveDivisionsAlong);
			CurveDivisionsAround = Mathf.Max(3, CurveDivisionsAround);

			//Generate the mesh.
			CurveMeshGenerator.GenerateMesh(mf.sharedMesh, Curve,
											RadiusAlongCurve, RadiusAroundCurve, RadiusVarianceAlongCurve,
											Seed, CurveDivisionsAlong, CurveDivisionsAround);
		}
	}
}