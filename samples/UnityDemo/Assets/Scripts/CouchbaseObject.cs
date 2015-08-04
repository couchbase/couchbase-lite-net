using UnityEngine;
using System.Collections;
using Couchbase.Lite;
using System.Collections.Generic;
using System;

public abstract class CouchbaseObject : MonoBehaviour {

	#region Constants

	private static readonly string[] ROTATION_KEYS = new string[] { "CouchbaseObject.Rotation.X", "CouchbaseObject.Rotation.Y",
		"CouchbaseObject.Rotation.Z", "CouchbaseObject.Rotation.W" };

	private static readonly string[] SCALE_KEYS = new string[] { "CouchbaseObject.Scale.X", "CouchbaseObject.Scale.Y",
		"CouchbaseObject.Scale.Z" };

	private static readonly string[] COLOR_KEYS = new string[] { "CouchbaseObject.Color.R", "CouchbaseObject.Color.G",
		"CouchbaseObject.Color.B", "CouchbaseObject.Color.A" };

	#endregion

	#region Members

	[Flags]
	protected enum SaveData : uint
	{
		PositionXY = 1,
		PositionZ = 1 << 1,
		Position = PositionXY | PositionZ,
		Rotation = 1 << 2,
		Scale = 1 << 3,
		Color = 1 << 4,
		Everything = 0xFFFFFFFF
	}

	private Document _doc;

	#endregion

	#region Constructors



	#endregion

	#region Properties

	//true if the object has changed since its last save
	protected abstract bool IsDirty { get; }

	public Database Database { get; set; }

	#endregion

	#region Public Methods

	/// <summary>
	/// Restores the CouchbaseObject from its associated document
	/// </summary>
	/// <param name="doc">The document containing the information for the object</param>
	public void RestoreFromDocument (Document doc)
	{
		if (doc == null) {
			return;
		}
		var props = doc.UserProperties;
		RestoreFromDocumentProperties (props);
		_doc = doc;
		Database = doc.Database;
	}

	/// <summary>
	/// Saves the CouchbaseObject to its associated document
	/// </summary>
	/// <returns>The newly saved revision, or the current one if no changes were made</returns>
	public SavedRevision SaveToDocument ()
	{
		if (_doc != null && !IsDirty) {
			return _doc.CurrentRevision;
		}
		
		if (_doc == null) {
			_doc = Database.CreateDocument();
		}
		
		return _doc.Update (rev =>
		{
			return UpdateRevision(rev);
		});
	}

	#endregion

	#region Protected Methods

	protected abstract bool UpdateRevision (UnsavedRevision rev);
	
	protected abstract void RestoreFromDocumentProperties (IDictionary<string, object> props);

	protected void EmitBaseData(UnsavedRevision rev, SaveData dataToSave)
	{
		if (dataToSave.HasFlag (SaveData.PositionXY)) {
			var pos = transform.position;
			rev.Properties ["CouchbaseObject.Position.X"] = pos.x;
			rev.Properties ["CouchbaseObject.Position.Y"] = pos.y;

			if(dataToSave.HasFlag(SaveData.PositionZ)) {
				rev.Properties ["CouchbaseObject.Position.Z"] = pos.z;
			}
		}

		if (dataToSave.HasFlag (SaveData.Rotation)) {
			transform.rotation.InsertIntoDictionary(rev.Properties, ROTATION_KEYS);
		}

		if (dataToSave.HasFlag (SaveData.Scale)) {
			transform.localScale.InsertIntoDictionary(rev.Properties, SCALE_KEYS);
		}

		if (dataToSave.HasFlag (SaveData.Color)) {
			gameObject.GetComponent<MeshRenderer>().material.color.InsertIntoDictionary(rev.Properties, COLOR_KEYS);
		}
	}

	protected void RestoreBaseData(IDictionary<string, object> props, SaveData dataToRestore)
	{
		if (dataToRestore.HasFlag (SaveData.PositionXY)) {
			var pos = transform.position;
			pos.x = Convert.ToSingle(props["CouchbaseObject.Position.X"]);
			pos.y = Convert.ToSingle(props["CouchbaseObject.Position.Y"]);
			
			if(dataToRestore.HasFlag(SaveData.PositionZ)) {
				pos.z = Convert.ToSingle(props["CouchbaseObject.Position.Z"]);
			}
			transform.position = pos;
		}
		
		if (dataToRestore.HasFlag (SaveData.Rotation)) {
			transform.rotation = props.QuaternionFromKeys(ROTATION_KEYS);
		}
		
		if (dataToRestore.HasFlag (SaveData.Scale)) {
			transform.localScale = props.Vector3FromKeys(SCALE_KEYS);
		}
		
		if (dataToRestore.HasFlag (SaveData.Color)) {
			gameObject.GetComponent<MeshRenderer>().material.color = props.ColorFromKeys(COLOR_KEYS);
		}
	}

	#endregion

}
