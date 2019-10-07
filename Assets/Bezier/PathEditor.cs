using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathEditor : MonoBehaviour
{
    [HideInInspector]
    public Path path;
    public MeshFilter start, end;
    
    void Awake()
    {
        Vector3[] vertices = start.mesh.vertices;

        Vector3 v0 = start.transform.TransformPoint(vertices[1]) - start.transform.TransformPoint(vertices[0]);
        Vector3 v1 = start.transform.TransformPoint(vertices[2]) - start.transform.TransformPoint(vertices[0]);
        Vector3 normal = Vector3.Cross(v0, v1).normalized;

        float dist = (start.transform.position - end.transform.position).magnitude;
        Vector3 p0 = start.transform.position + vertices[0];
        Vector3 p1 = start.transform.position + vertices[0] + normal * dist * 0.1f;

        path = new Path(p0, p1);

        vertices = end.mesh.vertices;
        v0 = end.transform.TransformPoint(vertices[1]) - end.transform.TransformPoint(vertices[0]);
        v1 = end.transform.TransformPoint(vertices[2]) - end.transform.TransformPoint(vertices[0]);
        normal = Vector3.Cross(v0, v1).normalized;

        path.AddSegment(end.transform.position + vertices[0] + normal * dist * 0.1f);
        path.AddSegment(end.transform.position + vertices[0]);

        path.MovePoint(4, Vector3.LerpUnclamped(path[0], path[3], 3.5f));
        path.MovePoint(7, Vector3.Lerp(path[6], path[9], 0.5f));
        path.MovePoint(8, Vector3.Lerp(path[9], path[6], 0.2f));
    }

    private void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            for (int p = 0; p < path.NumPoints; ++p)
            {
                Gizmos.DrawSphere(path[p], 1f);
            }

            Gizmos.color = Color.blue;
            for (int segment = 0; segment < path.NumSegments; ++segment)
            {
                for (int i = 0; i < 100; ++i)
                {
                    Gizmos.DrawLine(path.GetP(segment, (float)i / 100f), path.GetP(segment, (float)(i + 1) / 100f));
                }
            }

            Gizmos.color = Color.black;
            Gizmos.DrawLine(path[0], path[1]);
            Gizmos.DrawLine(path[path.NumPoints - 2], path[path.NumPoints - 1]);
            for (int i = 3; i < path.NumPoints - 1; i += 3)
            {
                Gizmos.DrawLine(path[i - 1], path[i + 1]);
            }
        }
    }
}
