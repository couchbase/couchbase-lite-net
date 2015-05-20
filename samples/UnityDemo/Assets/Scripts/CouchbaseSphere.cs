using UnityEngine;
using Couchbase.Lite;
using System.Collections.Generic;

public class CouchbaseSphere : CouchbaseObject {

	protected override bool IsDirty {
		get {
			return transform.hasChanged;
		}
	}

	void OnMouseUp()
	{
		GetComponent<CouchbaseSphere> ().SaveToDocument ();
	}

	void Awake()
	{
		MeshRenderer gameObjectRenderer = gameObject.GetComponent<MeshRenderer>();
		gameObjectRenderer.material = new Material (Shader.Find ("Diffuse"));
		var rand = new System.Random();
		gameObjectRenderer.material.color = new Color((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble());
	}

	protected override void RestoreFromDocumentProperties (IDictionary<string, object> props)
	{
		RestoreBaseData (props, SaveData.Position | SaveData.Color);
	}

	protected override bool UpdateRevision (UnsavedRevision rev)
	{
		rev.Properties ["type"] = "CouchbaseSphere";
		EmitBaseData (rev, SaveData.Position | SaveData.Color);
		return true;
	}

}
