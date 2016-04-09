using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Rand = UnityEngine.Random;


namespace TreeGen
{
	[RequireComponent(typeof(MeshFilter))]
	public class CurveFoliage : MonoBehaviour
	{
		/// <summary>
		/// The different ways of storing foliage meshes.
		/// </summary>
		public enum MeshModes
		{
			//Each piece of foliage is a 2-triangle quad.
			Quad,
			//Each piece of foliage is a single point with a normal,
			// assumed to be expanded to a billboard in the material's geometry shader,
			// plus a UV coordinate representing the billboard's size along each axis.
			Point,
		}


		public int MinPieces = 15, MaxPieces = 20;
		public float MinSize = 5.0f, MaxSize = 7.0f;

		/// <summary>
		/// The minimum position (from 0 to 1) along the curve that the foliage can start appearing at.
		/// </summary>
		public float MinSpawnLength = 0.5f;
		/// <summary>
		/// Effects the distribution of foliage. 1.0 is an even distribution.
		/// </summary>
		public float SpawnDistribution = 1.0f;

		/// <summary>
		/// 0.0 means facing totally downward, 1.0 means facing totally upward.
		/// </summary>
		public float VerticalAngle = 0.5f;
		public float VerticalAngleVariance = 0.1f;

		public int Seed = 523541;

		public MeshModes Mode = MeshModes.Quad;


		public Mesh MyMesh { get { return myMF.sharedMesh; } set { myMF.sharedMesh = value; } }
		private MeshFilter myMF;


