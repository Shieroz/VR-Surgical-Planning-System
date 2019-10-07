using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using UnityEditor;

public class Intesect : MonoBehaviour
{
    public Material meshMat;
    public GameObject meshSpawner;
    public MeshFilter targetMesh;
    public Collider intersectPlane;

    [HideInInspector]
    public Vector3[] vertices;
    [HideInInspector]
    public float[] angles;

    List<Vector3> points;
    Vector3 centroid;
    Vector3 normal;

    Stopwatch stopwatch;

    private void Start()
    {
        stopwatch = new Stopwatch();
        points = new List<Vector3>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            stopwatch.Start();
            calculateIntersections();
            drawMesh();
            stopwatch.Stop();
            UnityEngine.Debug.Log("Done: " + stopwatch.Elapsed);
        }
    }

    void calculateIntersections()
    {
        Mesh m = targetMesh.mesh;
        points = new List<Vector3>();

        for (int i = 0; i < m.triangles.Length; i += 3)
        {
            Vector3 p1 = targetMesh.transform.TransformPoint(m.vertices[m.triangles[i]]);
            Vector3 p2 = targetMesh.transform.TransformPoint(m.vertices[m.triangles[i + 1]]);
            Vector3 p3 = targetMesh.transform.TransformPoint(m.vertices[m.triangles[i + 2]]);

            addIntersect(p1, p2);
            addIntersect(p2, p3);
            addIntersect(p3, p1);
        }
        //Adds the centroid to the beginning
        calculateCentroid();
        points.Insert(0, centroid);
        
        //sort all points 
        sortPerimeter();

        //Center the mesh
        centerMesh();
    }

    void addIntersect(Vector3 start, Vector3 end)
    {
        Vector3 dir = end - start;
        RaycastHit hit;
        if (intersectPlane.Raycast(new Ray(start, dir), out hit, dir.magnitude))
        {
            if (!points.Contains(hit.point))
                points.Add(hit.point);
        }
    }

    // Find the center of the polygon created after finding all the intersection between the mesh and the plane
    void calculateCentroid()
    {
        float x = 0;
        float y = 0;
        float z = 0;
        foreach (Vector3 point in points)
        {
            x += point.x;
            y += point.y;
            z += point.z;
        }
        float length = points.ToArray().Length;
        centroid = new Vector3(x/length, y/length, z/length);
    }

    //Center the vertices of the mesh around the centroid
    void centerMesh()
    {
        Vector3 dir = -centroid;

        for (int i = 0; i < vertices.Length; ++i)
        {
            vertices[i] += dir;
        }
    }

    void sortPerimeter()
    {
        //Initiate the vertices array
        vertices = points.ToArray();

        //Initiate the angles array
        angles = new float[vertices.Length];
        angles[0] = -Mathf.Infinity;
        angles[1] = 0f;

        //Set up the pivot
        Vector3 pivot = vertices[1] - vertices[0];

        //Find the normal of the new mesh, which should be the total opposite of the current intersection plane,
        //which means their dot product should be > 0
        int idx = 2;
        normal = Vector3.Cross(pivot, vertices[idx] - vertices[0]).normalized;
        while (Vector3.Dot(normal, intersectPlane.transform.up) > 0)
        {
            idx += 1;
            normal = Vector3.Cross(pivot, vertices[idx] - vertices[0]).normalized;
        }

        //Fill angles with values
        for (int i = 2; i < angles.Length; ++i)
        {
            angles[i] = Vector3.SignedAngle(pivot, vertices[i] - vertices[0], normal);
        }

        //Sort angles and move vertices in conjunction with it.
        //I used Insertion Sort for simplicity but this might need to change for more detailed mesh
        for (int i = 2; i < angles.Length; ++i)
        {
            float t = angles[i];
            Vector3 v = vertices[i];
            int j = i - 1;
            while (t < angles[j]) {
                angles[j + 1] = angles[j];
                vertices[j + 1] = vertices[j];
                --j;
            }
            angles[j + 1] = t;
            vertices[j + 1] = v;
        }
    }

    void drawMesh()
    {
        meshSpawner.transform.position = centroid;

        //Use the provided material
        meshSpawner.GetComponent<MeshRenderer>().material = meshMat;

        //Reset the mesh filter
        Mesh mesh = meshSpawner.GetComponent<MeshFilter>().mesh;
        mesh.Clear();

        mesh.vertices = vertices;

        //Generate triangles for the mesh
        List<int> triangles = new List<int>();
        for (int i = 2; i < mesh.vertices.Length; i++)
        {
            triangles.Add(0);
            triangles.Add(i - 1);
            triangles.Add(i);
        }

        triangles.Add(0);
        triangles.Add(mesh.vertices.Length - 1);
        triangles.Add(1);

        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();

        AssetDatabase.CreateAsset(mesh, "Assets/Plane Intersect/MeshCut.asset");
        AssetDatabase.SaveAssets();
    }
}
