using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


/// <summary>
/// Adds various useful methods to IEnumerable&lt;T&gt;.
/// </summary>
public static class IEnumerableExtensions
{
	/// <summary>
	/// Converts all elements using the given function
	/// and ignores any values that were converted to "null".
	/// </summary>
	public static IEnumerable<OutType> SelectSome<InType, OutType>(this IEnumerable<InType> collection,
																   Func<InType, OutType> converterFunc)
		where OutType : class
	{
		foreach (InType it in collection)
		{
			OutType ot = converterFunc(it);
			if (ot != null)
			{
				yield return ot;
			}
		}
	}
	
	/// <summary>
	/// Returns the given collection but with no duplicates.
	/// </summary>
	public static IEnumerable<T> RemoveDuplicates<T>(this IEnumerable<T> collection)
	{
		List<T> doneAlready = new List<T>();
		foreach (T t in collection)
		{
			if (!doneAlready.Contains(t))
			{
				doneAlready.Add(t);
				yield return t;
			}
		}
	}


	/// <summary>
	/// Removes any GameObjects/components from the given collection
	/// that are children of at least one other GameObject/component in the collection.
	/// </summary>
	public static IEnumerable<T> RemoveChildren<T>(IEnumerable<T> elements, Func<T, Transform> getTR)
	{
		T[] objs = elements.ToArray();
		Transform[] objsT = objs.Select(getTR).ToArray();

		for (int i = 0; i < objs.Length; ++i)
		{
			Transform myTr = objsT[i];

			//Get whether this object is a child of any other object in the collection.
			bool isChild = false;
			if (myTr.parent != null)
			{
				for (int j = 0; j < objs.Length && !isChild; ++j)
				{
					if (i != j)
					{
						//See if any of object i's parents is object j.
						Transform parent = myTr.parent;
						while (parent != null)
						{
							if (parent == objsT[j])
							{
								isChild = true;
								break;
							}
							parent = parent.parent;
						}
					}
				}
			}


			if (!isChild)
			{
				yield return objs[i];
			}
		}
	}
	
	/// <summary>
	/// Removes any GameObjects from the given collection
	/// that are children of at least one other GameObject in the collection.
	/// </summary>
	public static IEnumerable<GameObject> RemoveChildren(this IEnumerable<GameObject> objects)
	{
		return RemoveChildren(objects, go => go.transform);
	}
	/// <summary>
	/// Removes any components from the given collection
	/// that are children of at least one other component in the collection.
	/// </summary>
	public static IEnumerable<T> RemoveChildren<T>(this IEnumerable<T> components)
		where T : Component
	{
		return RemoveChildren(components, co => co.transform);
	}


	/// <summary>
	/// Gets each component's sibling component of type "K".
	/// </summary>
	public static IEnumerable<K> GetComponents<T, K>(this IEnumerable<T> objects)
		where T : Component
		where K : Component
	{
		foreach (T t in objects)
			yield return t.GetComponent<K>();
	}
	/// <summary>
	/// Gets each object's component of type "T". Optionally doesn't return "null" values.
	/// </summary>
	public static IEnumerable<T> GetComponents<T>(this IEnumerable<GameObject> objects, bool ignoreNulls)
		where T : Component
	{
		foreach (GameObject go in objects)
		{
			T t = go.GetComponent<T>();
			if (!ignoreNulls || t != null)
			{
				yield return t;
			}
		}
	}

	/// <summary>
	/// Returns all components of type "K" in the given components' objects and/or their children.
	/// </summary>
	public static IEnumerable<K> GetComponentsInChildren<T, K>(this IEnumerable<T> objects)
		where T : Component
		where K : Component
	{
		foreach (T t in objects)
			foreach (K k in t.GetComponentsInChildren<K>())
				yield return k;
	}
	/// <summary>
	/// Returns all components in the given collection plus all components in those objects' children.
	/// </summary>
	public static IEnumerable<T> GetComponentsInChildren<T>(this IEnumerable<T> objects)
		where T : Component
	{
		return GetComponentsInChildren<T, T>(objects);
	}
	/// <summary>
	/// Returns all components that belong to an object in the given collection or one of its children.
	/// </summary>
	public static IEnumerable<T> GetComponentsInChildren<T>(this IEnumerable<GameObject> objects)
		where T : Component
	{
		foreach (GameObject go in objects)
			foreach (T t in go.GetComponentsInChildren<T>())
				yield return t;
	}
}