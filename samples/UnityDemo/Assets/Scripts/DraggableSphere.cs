using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SphereCollider))]
public class DraggableSphere : MonoBehaviour {

	private Vector3 _screenPoint;
	private Vector3 _offset;

	void OnMouseDown() {
		_screenPoint = Camera.main.WorldToScreenPoint (gameObject.transform.position);
		_offset = gameObject.transform.position - Camera.main.ScreenToWorldPoint(
			new Vector3(Input.mousePosition.x, Input.mousePosition.y, _screenPoint.z));
	}

	void OnMouseDrag() {
		var curScreenPoint = new Vector3(Input.mousePosition.x, Input.mousePosition.y, _screenPoint.z);
		Vector3 curPosition = Camera.main.ScreenToWorldPoint (curScreenPoint) + _offset;
		transform.position = curPosition;
	}
}
