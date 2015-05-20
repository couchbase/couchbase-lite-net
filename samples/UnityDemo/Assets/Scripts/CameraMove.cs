using UnityEngine;
using System.Collections;
using UnityStandardAssets.CrossPlatformInput;

public class CameraMove : MonoBehaviour {

	// Update is called once per frame
	void Update () {
		float xAxisValue = CrossPlatformInputManager.GetAxis("Horizontal");
		float zAxisValue = CrossPlatformInputManager.GetAxis("Vertical");
		float turnAxisValue = CrossPlatformInputManager.GetAxis ("Turn");
		if(Camera.current != null)
		{
			Camera.current.transform.Translate(new Vector3(xAxisValue * 0.2f, 0.0f, zAxisValue * 0.2f));
			Camera.current.transform.Rotate(new Vector3(0.0f, turnAxisValue, 0.0f));
		}
	}
}
