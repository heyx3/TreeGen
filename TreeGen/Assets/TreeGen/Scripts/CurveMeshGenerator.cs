using System;
using System.Collections.Generic;
using UnityEngine;

using Rand = UnityEngine.Random;


namespace TreeGen
{
	public static class CurveMeshGenerator
	{
		public static void CalculateMeshTangents(Mesh mesh)
		{
			//Taken from here: http://answers.unity3d.com/questions/7789/calculating-tangents-vector4.html

			//Speed up math by copying the mesh arrays.
			int[] triangles = mesh.triangles;
			Vector3[] vertices = mesh.vertices;
			Vector2[] uv = mesh.uv;
			Vector3[] normals = mesh.normals;

			int triangleCount = triangles.Length;
			int vertexCount = vertices.Length;

			Vector3[] tan1 = new Vector3[vertexCount];
			Vector3[] tan2 = new Vector3[vertexCount];

			Vector4[] tangents = new Vector4[vertexCount];

			for (long a = 0; a < triangleCount; a += 3)
			{
				long i1 = triangles[a + 0];
				long i2 = triangles[a + 1];
				long i3 = triangles[a + 2];

				Vector3 v1 = vertices[i1];
				Vector3 v2 = vertices[i2];
				Vector3 v3 = vertices[i3];

				Vector2 w1 = uv[i1];
				Vector2 w2 = uv[i2];
				Vector2 w3 = uv[i3];

				float x1 = v2.x - v1.x;
				float x2 = v3.x - v1.x;
				float y1 = v2.y - v1.y;
				float y2 = v3.y - v1.y;
				float z1 = v2.z - v1.z;
				float z2 = v3.z - v1.z;

				float s1 = w2.x - w1.x;
				float s2 = w3.x - w1.x;
				float t1 = w2.y - w1.y;
				float t2 = w3.y - w1.y;

				float r = 1.0f / (s1 * t2 - s2 * t1);

				Vector3 sdir = new Vector3((t2 * x1 - t1 * x2) * r,
										   (t2 * y1 - t1 * y2) * r,
										   (t2 * z1 - t1 * z2) * r);
				Vector3 tdir = new Vector3((s1 * x2 - s2 * x1) * r,
										   (s1 * y2 - s2 * y1) * r,
										   (s1 * z2 - s2 * z1) * r);

				tan1[i1] += sdir;
				tan1[i2] += sdir;
				tan1[i3] += sdir;

				tan2[i1] += tdir;
				tan2[i2] += tdir;
				tan2[i3] += tdir;
			}


			for (long a = 0; a < vertexCount; ++a)
			{
				Vector3 n = normals[a];
				Vector3 t = tan1[a];

				Vector3.OrthoNormalize(ref n, ref t);
				tangents[a].x = t.x;
				tangents[a].y = t.y;
				tangents[a].z = t.z;

				tangents[a].w = (Vector3.Dot(Vector3.Cross(n, t), tan2[a]) < 0.0f) ? -1.0f : 1.0f;
			}

			mesh.tangents = tangents;
		}

		public static void GenerateMesh(Mesh m, Curve c, float radiusScale,
										AnimationCurve radiusAlongCurve,
										AnimationCurve radiusAroundCurve,
										AnimationCurve radiusVariance,
										int seed, int divisionsAlong = 20, int divisionsAround = 5)
		{
			Rand.seed = seed;

			Vector3[] cV = c.PreCalculateValues();


			//First generate the positions/UV's along the curve.

			Vector3[] meshPoses = new Vector3[(divisionsAlong * divisionsAround) + 2];
			Vector2[] meshUVs = new Vector2[(divisionsAlong * divisionsAround) + 2];


			//Add a vertex at the beginning and end of the curve.

			meshPoses[meshPoses.Length - 2] = c.GetValue(0.0f, cV);
			meshPoses[meshPoses.Length - 1] = c.GetValue(1.0f, cV);
			meshUVs[meshUVs.Length - 2] = new Vector2(0.5f, 1.0f);
			meshUVs[meshUVs.Length - 1] = new Vector2(0.5f, 0.0f);


			//At each division along the curve, generate a ring of vertices.

			float rotateIncrement = 360.0f / divisionsAround;
			float moveIncrement = 1.0f / (float)(divisionsAlong - 1);

			for (int i = 0; i < divisionsAlong; ++i)
			{
				float t = moveIncrement * (float)i;

				var valAndDerivative = c.GetValueAndDerivative(t, cV);
				valAndDerivative.Derivative.Normalize();
				Vector3 perp = valAndDerivative.Perpendicular.normalized;
				Quaternion rot = Quaternion.AngleAxis(rotateIncrement, valAndDerivative.Derivative);

				float variance = radiusVariance.Evaluate(t),
					  radiusBase = radiusAlongCurve.Evaluate(t);

				for (int j = 0; j < divisionsAround; ++j)
				{
					float jLerp = (float)j / (float)(divisionsAround - 1);
					float radius = radiusAroundCurve.Evaluate(jLerp) *
								   radiusScale *
								   (radiusBase + Rand.Range(-variance, variance));
					
					int index = j + (i * divisionsAround);
					meshPoses[index] = valAndDerivative.Value + (perp * radius);
					meshUVs[index] = new Vector2((float)j / (float)(divisionsAround + 1),
												 (float)i / (float)divisionsAlong);
					
					perp = rot * perp;
				}
			}

			
			//Next, generate indices.

			int indicesPerRing = divisionsAround * 2 * 3,
				indicesPerCap = divisionsAround * 3;
			int[] meshTris = new int[(indicesPerRing * (divisionsAlong - 1)) +
									 (indicesPerCap * 2)];

			//Generate indices along the curve.
			int triIndex = 0;
			for (int i = 1; i < divisionsAlong; ++i)
			{
				int prevStartVertex = (i - 1) * divisionsAround,
					startVertex = i * divisionsAround;

				for (int j = 0; j < divisionsAround; ++j)
				{
					int nextJ = (j + 1) % divisionsAround;

					meshTris[triIndex] = prevStartVertex + j;
					meshTris[triIndex + 1] = startVertex + nextJ;
					meshTris[triIndex + 2] = startVertex + j;
					meshTris[triIndex + 3] = prevStartVertex + j;
					meshTris[triIndex + 4] = prevStartVertex + nextJ;
					meshTris[triIndex + 5] = startVertex + nextJ;
					triIndex += 6;
				}
			}

			//Generate indices for the end caps of the curve.
			int topStartVert = (divisionsAlong - 1) * divisionsAround;
			for (int i = 0; i < divisionsAround; ++i)
			{
				int nextI = (i + 1) % divisionsAround;

				meshTris[triIndex] = nextI;
				meshTris[triIndex + 1] = i;
				meshTris[triIndex + 2] = meshPoses.Length - 2;

				meshTris[triIndex + indicesPerCap] = topStartVert + i;
				meshTris[triIndex + indicesPerCap + 1] = topStartVert + nextI;
				meshTris[triIndex + indicesPerCap + 2] = meshPoses.Length - 1;

				triIndex += 3;
			}


			//Finally, generate and return the mesh object.

			m.Clear();

			m.vertices = meshPoses;
			m.uv = meshUVs;
			m.triangles = meshTris;

			m.RecalculateBounds();
			m.RecalculateNormals();
			CalculateMeshTangents(m);

			m.UploadMeshData(true);
		}
	}
}