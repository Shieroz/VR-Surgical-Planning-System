using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathEditor : MonoBehaviour
{
    [HideInInspector]
    public Path path;
    public MeshFilter start;
    public Transform end;
    public List<float> radii;

    //A reference to the location of the hand in VR
    public Transform VRHand;

    private List<GameObject> points; //List of control points
    private List<GameObject> radiusControlPoints;
    private GameObject hoverLocation;
    private LineRenderer line;
    private int accuracy = 10;

    void Awake()
    {
        Vector3[] vertices = start.mesh.vertices;

        Vector3 v0 = start.transform.TransformPoint(vertices[1]) - start.transform.TransformPoint(vertices[0]);
        Vector3 v1 = start.transform.TransformPoint(vertices[2]) - start.transform.TransformPoint(vertices[0]);
        Vector3 normal = Vector3.Cross(v0, v1).normalized;

        float dist = (start.transform.position - end.position).magnitude * 0.1f;
        Vector3 p0 = start.transform.position + vertices[0];
        Vector3 p1 = start.transform.position + vertices[0] + normal * dist;

        path = new Path(p0, p1);

        path.AddSegment(end.position + end.up * dist);
        path.AddSegment(end.position);

        path.MovePoint(4, Vector3.LerpUnclamped(path[0], path[3], 3.5f));
        path.MovePoint(7, Vector3.Lerp(path[6], path[9], 0.5f));
        path.MovePoint(8, Vector3.Lerp(path[9], path[6], 0.2f));
    }

    private void Start()
    {
        //Initiate the list of control points
        points = new List<GameObject>();
        for (int i = 0; i < path.NumPoints; ++i)
        {
            GameObject point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            //We don't want to be able to move the first and last point
            if (i != 0 && i != path.NumPoints - 1)
            {
                point.AddComponent<DragObject>();
            }
            point.transform.position = path.GetControlPoint(i);
            points.Add(point);
        }
        
        //Initiate the list of radius control points and radii list
        radii = new List<float>();
        radii.Add(AverageRadius(start.mesh.vertices));
        radiusControlPoints = new List<GameObject>();
        for (int seg = 1; seg <= path.NumSegments; ++seg)
        {
            GameObject p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            radii.Add(radii[0]);
            p.AddComponent<DragObjectInLine>().origin = points[seg * 3].transform;
            p.GetComponent<DragObjectInLine>().distance = radii[0];
            p.GetComponent<DragObjectInLine>().direction = path.GetN(seg - 1, 1f, Vector3.up);
            radiusControlPoints.Add(p);
        }
        radii.RemoveAt(0);

        //Initiate hover location
        hoverLocation = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hoverLocation.GetComponent<MeshRenderer>().material.color = new Color(0f, 0f, 0f);
        hoverLocation.SetActive(false);

        //Inititate line renderer for the curve
        line = GetComponent<LineRenderer>();
    }

    private void Update()
    {
        //Cast a ray from screen to see where the mouse click on
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            //Move points on the curve
            if (Input.GetMouseButton(0))
            {
                //Go through the list of the Bezier curve control points to see which one it hits
                for (int i = 1; i < path.NumPoints - 1; ++i)
                {
                    if (hit.transform == points[i].transform)
                    {
                        MovePoint(i);
                    }
                }
            }

            //Delete anchor points using a right click (excluding the first and last point)
            if (Input.GetMouseButtonDown(1))
            {
                for (int i = 1; i < path.NumPoints - 1; ++i)
                {
                    if (hit.transform == points[i].transform)
                    {
                        DeleteAnchorPoint(i);
                    }
                }
            }

            if (Input.GetMouseButton(0))
            {
                for (int i = 0; i < radiusControlPoints.Count; ++i)
                {
                    if (hit.transform == radiusControlPoints[i].transform)
                    {
                        radii[i] = radiusControlPoints[i].GetComponent<DragObjectInLine>().distance;
                    }
                }
            }
        }

        //Check if the hand is close enough to be able to add a segment
        int seg = closestSegment(VRHand.position);
        if (seg >= 0)
        {
            //Display a ball to signify where the new anchor point will be added
            hoverLocation.SetActive(true);
            hoverLocation.transform.position = GetHoverLocation(VRHand.position, seg);
            //Add the new anchor point
            if (Input.GetKeyDown(KeyCode.Space))
            {
                SplitBezier(hoverLocation.transform.position, seg);
            }
        }
        else
        {
            hoverLocation.SetActive(false);
        }

        //Draw the Bezier curve
        DrawBezier();
    }

    float AverageRadius(Vector3[] vertices)
    {
        float avg = 0;
        foreach (Vector3 v in vertices)
        {
            avg += v.magnitude;
        }
        return avg / vertices.Length;
    }

    void DrawBezier()
    {
        line.startWidth = (start.transform.position - end.position).magnitude * 0.003f;
        line.endWidth = (start.transform.position - end.position).magnitude * 0.003f;
        List<Vector3> p = new List<Vector3>();
        for (int segment = 0; segment < path.NumSegments; ++segment)
        {
            for (int i = 0; i <= 50; ++i)
            {
                p.Add(path.GetP(segment, (float)i / 50f));
            }
        }
        line.positionCount = p.Count;
        line.SetPositions(p.ToArray());
    }

    int closestSegment(Vector3 origin)
    {
        //Split each segment into smaller sections (maybe 10?), find the minimum of each and compare them to find the segment with the minimum distance
        float minDist = Mathf.Infinity;
        int minSegment = 0;
        for (int seg = 0; seg < path.NumSegments; ++seg)
        {
            for (int t = 0; t <= accuracy; ++t)
            {
                float dist = (path.GetP(seg, (float)t / (float)accuracy) - origin).magnitude;
                if (dist < minDist)
                {
                    minDist = dist;
                    minSegment = seg;
                }
            }
        }

        if (minDist > (start.transform.position - end.position).magnitude * 0.1f)
        {
            return -1; //invalid segment for error handling
        }
        return minSegment;
    }
    
    Vector3 GetHoverLocation(Vector3 origin, int segment)
    {
        //Split the minimum segment into smaller sections, find the t range a <= t <= b such that a and b are the closest to origin.
        float minT = 0f;
        float minDist = Mathf.Infinity;
        float dist;
        int acc = (int)(path.SegmentLength(segment) / 0.01f);
        // we don't want the new point to be the same as the old anchors so I left out the first and last point
        for (int t = 1; t < acc; ++t)
        {
            dist = (path.GetP(segment, (float)t / (float)acc) - origin).magnitude;
            if (dist < minDist)
            {
                minDist = dist;
                minT = (float)t / (float)acc;
            }
        }
        return path.GetP(segment, minT);
    }


    void SplitBezier(Vector3 anchorPos, int segment)
    {
        path.SplitSegment(anchorPos, segment);
        GameObject[] newSegment = new GameObject[3];
        for (int i = 0; i < 3; ++i)
        {
            newSegment[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            newSegment[i].AddComponent<DragObject>();
            newSegment[i].transform.position = path.GetControlPoint(segment * 3 + 2 + i);
        }
        points.InsertRange(segment * 3 + 2, newSegment);

        radii.Insert(segment, radii[segment]);
        GameObject p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        p.AddComponent<DragObjectInLine>().origin = points[(segment + 1) * 3].transform;
        p.GetComponent<DragObjectInLine>().distance = radii[segment];
        p.GetComponent<DragObjectInLine>().direction = path.GetN(segment - 1, 1f, Vector3.up);
        radiusControlPoints.Insert(segment, p);
    }

    void DeleteAnchorPoint(int anchorIndex)
    {
        if (anchorIndex % 3 == 0)
        {
            GameObject p1 = points[anchorIndex - 1];
            GameObject p2 = points[anchorIndex];
            GameObject p3 = points[anchorIndex + 1];
            points.RemoveRange(anchorIndex - 1, 3);
            Destroy(p1);
            Destroy(p2);
            Destroy(p3);
            path.DeleteSegment(anchorIndex);

            p1 = radiusControlPoints[anchorIndex / 3 - 1];
            Destroy(p1);
            radiusControlPoints.RemoveAt(anchorIndex / 3 - 1);
        }
    }

    void MovePoint(int point)
    {
        path.MovePoint(point, points[point].transform.position);
        points[point].transform.position = path.GetControlPoint(point);
        //After moving the desired control point, update other points according to the path
        if (point % 3 == 0)
        {
            points[point - 1].transform.position = path.GetControlPoint(point - 1);
            points[point + 1].transform.position = path.GetControlPoint(point + 1);
            radiusControlPoints[point / 3 - 1].GetComponent<DragObjectInLine>().resetPosition();
        }
        else if (point > 1 && point % 3 == 1)
        {
            points[point - 2].transform.position = path.GetControlPoint(point - 2);
        }
        else if (point < path.NumPoints - 2)
        {
            points[point + 2].transform.position = path.GetControlPoint(point + 2);
        }
    }

    private void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            Gizmos.color = Color.black;
            Gizmos.DrawLine(path[0], path[1]);
            Gizmos.DrawLine(path[path.NumPoints - 2], path[path.NumPoints - 1]);
            for (int i = 3; i < path.NumPoints - 1; i += 3)
            {
                Gizmos.DrawLine(path[i - 1], path[i + 1]);
                Gizmos.DrawLine(path[i], radiusControlPoints[i / 3 - 1].transform.position);
            }
            Gizmos.DrawLine(path[path.NumPoints - 1], radiusControlPoints[radiusControlPoints.Count - 1].transform.position);
        }
    }
}