		private struct Piece
		{
			public Vector3 Pos, Normal, Tangent;
			public Vector2 Size;
		}
		/// <summary>
		/// Re-generates the foliage mesh.
		/// </summary>
		public void OnValidate()
		{
			//Make sure inputs are sane.
			VerticalAngle = Mathf.Clamp01(VerticalAngle);
			VerticalAngleVariance = Mathf.Max(0.0f, VerticalAngleVariance);
			MinPieces = Mathf.Max(MinPieces, 0);
			MaxPieces = Mathf.Max(MaxPieces, MinPieces);
			SpawnDistribution = Mathf.Max(0.00001f, SpawnDistribution);
			MinSpawnLength = Mathf.Clamp01(MinSpawnLength);
			MinSize = Mathf.Max(0.0f, MinSize);
			MaxSize = Mathf.Max(MinSize, MaxSize);


			if (myMF == null)
			{
				myMF = GetComponent<MeshFilter>();
			}

			TreeCurve tc = transform.parent.GetComponent<TreeCurve>();
			Vector3[] preC = tc.Curve.PreCalculateValues();

			Rand.seed = Seed;


			//Calculate the position, normal/tangent, and size of each foliage quad.

			List<Piece> pieces = new List<Piece>();

			int nPieces = Rand.Range(MinPieces, MaxPieces);
			pieces.Capacity = nPieces;

			for (int i = 0; i < nPieces; ++i)
			{
				float t = Mathf.Lerp(MinSpawnLength, 1.0f,
									 Mathf.Pow(Rand.value, SpawnDistribution));
				var posAndDerivative = tc.Curve.GetValueAndDerivative(t, preC);
				posAndDerivative.Derivative.Normalize();
				Vector3 perp = posAndDerivative.Perpendicular.normalized;

				float sizeF = Rand.Range(MinSize, MaxSize);
				Vector2 size = new Vector2(sizeF, sizeF);

				Vector3 curveNormal = Quaternion.AngleAxis(Rand.Range(5.0f, 355.0f),
														   posAndDerivative.Derivative) * perp;
				curveNormal = new Vector3(curveNormal.x, 0.0f, curveNormal.z).normalized;

				Piece p = new Piece();
				p.Pos = posAndDerivative.Value + (size.y * 0.5f * curveNormal);
				p.Normal = Vector3.Cross(curveNormal, new Vector3(0.0f, 1.0f, 0.0f)).normalized;
				float angle = Mathf.Lerp(-90.0f, 90.0f,
										 VerticalAngle + Rand.Range(-VerticalAngleVariance,
																	VerticalAngleVariance));
				p.Normal = Quaternion.AngleAxis(angle, curveNormal) * p.Normal;
				p.Tangent = curveNormal;
				p.Size = size;

				pieces.Add(p);
			}


			//Convert the "pieces" list into a mesh.

			if (MyMesh == null)
			{
				MyMesh = new Mesh();
			}
			else
			{
				MyMesh.Clear();
			}

			switch (Mode)
			{
				case MeshModes.Point:
					MyMesh.vertices = pieces.Select(p => p.Pos).ToArray();
					MyMesh.normals = pieces.Select(p => p.Normal).ToArray();
					MyMesh.tangents = pieces.Select(p =>
						{
							return new Vector4(p.Tangent.x, p.Tangent.y, p.Tangent.z, 1.0f);
						}).ToArray();
					MyMesh.uv = pieces.Select(p => p.Size).ToArray();

					int i = -1;
					MyMesh.SetIndices(pieces.Select(p => { i += 1; return i; }).ToArray(),
													MeshTopology.Points, 0);
					break;

				case MeshModes.Quad:

					Vector3[] poses = new Vector3[pieces.Count * 4],
							  normals = new Vector3[pieces.Count * 4];
					Vector4[] tangents = new Vector4[pieces.Count * 4];
					Vector2[] uvs = new Vector2[pieces.Count * 4];
					int[] indices = new int[pieces.Count * 6];

					for (int j = 0; j < pieces.Count; ++j)
					{
						Piece p = pieces[j];

						Vector3 bitangent = Vector3.Cross(p.Normal, p.Tangent).normalized;

						Vector3 deltaX = 0.5f * p.Tangent * p.Size.y,
								deltaY = 0.5f * bitangent * p.Size.x;
						
						int vIndex = j * 4,
							iIndex = j * 6;

						poses[vIndex] = p.Pos + (-deltaX + -deltaY);
						uvs[vIndex] = new Vector2(0.0f, 0.0f);
						poses[vIndex + 1] = p.Pos + (deltaX + -deltaY);
						uvs[vIndex + 1] = new Vector2(1.0f, 0.0f);
						poses[vIndex + 2] = p.Pos + (-deltaX + deltaY);
						uvs[vIndex + 2] = new Vector2(0.0f, 1.0f);
						poses[vIndex + 3] = p.Pos + (deltaX + deltaY);
						uvs[vIndex + 3] = new Vector2(1.0f, 1.0f);

						normals[vIndex] = p.Normal;
						normals[vIndex + 1] = p.Normal;
						normals[vIndex + 2] = p.Normal;
						normals[vIndex + 3] = p.Normal;

						Vector4 tang = new Vector4(p.Tangent.x, p.Tangent.y, p.Tangent.z, 1.0f);
						tangents[vIndex] = tang;
						tangents[vIndex + 1] = tang;
						tangents[vIndex + 2] = tang;
						tangents[vIndex + 3] = tang;

						indices[iIndex] = vIndex;
						indices[iIndex + 1] = vIndex + 1;
						indices[iIndex + 2] = vIndex + 3;
						indices[iIndex + 3] = vIndex;
						indices[iIndex + 4] = vIndex + 3;
						indices[iIndex + 5] = vIndex + 2;
					}

					MyMesh.vertices = poses;
					MyMesh.normals = normals;
					MyMesh.tangents = tangents;
					MyMesh.uv = uvs;
					MyMesh.SetIndices(indices, MeshTopology.Triangles, 0);
					break;

				default: throw new NotImplementedException(Mode.ToString());
			}

			MyMesh.UploadMeshData(false);
		}


		void Start()
		{
			OnValidate();
		}
	}
}