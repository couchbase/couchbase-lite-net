
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Threading;

public static class SerializationHelpers
{
	public static void InsertIntoDictionary(this Vector2 vector, IDictionary<string, object> dict, params string[] keys)
	{
		InsertIntoDictionary (dict, keys, new object[] { vector.x, vector.y });
	}

	public static void InsertIntoDictionary(this Vector3 vector, IDictionary<string, object> dict, params string[] keys)
	{
		InsertIntoDictionary (dict, keys, new object[] { vector.x, vector.y, vector.z });
	}

	public static void InsertIntoDictionary(this Quaternion quaternion, IDictionary<string, object> dict, params string[] keys)
	{
		InsertIntoDictionary (dict, keys, new object[] { quaternion.x, quaternion.y, quaternion.z, quaternion.w });
	}

	public static void InsertIntoDictionary(this Color color, IDictionary<string, object> dict, params string[] keys)
	{
		InsertIntoDictionary (dict, keys, new object[] { color.r, color.g, color.b, color.a });
	}

	public static Vector3 Vector2FromKeys(this IDictionary<string, object> dict, params string[] keys)
	{
		return Populate<Vector2> (() =>
		{
			var retVal = new Vector2();
			retVal.x = Convert.ToSingle(dict[keys[0]]);
			retVal.y = Convert.ToSingle(dict[keys[1]]);

			return retVal;
		});
	}

	public static Vector3 Vector3FromKeys(this IDictionary<string, object> dict, params string[] keys)
	{
		return Populate<Vector3> (() =>
		{
			var retVal = new Vector3();
			retVal.x = Convert.ToSingle(dict[keys[0]]);
			retVal.y = Convert.ToSingle(dict[keys[1]]);
			retVal.z = Convert.ToSingle(dict[keys[2]]);

			return retVal;
		});
	}

	public static Quaternion QuaternionFromKeys(this IDictionary<string, object> dict, params string[] keys)
	{
		return Populate<Quaternion> (() => 
		{
			var retVal = new Quaternion();
			retVal.x = Convert.ToSingle(dict[keys[0]]);
			retVal.y = Convert.ToSingle(dict[keys[1]]);
			retVal.z = Convert.ToSingle(dict[keys[2]]);
			retVal.w = Convert.ToSingle(dict[keys[3]]);

			return retVal;
		});
	}

	public static Color ColorFromKeys(this IDictionary<string, object> dict, params string[] keys)
	{
		return Populate<Color> (() =>
		{
			var retVal = new Color();
			retVal.r = Convert.ToSingle(dict[keys[0]]);
			retVal.g = Convert.ToSingle(dict[keys[1]]);
			retVal.b = Convert.ToSingle(dict[keys[2]]);
			retVal.a = Convert.ToSingle(dict[keys[3]]);

			return retVal;
		});
	}

	private static T Populate<T>(Func<T> initializer)
	{
		try {
			return initializer();
		} catch(KeyNotFoundException) {
			Debug.LogError(String.Format("Missing key(s) for {0}", typeof(T).ToString()));
			throw;
		}
	}

	private static void InsertIntoDictionary(IDictionary<string, object> dict, IEnumerable<string> keys, IEnumerable<object> vals)
	{
		if (keys.Count () < vals.Count ()) {
			throw new ArgumentOutOfRangeException("keys", String.Format("Not enough keys ({0}) for vals ({1})", keys.Count(), vals.Count()));
		}

		for(int i = 0; i < vals.Count(); i++) {
			dict[keys.ElementAt(i)] = vals.ElementAt(i);
		}
	}
}

