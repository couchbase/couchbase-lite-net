using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Couchbase.Lite;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading;
using Couchbase.Lite.Util;
using System.IO;
using System.Threading.Tasks;
using Couchbase.Lite.Unity;
using System.Diagnostics;

public class TestScript : MonoBehaviour {
	Database _db;
	Manager _manager;
	LiveQuery _query;
	LiveQuery _reduceQuery;
	private const string TAG = "TestScript";


	int _counter = 0;
	Dictionary<String, GameObject> _spheres = new Dictionary<string, GameObject>();

	Text _counterText;
	
	Replication _pusher;
	Replication _puller;

	public string replicationUrl;

	void Start() {
		Log.SetLogger(new UnityLogger(SourceLevels.All));

		var path = Application.persistentDataPath;
		Log.D (TAG, "Database path: " + path);

		_manager = new Manager (new DirectoryInfo (Application.persistentDataPath), new ManagerOptions { CallbackScheduler = UnityMainThreadScheduler.TaskScheduler } );
		_db = _manager.GetDatabase ("unity_test");

		CreateGameObjectsView ();
		CreateCounterView ();
		StartReplication ();
	}

	void CreateGameObjectsView ()
	{
		var view = _db.GetView ("SphereDocs");
		var success = view.SetMap ((doc, emit) =>  {
			object key;
			var hasType = doc.TryGetValue ("type", out key);
			if (hasType && key.Equals ("CouchbaseSphere")) {
				emit (doc ["_id"], null);
			}
		}, "1.0");
		_query = view.CreateQuery ().ToLiveQuery ();
		_query.Changed += (sender, e) =>  {
			ProcessRows(e.Rows);
		};
		_query.Start ();
	}

	void CreateCounterView ()
	{
		var view = _db.GetView ("SphereCount");
		var success = view.SetMapReduce ((doc, emit) => {
			object key;
			var hasType = doc.TryGetValue ("type", out key);
			if (hasType && key.Equals ("CouchbaseSphere")) {
				emit (doc ["_id"], null);
			}
		},
		(keys, vals, rereduce) => {
			return keys.Count ();
		}, "1.0");
		_reduceQuery = view.CreateQuery ().ToLiveQuery ();
		_reduceQuery.Changed += (sender, e) =>  {
			int counter = _counter;
			Interlocked.CompareExchange(ref _counter, (int)e.Rows.ElementAt(0).Value, counter);
		};
		_reduceQuery.Start ();
	}

	void StartReplication ()
	{
		if (StringEx.IsNullOrWhiteSpace (replicationUrl)) {
			Log.E (TAG, "Replication URL not set");
			return;
		}

		_puller = _db.CreatePullReplication (new Uri(replicationUrl));
		_puller.Continuous = true;
		_puller.Changed += (sender, e) => {
			Log.D (TAG, "Puller: " + _puller.LastError == null ? "Okay" : _puller.LastError.Message);
		};

		_pusher = _db.CreatePushReplication (new Uri(replicationUrl));
		_pusher.Continuous = true;
		_pusher.Changed += (sender, e) => {
			Log.D (TAG, "Pusher: " + _pusher.LastError == null ? "Okay" : _pusher.LastError.Message);
		};

		_pusher.Start ();
		_puller.Start ();

		Log.D (TAG, "Started replication with " + replicationUrl);
	}
	
	void ProcessRows(QueryEnumerator rows)
	{
		foreach(var row in rows) {
			GameObject obj;
			var foundObj = _spheres.TryGetValue(row.DocumentId, out obj);

			if (row.Document.Deleted) {
				if (foundObj)
				{
					// Remove obj.
					Destroy(obj);
					_spheres.Remove(row.DocumentId);
				}
			} else {
				if (foundObj) {
					// Update obj.
					obj.GetComponent<CouchbaseSphere>().RestoreFromDocument(row.Document);
				} else {
					var sphere = GameObject.CreatePrimitive (PrimitiveType.Sphere);
					sphere.AddComponent<SphereCollider> ();
					sphere.AddComponent<DraggableSphere> ();
					sphere.AddComponent<CouchbaseSphere>().RestoreFromDocument(row.Document);
					_spheres[row.DocumentId] = sphere;
				}
			}
		}
	}

	void Awake()
	{
		_counterText = GameObject.FindWithTag("Counter").GetComponent<Text>();
		if (_counterText == null) {
			Log.E (TAG, "countertext is null");
		} 
	}

	void Update() 
	{
		if (Input.GetKeyDown (KeyCode.N)) {
			CreateObject();
		}

		if (_counterText == null) {
			Log.E (TAG, "countertext is null");
		} else {
			_counterText.text = _counter.ToString ();
		}
	}

	void OnApplicationQuit()
	{
		_puller.Stop ();
		_pusher.Stop ();
		_query.Stop ();
		_manager.Close ();
	}

	public void CreateObject()
	{
		var sphere = GameObject.CreatePrimitive (PrimitiveType.Sphere);
		sphere.AddComponent<SphereCollider> ();
		sphere.AddComponent<DraggableSphere> ();
		sphere.AddComponent<CouchbaseSphere>().Database = _db;
		sphere.transform.position = Camera.main.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 5.0f));

		sphere.GetComponent<CouchbaseSphere> ().SaveToDocument ();
	}
}
