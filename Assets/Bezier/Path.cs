using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Path
{
    [SerializeField, HideInInspector]
    List<Vector3> points;

    // This path implementation is based on multiple cubic Bezier curves smoothly connect to each other.
    // The points are stored in a List with anchor points in index of 0,4,7,.. and between them are control points.
    // The control points for a particular anchor is 1 before and 1 after it in the list.

    //The constructor creates a starting anchor point and a single control point for that anchor
    public Path(Vector3 start, Vector3 end)
    {
        points = new List<Vector3>
        {
            start,
            Vector3.Lerp(start, end, 0.2f),
            Vector3.Lerp(end, start, 0.2f),
            end
        };
    }

    //The copy constructor
    public Path (Vector3 start, Path other)
    {
        points = new List<Vector3>();
        for (int i = 0; i < other.NumPoints; ++i)
        {
            points.Add(other.points[i] - other.points[0] + start);
        }
    }

    //The other copy constructor
    public Path(Path other, Vector3 end)
    {
        points = new List<Vector3>();
        for (int i = 0; i < other.NumPoints; ++i)
        {
            points.Add(other.points[i] - other.points[other.NumPoints - 1] + end);
        }
    }

    public Vector3 this[int i]
    {
        get
        {
            return points[i];
        }
    }

    public int NumPoints
    {
        get
        {
            return points.Count;
        }
    }

    public int NumSegments
    {
        get
        {
            return points.Count / 3;
        }
    }
    public Vector3[] GetPointsInSegment(int i)
    {
        return new Vector3[] { points[i * 3], points[i * 3 + 1], points[i * 3 + 2], points[i * 3 + 3] };
    }

    public void AddSegment(Vector3 anchorPos)
    {
        //Adds a control point that is opposite to the previous control point
        points.Add(points[points.Count - 1] * 2 - points[points.Count - 2]);
        //Adds another control point that is in the middle of the previous control point and anchor point
        points.Add((points[points.Count - 1] + anchorPos) * .5f);
        //Adds anchor point
        points.Add(anchorPos);
    }

    //Returns the length of a segment with a resolution of 200
    public float SegmentLength(int segment)
    {
        float length = 0;
        Vector3 p1 = GetP(segment, 0f);
        for (int i = 1; i <= 200; ++i)
        {
            Vector3 p2 = GetP(segment, (float)i / 200f);
            length += (p2 - p1).magnitude;
            p1 = p2;
        }
        return length;
    }

    //Returns a point at time t in a segment
    public Vector3 GetP(int segment, float t)
    {
        Vector3[] p = GetPointsInSegment(segment);
        float omt = 1f - t;
        float omt2 = omt * omt;
        float t2 = t * t;
        //Optimized math
        return p[0] * (omt2 * omt) +
               p[1] * (3f * omt2 * t) +
               p[2] * (3f * omt * t2) +
               p[3] * (t2 * t);
    }

    //Returns a tangent at time t in a segment
    public Vector3 GetT(int segment, float t)
    {
        Vector3[] pts = GetPointsInSegment(segment);
        float omt = 1f - t;
        float omt2 = omt * omt;
        float t2 = t * t;
        Vector3 tangent = pts[0] * (-omt2) +
                          pts[1] * (3 * omt2 - 2 * omt) +
                          pts[2] * (-3 * t2 + 2 * t) +
                          pts[3] * (t2);
        return tangent.normalized;
    }

    //Returns a normal at time t in a segment
    public Vector3 GetN(int segment, float t, Vector3 up)
    {
        Vector3 tng = GetT(segment, t);
        Vector3 binormal = Vector3.Cross(up, tng);
        return Vector3.Cross(tng, binormal).normalized;
    }

    //Create an array of normals for the whole Bezier Curve that minimize twisting as much as possible by refering to the previous normal
    //This implementation is inspired from the Rotation Minimizing Frame algrorithm paper from Microsoft, albeit simplified
    public Vector3[] GetConsistentNormals(Vector3 initialDirection, int segment, float resolution)
    {
        List<Vector3> normals = new List<Vector3>();
        normals.Add(GetN(0, 0f, initialDirection));
        int stepumber = (int)(SegmentLength(segment) / resolution);
        for (int t = 0; t <= stepumber; ++t)
        {
            normals.Add(GetN(segment, (float)t / stepumber, normals[normals.Count - 1]));
        }
        normals.RemoveAt(0);
        return normals.ToArray();
    }

    //Returns the orientation at time t in a segment
    public Quaternion GetOrientation3D(int segment, float t, Vector3 up)
    {
        Vector3 tng = GetT(segment, t);
        Vector3 nrm = GetN(segment, t, up);
        return Quaternion.LookRotation(tng, nrm);
    }

    public Vector3 GetControlPoint(int i)
    {
        return points[i];
    }

    public void SplitSegment(Vector3 anchorPos, int segmentIndex)
    {
        Vector3[] segP = GetPointsInSegment(segmentIndex);
        Vector3 anchor1 = anchorPos + (segP[1] - anchorPos) * 0.5f;
        Vector3 anchor2 = anchorPos - (segP[1] - anchorPos) * 0.5f;
        points.InsertRange(segmentIndex * 3 + 2, new Vector3[] { anchor1, anchorPos, anchor2 });
    }

    public void DeleteSegment(int anchorIndex)
    {
        if (anchorIndex % 3 == 0)
        {
            points.RemoveRange(anchorIndex - 1, 3);
        }
    }

    public void MovePoint(int i, Vector3 pos)
    {
        Vector3 deltaMove = pos - points[i];
        points[i] = pos;

        if (i % 3 == 0)
        {
            if (i + 1 < points.Count)
            {
                points[i + 1] += deltaMove;
            }
            if (i - 1 >= 0)
            {
                points[i - 1] += deltaMove;
            }
        }
        else
        {
            bool nextPointIsAnchor = (i + 1) % 3 == 0;
            int correspondingControlIndex = (nextPointIsAnchor) ? i + 2 : i - 2;
            int anchorIndex = (nextPointIsAnchor) ? i + 1 : i - 1;

            if (correspondingControlIndex >= 0 && correspondingControlIndex < points.Count)
            {
                float dst = (points[anchorIndex] - points[correspondingControlIndex]).magnitude;
                Vector3 dir = (points[anchorIndex] - pos).normalized;
                points[correspondingControlIndex] = points[anchorIndex] + dir * dst;
            }
        }
    }
}
