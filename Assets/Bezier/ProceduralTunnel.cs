using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[RequireComponent(typeof(PathEditor))]
[RequireComponent(typeof(MeshFilter))]
public class ProceduralTunnel : MonoBehaviour
{
    public Material mat;
    public List<float> radii;
    public int Bres, Cres, Eres;

    Path path;
    List<List<Vector3>> circles;

    public MeshFilter start, end;

    void Start()
    {
        GetComponent<MeshRenderer>().material = mat;

        radii = new List<float>();
        path = GetComponent<PathEditor>().path;

        CreateTunnel();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            CreateTunnel();
        }
    }

    void CreateTunnel()
    {
        //(Re)initiate the circles list
        circles = new List<List<Vector3>>();
        for (int i = 0; i < path.NumSegments; ++i)
            circles.Add(new List<Vector3>());
        
        //(Re)initiate radii list
        radii = new List<float>();
        radii.Add(AverageRadius(start.mesh.vertices));
        radii.Add(radii[0]);
        radii.Add(AverageRadius(end.mesh.vertices));

        //Create circles
        createCirclesInSegment(0);
        createCirclesInSegment(path.NumSegments - 1);
        for (int i = 1; i < path.NumSegments - 1; ++i)
        {
            createCirclesInSegment(i);
        }

        //Update tunnel mesh
        UpdateMesh();
    }

    Vector3[] createCircle(Vector3 root, Vector3 normal, Vector3 origin, float radius)
    {
        Vector3[] circle = new Vector3[Cres];
        float angle = 360f / (float)Cres;
        for (int i = 0; i < Cres; ++i)
        {
            circle[i] = root + Quaternion.AngleAxis(i * angle, normal) * origin * radius;
        }
        return circle;
    }

    float[][] LerpRadii(float originalRadius, float[] radiiDiff, bool increase)
    {
        float[][] res = new float[Eres + 1][];
        if (increase)
        {
            for (int i = 0; i < Eres + 1; ++i)
            {
                res[i] = new float[radiiDiff.Length];
                for (int j = 0; j < radiiDiff.Length; ++j)
                {
                    res[i][j] = originalRadius + radiiDiff[j] * (float)i / (float)Eres;
                }
            }
        }
        else
        {
            for (int i = 0; i < Eres + 1; ++i)
            {
                res[Eres - i] = new float[radiiDiff.Length];
                for (int j = 0; j < radiiDiff.Length; ++j)
                {
                    res[Eres - i][j] = originalRadius + radiiDiff[j] * (float)i / (float)Eres;
                }
            }
        }
        
        return res;
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

    void createCirclesInSegment(int segment)
    {
        if (segment == 0)
        {
            List<Vector3> startV = new List<Vector3>();
            startV.AddRange(start.mesh.vertices);
            startV.RemoveAt(0);

            //Lerp radii of the first section
            float[] radDiff = new float[startV.Count];
            for (int i = 0; i < radDiff.Length; ++i)
            {
                radDiff[i] = startV[i].magnitude - radii[0];
            }
            float[][] radDiffs;
            radDiffs = LerpRadii(radii[0], radDiff, false);

            //Calculate angles
            float[] angles = new float[startV.Count];
            Vector3 axis = Vector3.Cross(startV[1], startV[0]);
            for (int i = 0; i < angles.Length; ++i)
            {
                angles[i] = Vector3.SignedAngle(startV[i], startV[0], axis);
            }

            //Create a clone Bezier curve to calculate origin of circles
            Path normalBezier = new Path(start.transform.TransformPoint(startV[0]), path);

            //Create the circles
            for (int c = 0; c < Eres; ++c)
            {
                float t = (float)c / Eres;
                Vector3 center = path.GetP(segment, t);
                Vector3 origin = (normalBezier.GetP(segment, t) - center).normalized;
                axis = path.GetT(segment, t);
                for (int v = 0; v < startV.Count; ++v)
                {
                    circles[segment].Add(center + Quaternion.AngleAxis(angles[v], axis) * origin * radDiffs[c][v]);
                }
            }
        }
        else if (segment == path.NumSegments - 1)
        {
            int last = radii.Count - 1;
            
            List<Vector3> endV = new List<Vector3>();
            endV.AddRange(end.mesh.vertices);
            endV.RemoveAt(0);

            //Lerp radii of the first section
            float[] radDiff = new float[endV.Count];
            for (int i = 0; i < radDiff.Length; ++i)
            {
                radDiff[i] = endV[i].magnitude - radii[last];
            }
            float[][] radDiffs;
            radDiffs = LerpRadii(radii[last], radDiff, true);

            //Calculate angles
            float[] angles = new float[endV.Count];
            Vector3 axis = Vector3.Cross(endV[0], endV[1]);
            for (int i = 0; i < angles.Length; ++i)
            {
                angles[i] = Vector3.SignedAngle(endV[i], endV[0], axis);
            }

            //Create a clone Bezier curve to calculate origin of circles
            Path normalBezier = new Path(path, end.transform.TransformPoint(endV[0]));

            //Create the circles
            for (int c = 0; c <= Eres; ++c)
            {
                float t = (float)c / Eres;
                Vector3 center = path.GetP(segment, t);
                Vector3 origin = (normalBezier.GetP(segment, t) - center).normalized;
                axis = path.GetT(segment, t);
                //Since the end mesh is connected on the other end of the tunnel, the vertices needs to be reversed
                List<Vector3> temp = new List<Vector3>();
                for (int v = 0; v < endV.Count; ++v)
                {
                    temp.Add(center + Quaternion.AngleAxis(angles[v], axis) * origin * radDiffs[c][v]);
                }
                temp.Reverse();
                circles[segment].AddRange(temp);
            }
        }
        else
        {
            float radius = radii[segment];
            float radiusEnd = radii[segment + 1];
            float radiusStep = (radiusEnd - radius) / (float)Bres;

            Path normalBezier = new Path(start.transform.TransformPoint(start.mesh.vertices[1]), path);

            //Create circles
            for (int i = 0; i <= Bres; ++i)
            {
                float t = (float)i / Bres;
                Vector3 center = path.GetP(segment, t);
                Vector3 origin = (normalBezier.GetP(segment, t) - center).normalized;
                Vector3 axis = path.GetT(segment, t);
                circles[segment].AddRange(createCircle(center, axis, origin, radius));
                radius += radiusStep;
            }
        }
    }
    
    void UpdateMesh()
    {
        //Generating array of triangles
        List<int> triangles = new List<int>();
        int idx = 0;
        for (int seg = 0; seg < path.NumSegments; ++seg)
        {
            //Define boundaries of each segment
            int Outer;
            int Inner;
            
            //Start
            if (seg == 0)
            {
                Outer = Eres - 1;
                Inner = start.mesh.vertexCount - 1;
            }
            //End
            else if (seg == path.NumSegments - 1)
            {
                Outer = Eres;
                Inner = end.mesh.vertexCount - 1;
            }
            //Middle
            else
            {
                Outer = Bres;
                Inner = Cres;
            }

            //Creating triangles
            for (int i = 0; i < Outer; ++i)
            {
                int p0lower = idx + Inner * i;
                int p0higher = idx + Inner * (i + 1);
                for (int j = 0; j < Inner - 1; ++j)
                {
                    triangles.Add(p0lower + j);
                    triangles.Add(p0lower + j + 1);
                    triangles.Add(p0higher + j);
                    triangles.Add(p0lower + j + 1);
                    triangles.Add(p0higher + j + 1);
                    triangles.Add(p0higher + j);
                }
                triangles.Add(p0lower + Inner - 1);
                triangles.Add(p0lower);
                triangles.Add(p0higher + Inner - 1);
                triangles.Add(p0lower);
                triangles.Add(p0higher);
                triangles.Add(p0higher + Inner - 1);
            }
            idx += circles[seg].Count;
        }

        //Create the return mesh
        Mesh mesh = new Mesh();
        mesh.Clear();

        //Generating array of vertices and triangles\
        List<Vector3> vertices = new List<Vector3>();
        foreach (List<Vector3> list in circles)
        {
            vertices.AddRange(list);
        }
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();

        //Recalculate normals
        mesh.RecalculateNormals();

        //Draw mesh
        GetComponent<MeshFilter>().mesh = mesh;
    }
    /*
    void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            Gizmos.color = Color.black;
            for (int seg = 0; seg < path.NumSegments; ++seg)
            {
                if (seg == 0 || seg == path.NumSegments - 1)
                {
                    //Triangles of circles at the start/end of the tunnel
                    int res;
                    if (seg == 0)
                        res = start.mesh.vertexCount - 1;
                    else
                        res = end.mesh.vertexCount - 1;
                    for (int i = 0; i < Eres; ++i)
                    {

                        if (seg == 0 && i == Eres - 1)
                            break;
                        int p0lower = res * i;
                        int p0higher = res * (i + 1);
                        for (int j = 0; j < res - 1; ++j)
                        {
                            Gizmos.DrawLine(circles[seg][p0lower + j], circles[seg][p0lower + j + 1]);
                            Gizmos.DrawLine(circles[seg][p0lower + j + 1], circles[seg][p0higher + j]);
                            Gizmos.DrawLine(circles[seg][p0higher + j], circles[seg][p0lower + j]);
                            Gizmos.DrawLine(circles[seg][p0lower + j + 1], circles[seg][p0higher + j + 1]);
                            Gizmos.DrawLine(circles[seg][p0higher + j + 1], circles[seg][p0higher + j]);
                        }
                        Gizmos.DrawLine(circles[seg][p0lower + res - 1], circles[seg][p0lower]);
                        Gizmos.DrawLine(circles[seg][p0lower], circles[seg][p0higher + res - 1]);
                        Gizmos.DrawLine(circles[seg][p0higher + res - 1], circles[seg][p0lower + res - 1]);
                        Gizmos.DrawLine(circles[seg][p0lower], circles[seg][p0higher]);
                        Gizmos.DrawLine(circles[seg][p0higher], circles[seg][p0higher + res - 1]);
                    }
                }
                else
                {
                    //Triangles of circles at the middle of the tunnel
                    for (int i = 0; i < Bres; ++i)
                    {
                        int p0lower = Cres * i;
                        int p0higher = Cres * (i + 1);
                        for (int j = 0; j < Cres - 1; ++j)
                        {
                            Gizmos.DrawLine(circles[seg][p0lower + j], circles[seg][p0lower + j + 1]);
                            Gizmos.DrawLine(circles[seg][p0lower + j + 1], circles[seg][p0higher + j]);
                            Gizmos.DrawLine(circles[seg][p0higher + j], circles[seg][p0lower + j]);
                            Gizmos.DrawLine(circles[seg][p0lower + j + 1], circles[seg][p0higher + j + 1]);
                            Gizmos.DrawLine(circles[seg][p0higher + j + 1], circles[seg][p0higher + j]);
                        }
                        Gizmos.DrawLine(circles[seg][p0lower + Cres - 1], circles[seg][p0lower]);
                        Gizmos.DrawLine(circles[seg][p0lower], circles[seg][p0higher + Cres - 1]);
                        Gizmos.DrawLine(circles[seg][p0higher + Cres - 1], circles[seg][p0lower + Cres - 1]);
                        Gizmos.DrawLine(circles[seg][p0lower], circles[seg][p0higher]);
                        Gizmos.DrawLine(circles[seg][p0higher], circles[seg][p0higher + Cres - 1]);
                    }
                }
            }
        }
        
    }
    */
}
