using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[RequireComponent(typeof(PathEditor))]
[RequireComponent(typeof(MeshFilter))]
public class ProceduralTunnel : MonoBehaviour
{
    Material mat;
    public List<float> radii;
    public int Cres;
    public float Bres;

    Path path;
    List<List<Vector3>> circles;
    bool showGrid = false;

    public MeshFilter start;

    void Start()
    {
        mat = GetComponent<MeshRenderer>().material;
        GetComponent<MeshRenderer>().enabled = false;

        radii = GetComponent<PathEditor>().radii;
        path = GetComponent<PathEditor>().path;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            MeshRenderer r = GetComponent<MeshRenderer>();
            r.enabled = !r.enabled;
            CreateTunnel();
        }

        if (Input.GetKeyDown(KeyCode.N))
        {
            showGrid = !showGrid;
        }
    }

    void CreateTunnel()
    {
        //(Re)initiate the circles list
        circles = new List<List<Vector3>>();
        for (int i = 0; i < path.NumSegments; ++i)
            circles.Add(new List<Vector3>());

        //Create circles
        for (int i = 0; i < path.NumSegments; ++i)
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

    float[][] LerpRadii(float originalRadius, float[] radiiDiff, bool increase, int steps)
    {
        float[][] res = new float[steps + 1][];
        if (increase)
        {
            for (int i = 0; i <= steps; ++i)
            {
                res[i] = new float[radiiDiff.Length];
                for (int j = 0; j < radiiDiff.Length; ++j)
                {
                    res[i][j] = originalRadius + radiiDiff[j] * (float)i / (float)steps;
                }
            }
        }
        else
        {
            for (int i = 0; i <= steps; ++i)
            {
                res[steps - i] = new float[radiiDiff.Length];
                for (int j = 0; j < radiiDiff.Length; ++j)
                {
                    res[steps - i][j] = originalRadius + radiiDiff[j] * (float)i / (float)steps;
                }
            }
        }
        
        return res;
    }

    void createCirclesInSegment(int segment)
    {
        Vector3[] origins = path.GetConsistentNormals(start.transform.TransformPoint(start.mesh.vertices[1]), segment, Bres);
        int res = (int)(path.SegmentLength(segment) / Bres);
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
            radDiffs = LerpRadii(radii[0], radDiff, false, res);

            //Calculate angles
            float[] angles = new float[startV.Count];
            Vector3 axis = Vector3.Cross(startV[1], startV[0]);
            for (int i = 0; i < angles.Length; ++i)
            {
                angles[i] = Vector3.SignedAngle(startV[i], startV[0], axis);
            }

            Path normalPath = new Path(start.transform.TransformPoint(startV[0]), path);
            //Create the circles
            for (int c = 0; c <= res; ++c)
            {
                float t = (float)c / (float)res;
                Vector3 center = path.GetP(segment, t);
                axis = path.GetT(segment, t);
                for (int v = 0; v < startV.Count; ++v)
                {
                    circles[segment].Add(center + Quaternion.AngleAxis(angles[v], axis) * origins[c] * radDiffs[c][v]);
                }
            }
        }
        else
        {
            float radius = radii[segment - 1];
            float radiusEnd = radii[segment];
            float radiusStep = (radiusEnd - radius) / (float)res;

            //Create circles
            for (int i = 0; i <= res; ++i)
            {
                float t = (float)i / (float)res;
                Vector3 center = path.GetP(segment, t);
                Vector3 axis = path.GetT(segment, t);
                circles[segment].AddRange(createCircle(center, axis, origins[i], radius));
                radius += radiusStep;
            }
        }
    }
    
    void UpdateMesh()
    {
        //Generating array of triangles
        List<int> triangles = new List<int>();
        int idx = 0;
        for (int seg = 0; seg < circles.Count; ++seg)
        {
            //Define boundaries of each segment
            int Outer;
            int Inner;
            
            //Start
            if (seg == 0)
            {
                Outer = (int)(path.SegmentLength(seg) / Bres);
                Inner = start.mesh.vertexCount - 1;
            }
            //The rest
            else
            {
                Outer = (int)(path.SegmentLength(seg) / Bres);
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
    
    void OnDrawGizmos()
    {
        if (Application.isPlaying && GetComponent<MeshRenderer>().enabled && showGrid)
        {
            Gizmos.color = Color.black;
            for (int seg = 0; seg < path.NumSegments; ++seg)
            {
                int res;
                if (seg == 0)
                    res = start.mesh.vertexCount - 1;
                else
                    res = Cres;
                for (int i = 0; i < (int)(path.SegmentLength(seg) / Bres); ++i)
                {
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
        }
    }
}
