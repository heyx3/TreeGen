using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;


namespace TreeGen
{
	/// <summary>
	/// A Bezier curve with any number of control points.
	/// Source: https://en.wikipedia.org/wiki/B%C3%A9zier_curve#Polynomial_form
	/// </summary>
	[Serializable]
	public struct Curve
	{
		public struct ValueAndDerivative
		{
			public Vector3 Value, Derivative;

			/// <summary>
			/// Calculates a perpendicular to the curve at this value.
			/// Is NOT necessarily normalized!
			/// </summary>
			public Vector3 Perpendicular
			{
				get
				{
					if (Mathf.Abs(Derivative.z) >= 1.0f)
					{
						return Vector3.Cross(Derivative, new Vector3(1.0f, 0.0f, 0.0f));
					}
					else
					{
						return Vector3.Cross(Derivative, new Vector3(0.0f, 0.0f, 1.0f));
					}
				}
			}
		
			public ValueAndDerivative(Vector3 val, Vector3 derivative)
			{
				Value = val;
				Derivative = derivative;
			}
		}


		private static List<List<float>> binomialCoefficients = new List<List<float>>();
		private static float GetCoefficient(int n, int i)
		{
			while (binomialCoefficients.Count <= n)
			{
				binomialCoefficients.Add(new List<float>());
			}
			while (binomialCoefficients[n].Count <= i)
			{
				binomialCoefficients[n].Add((float)Factorial(n) /
											(float)(Factorial(i) * Factorial(n - i)));
			}

			return binomialCoefficients[n][i];
		}
		private static int Factorial(int i)
		{
			int val = 1;
			for (int j = i; j > 0; --j)
				val *= j;
			return val;
		}


		public List<Vector3> Points;


		//public Curve()							 : this(Vector3.zero, Vector3.one)		  { }
		public Curve(Vector3 start, Vector3 end) : this(new List<Vector3> { start, end }) { }
		public Curve(List<Vector3> points)												  { Points = points; }


		/// <summary>
		/// Adds a new control point to the curve in a way that doesn't change its shape.
		/// </summary>
		public void AddControlPoint()
		{
			List<Vector3> newPoints = new List<Vector3>(Points.Count + 1);

			float denom = 1.0f / (float)Points.Count;

			newPoints.Add(Points[0] * (float)Points.Count * denom);
			for (int i = 1; i < Points.Count; ++i)
			{
				newPoints.Add((Points[i - 1] * (float)i * denom) +
							  (Points[i] * (float)(Points.Count - i) * denom));
			}
			newPoints.Add(Points[Points.Count - 1] * (float)Points.Count * denom);

			Points = newPoints;
		}

		/// <summary>
		/// Pre-calculates complicated values that are used when interpolating along this curve.
		/// Pass the return value into calls to "GetValue".
		/// These values should be recalculated whenever any of the points in the curve change.
		/// </summary>
		public Vector3[] PreCalculateValues()
		{
			Vector3[] vals = new Vector3[Points.Count];

			float n = (float)(Points.Count - 1);

			for (int j = 0; j < vals.Length; ++j)
			{
				float term1 = 1.0f;
				for (int m = 0; m <= (j - 1); ++m)
				{
					term1 *= (n - m);
				}

				Vector3 term2 = Vector3.zero;
				for (int i = 0; i <= j; ++i)
				{
					term2 += (Points[i] * Mathf.Pow(-1.0f, (float)(i + j))) /
							 (float)(Factorial(i) * Factorial(j - i));
				}

				vals[j] = term1 * term2;
			}

			return vals;
		}

		public Vector3 GetValue(float t, Vector3[] precalculatedValues)
		{
			Vector3 val = Vector3.zero;
			for (int i = 0; i < Points.Count; ++i)
			{
				val += precalculatedValues[i] * Mathf.Pow(t, (float)i);
			}
			return val;
		}
		public ValueAndDerivative GetValueAndDerivative(float t, Vector3[] precalculatedValues)
		{
			ValueAndDerivative v = new ValueAndDerivative();
			v.Value = Vector3.zero;
			v.Derivative = Vector3.zero;

			int n = Points.Count - 1;

			for (int i = 0; i < Points.Count; ++i)
			{
				v.Value += precalculatedValues[i] * Mathf.Pow(t, (float)i);
				if (i < Points.Count - 1)
				{
					v.Derivative += (Points[i + 1] - Points[i]) *
									GetCoefficient(n - 1, i) *
									Mathf.Pow(t, (float)i) *
									Mathf.Pow(1.0f - t, (float)(n - 1 - i));
				}
			}
			v.Derivative *= (float)Points.Count;

			return v;
		}
	}
}