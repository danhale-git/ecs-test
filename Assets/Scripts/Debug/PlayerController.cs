using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour {

	//	First person controls
	public int speed = 25;
	int sensitivity = 1;
	Rigidbody rigidBody;
	Camera mycam;

	//	Chunk checks
	float updateTimer;
	int chunkLayerMask;
	

	void Start ()
	{
		mycam = GetComponentInChildren<Camera>();
		rigidBody = GetComponent<Rigidbody>();

		updateTimer = Time.fixedTime;
		chunkLayerMask = LayerMask.GetMask("Chunk");
	}

	//	The ray we are using for selection
	Ray Ray()
	{
		//return Camera.main.ScreenPointToRay(Input.mousePosition);
		return mycam.ViewportPointToRay(new Vector3(0.5F, 0.5F, 0));
	}

	void CursorOff()
	{
		if(Cursor.lockState == CursorLockMode.None) Cursor.lockState = CursorLockMode.Locked;
	}
	
	void CursorOn()
	{
		if(Cursor.lockState == CursorLockMode.Locked) Cursor.lockState = CursorLockMode.None;
	}

	void Update ()
	{
		if(Input.GetKey(KeyCode.LeftShift))
			Movement(0.25f);
		else
			Movement(1);

		if(Input.GetKeyDown(KeyCode.Escape))
			CursorOn();
	}

	
	//	Simple flying first person movement
	//	Probably temporary
	void Movement(float slow)
	{
		if(Input.GetKey(KeyCode.W))	//	forward
		{
			transform.Translate((Vector3.forward * (speed * slow)) * Time.deltaTime);
			CursorOff();
		}
		if(Input.GetKey(KeyCode.S))	//	back
		{
			transform.Translate((Vector3.back * (speed * slow)) * Time.deltaTime);
			CursorOff();
		}
		if(Input.GetKey(KeyCode.A))	//	left
		{
			transform.Translate((Vector3.left * (speed * slow)) * Time.deltaTime);
			CursorOff();
		}
		if(Input.GetKey(KeyCode.D))	//	right
		{
			transform.Translate((Vector3.right * (speed * slow)) * Time.deltaTime);
			CursorOff();
		}
		if(Input.GetKey(KeyCode.LeftControl))	//	down
		{
			transform.Translate((Vector3.down * (speed * slow)) * Time.deltaTime);
			CursorOff();
		}
		if(Input.GetKey(KeyCode.Space))	//	up
		{
			transform.Translate((Vector3.up * (speed * slow)) * Time.deltaTime);
			CursorOff();
		}

		if(Cursor.lockState != CursorLockMode.Locked) return;
		
		float horizontal = sensitivity * Input.GetAxis("Mouse X");
        float vertical = -(sensitivity * Input.GetAxis("Mouse Y"));
        transform.Rotate(0, horizontal, 0);
		mycam.gameObject.transform.Rotate(vertical, 0, 0);		
	}

}