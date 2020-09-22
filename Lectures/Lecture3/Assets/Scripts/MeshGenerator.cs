using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class MeshGenerator : MonoBehaviour
{
    public MetaBallField Field = new MetaBallField();

    private MeshFilter _filter;
    private Mesh _mesh;

    private List<Vector3> vertices = new List<Vector3>();
    private List<Vector3> normals = new List<Vector3>();
    private List<int> indices = new List<int>();

    /// <summary>
    /// Executed by Unity upon object initialization. <see cref="https://docs.unity3d.com/Manual/ExecutionOrder.html"/>
    /// </summary>
    private void Awake()
    {
        // Getting a component, responsible for storing the mesh
        _filter = GetComponent<MeshFilter>();

        // instantiating the mesh
        _mesh = _filter.mesh = new Mesh();

        // Just a little optimization, telling unity that the mesh is going to be updated frequently
        _mesh.MarkDynamic();
    }

    /// <summary>
    /// Executed by Unity on every frame <see cref="https://docs.unity3d.com/Manual/ExecutionOrder.html"/>
    /// You can use it to animate something in runtime.
    /// </summary>
    private void Update()
    {
        vertices.Clear();
        indices.Clear();
        normals.Clear();
        Field.Update();

        // определим пространство, внутри которого нужно строить кубики. Всего нужно получить 
        // 6 значений, по 2 на каждую ось 
        float[] frontPoints = CalculateWorkingArea();
        
        // разобьем пространство на кубики
        float cubeSide = 0.1f;
        List<List<Vector3>> microCubes = CalculateMicroCubes(cubeSide, frontPoints);

        // начнём заполнять массивы 
        foreach (var cube in microCubes)
        {
            int cubeCase = 0;
            float[] funcValues = new float[8];
            for (int i = 0; i < 8; i++)
            {
                funcValues[i] = Field.F(cube[i]);
                if (funcValues[i] > 0)
                {
                    cubeCase += (int) Math.Pow(2, i);
                }
            }

            int trianglesCount = MarchingCubes.Tables.CaseToTrianglesCount[cubeCase];
            int3[] edges = MarchingCubes.Tables.CaseToEdges[cubeCase];
            for (int i = 0; i < trianglesCount; i++)
            {
                int3 edgeIndex = edges[i];
                int[] edgeVertexes1 = MarchingCubes.Tables._cubeEdges[edgeIndex.x];
                int[] edgeVertexes2 = MarchingCubes.Tables._cubeEdges[edgeIndex.y];
                int[] edgeVertexes3 = MarchingCubes.Tables._cubeEdges[edgeIndex.z];
                Vector3 pt1 = Vector3.Lerp(cube[edgeVertexes1[0]], cube[edgeVertexes1[1]],
                    funcValues[edgeVertexes1[1]] / (funcValues[edgeVertexes1[1]] - funcValues[edgeVertexes1[0]]));
                Vector3 pt2 = Vector3.Lerp(cube[edgeVertexes2[0]], cube[edgeVertexes2[1]],
                    funcValues[edgeVertexes2[1]] / (funcValues[edgeVertexes2[1]] - funcValues[edgeVertexes2[0]]));
                Vector3 pt3 = Vector3.Lerp(cube[edgeVertexes3[0]], cube[edgeVertexes3[1]],
                    funcValues[edgeVertexes3[1]] / (funcValues[edgeVertexes3[1]] - funcValues[edgeVertexes3[0]]));

                indices.Add(vertices.Count);
                vertices.Add(pt1);
                normals.Add(CalculateNormal(pt1));

                indices.Add(vertices.Count);
                vertices.Add(pt2);
                normals.Add(CalculateNormal(pt2));

                indices.Add(vertices.Count);
                vertices.Add(pt3);
                normals.Add(CalculateNormal(pt3));
            }
        }
        
        _mesh.Clear();
        _mesh.SetVertices(vertices);
        _mesh.SetTriangles(indices, 0);
        _mesh.SetNormals(normals); // Use _mesh.SetNormals(normals) instead when you calculate them

        // Upload mesh data to the GPU
        _mesh.UploadMeshData(false);
    }

    private Vector3 CalculateNormal(Vector3 pt)
    {
        float delta = 0.001f;
        Vector3 dx = new Vector3(delta, 0, 0);
        Vector3 dy = new Vector3(0, delta, 0);
        Vector3 dz = new Vector3(0, 0, delta);

        return -Vector3.Normalize(new Vector3(
            Field.F(pt + dx) - Field.F(pt - dx),
            Field.F(pt + dy) - Field.F(pt - dy),
            Field.F(pt + dz) - Field.F(pt - dz)
        ));
    }

    private List<List<Vector3>> CalculateMicroCubes(float cubeSide, float[] frontPoints)
    {
        
        int xCount = (int) Math.Ceiling((frontPoints[1] - frontPoints[0]) / cubeSide);
        int yCount = (int) Math.Ceiling((frontPoints[3] - frontPoints[2]) / cubeSide);
        int zCount = (int) Math.Ceiling((frontPoints[5] - frontPoints[4]) / cubeSide);

        float[] xPoints = new float[xCount + 1];
        xPoints[0] = frontPoints[0];
        float[] yPoints = new float[yCount + 1];
        yPoints[0] = frontPoints[2];
        float[] zPoints = new float[zCount + 1];
        zPoints[0] = frontPoints[4];

        for (int i = 1; i < xCount + 1; ++i)
        {
            xPoints[i] = xPoints[i - 1] + cubeSide;
        }

        for (int i = 1; i < yCount + 1; ++i)
        {
            yPoints[i] = yPoints[i - 1] + cubeSide;
        }

        for (int i = 1; i < zCount + 1; ++i)
        {
            zPoints[i] = zPoints[i - 1] + cubeSide;
        }


        List<List<Vector3>> microCubes = new List<List<Vector3>>();

        for (int z = 0; z < zCount; z++)
        {
            for (int y = 0; y < yCount; y++)
            {
                for (int x = 0; x < xCount; x++)
                {
                    // посмотрим на вершины куба из примера и поставим +1 там, где у него 1
                    microCubes.Add(new List<Vector3>
                    {
                        new Vector3(xPoints[x], yPoints[y], zPoints[z]),
                        new Vector3(xPoints[x], yPoints[y + 1], zPoints[z]),
                        new Vector3(xPoints[x + 1], yPoints[y + 1], zPoints[z]),
                        new Vector3(xPoints[x + 1], yPoints[y], zPoints[z]),
                        new Vector3(xPoints[x], yPoints[y], zPoints[z + 1]),
                        new Vector3(xPoints[x], yPoints[y + 1], zPoints[z + 1]),
                        new Vector3(xPoints[x + 1], yPoints[y + 1], zPoints[z + 1]),
                        new Vector3(xPoints[x + 1], yPoints[y], zPoints[z + 1]),
                    });
                }
            }
        }

        return microCubes;
    }

    private float[] CalculateWorkingArea()
    {
        int radiusCoeff = 5;
        Vector3[] ballsPositions = Field.Balls.Select(ball => ball.position).ToArray();

        float[] frontPoints =
        {
            Int32.MaxValue, Int32.MinValue, // x
            Int32.MaxValue, Int32.MinValue, // y
            Int32.MaxValue, Int32.MinValue, // z
        };

        // frontPoints будет представлять собой некоторый параллелипипед с плоскостями, параллельными
        // координатным осям

        foreach (var pos in ballsPositions)
        {
            frontPoints[0] = Math.Min(frontPoints[0], pos.x - radiusCoeff * Field.BallRadius);
            frontPoints[1] = Math.Max(frontPoints[1], pos.x + radiusCoeff * Field.BallRadius);

            frontPoints[2] = Math.Min(frontPoints[0], pos.y - radiusCoeff * Field.BallRadius);
            frontPoints[3] = Math.Max(frontPoints[0], pos.y + radiusCoeff * Field.BallRadius);

            frontPoints[4] = Math.Min(frontPoints[0], pos.z - radiusCoeff * Field.BallRadius);
            frontPoints[5] = Math.Max(frontPoints[0], pos.z + radiusCoeff * Field.BallRadius);
        }

        return frontPoints;
    }
}