using System;
using Vector2 = UnityEngine.Vector2;

public struct Vector2i
{
	public int x, y;


	public Vector2i(int _x, int _y) { x = _x; y = _y; }


	public static bool operator==(Vector2i lhs, Vector2i rhs)
	{
		return lhs.x == rhs.x && lhs.y == rhs.y;
	}
	public static bool operator!=(Vector2i lhs, Vector2i rhs)
	{
		return lhs.x != rhs.x || lhs.y != rhs.y;
	}
	
	public static Vector2i operator+(Vector2i lhs, Vector2i rhs)
	{
		return new Vector2i(lhs.x + rhs.x, lhs.y + rhs.y);
	}
	public static Vector2i operator-(Vector2i lhs, Vector2i rhs)
	{
		return new Vector2i(lhs.x - rhs.x, lhs.y - rhs.y);
	}
	public static Vector2i operator*(Vector2i lhs, int rhs)
	{
		return new Vector2i(lhs.x * rhs, lhs.y * rhs);
	}
	public static Vector2i operator/(Vector2i lhs, int rhs)
	{
		return new Vector2i(lhs.x / rhs, lhs.y / rhs);
	}

	
	public Vector2i LessX { get { return new Vector2i(x - 1, y); } }
	public Vector2i LessY { get { return new Vector2i(x, y - 1); } }
	public Vector2i MoreX { get { return new Vector2i(x + 1, y); } }
	public Vector2i MoreY { get { return new Vector2i(x, y + 1); } }


	public override string ToString()
	{
		return "{" + x + ", " + y + "}";
	}
	public override int GetHashCode()
	{
		return (x * 73856093) ^ (y * 19349663);
	}
	public int GetHashCode(int z)
	{
		return (this * z).GetHashCode();
	}
	public override bool Equals(object obj)
	{
		return (obj is Vector2i) && ((Vector2i)obj) == this;
	}
}

/// <summary>
/// Add some stuff to Unity's Vector classes.
/// </summary>
public static class Extensions
{
	public static Vector2 GetPerp1(this Vector2 v) { return new Vector2(v.y, -v.x); }
	public static Vector2 GetPerp2(this Vector2 v) { return new Vector2(-v.y, v.x); }

	public static float DistanceSqr(this Vector2 v, Vector2 otherV) { return (otherV - v).sqrMagnitude; }
}