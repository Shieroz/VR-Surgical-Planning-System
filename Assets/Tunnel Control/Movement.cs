using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Movement : MonoBehaviour
{
    public float horizontalSpeed = 10f;
    public float verticalSpeed = 10f;
    public float turnRate = 1f;
    public float tilt = 10f;
    // Update is called once per frame
    void Update()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        transform.Translate((horizontal * horizontalSpeed * Vector3.right + vertical * verticalSpeed * Vector3.forward) * Time.deltaTime);
        if (Input.GetKey(KeyCode.Mouse1))
        {
            float mouseX = Input.GetAxis("Mouse X") * turnRate;
            float mouseY = -Input.GetAxis("Mouse Y") * turnRate;
            transform.Rotate(new Vector3(mouseY, mouseX, 0f));
        }
        if (Input.GetKey(KeyCode.Q))
        {
            transform.Rotate(new Vector3(0f, 0f, tilt) * Time.deltaTime);
        } else if (Input.GetKey(KeyCode.E))
        {
            transform.Rotate(new Vector3(0f, 0f, -tilt) * Time.deltaTime);
        }
    }
}
