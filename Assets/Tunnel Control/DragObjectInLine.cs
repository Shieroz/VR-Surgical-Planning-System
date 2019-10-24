using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DragObjectInLine : MonoBehaviour
{
    public Transform origin;
    public float distance;
    public Vector3 direction;

    private Vector3 mOffset;
    private float mZCoord;

    void Start()
    {
        resetPosition();
    }

    public void resetPosition()
    {
        transform.position = origin.position + direction * distance;
    }
    
    void OnMouseDown()
    {
        mZCoord = Camera.main.WorldToScreenPoint(transform.position).z;
        // Store offset = gameobject world pos - mouse world pos
        mOffset = transform.position - GetMouseAsWorldPoint();
    }

    private Vector3 GetMouseAsWorldPoint()
    {
        // Pixel coordinates of mouse (x,y)
        Vector3 mousePoint = Input.mousePosition;

        // z coordinate of game object on screen
        mousePoint.z = mZCoord;

        // Convert it to world points
        return Camera.main.ScreenToWorldPoint(mousePoint);
    }

    void Update()
    {
        //Adjust control point whenever it goes past origin
        if (Vector3.Dot(transform.position - origin.position, direction) <= 0)
            transform.position = origin.position + direction * 0.001f;
    }

    void OnMouseDrag()
    {
        distance = (transform.position - origin.position).magnitude;
        Vector3 newPos = origin.position + Vector3.Project(GetMouseAsWorldPoint() + mOffset - origin.position, direction);
        //Prevent the control point from moving past the origin
        if (Vector3.Dot(transform.position - origin.position, direction) > 0)
            transform.position = newPos;
            
    }
}
