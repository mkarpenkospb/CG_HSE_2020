using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class MeshGenerator : MonoBehaviour
{
    public MetaBallField Field = new MetaBallField();

    private MeshFilter _filter;
    private Mesh _mesh;
    private ComputeShader _shaderCubes;
    private int _kernelCubes;
    private ComputeBuffer _vertexBuffer;
    private ComputeBuffer _normalBuffer;
    private ComputeBuffer _indexesBuffer;
    private ComputeBuffer _cubesCenters;
    private ComputeBuffer _caseToEdges;
    // List<Vector3> vertecis = new List<Vector3>();
    // private ComputeBuffer _CaseToEdges = new ComputeBuffer(256, 3 * 3 * sizeof(int));
    
    
    private List<Vector3> vertices = new List<Vector3>();
    private List<Vector3> normals = new List<Vector3>();
    private List<int> indices = new List<int>();
    
    
    
    private void Start()
    {
        _shaderCubes = Resources.Load<ComputeShader>("cubes");
        // if (!_shaderCubes.HasKernel("kernel1")) 
        //     throw  new Exception();
        _kernelCubes = _shaderCubes.FindKernel("kernel1");
    }

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
        float cubeSide = 0.8f;

        int xCount = (int) Math.Ceiling((frontPoints[1] - frontPoints[0]) / cubeSide / 8 ) * 8 ;
        int yCount = (int) Math.Ceiling((frontPoints[3] - frontPoints[2]) / cubeSide / 8) * 8  ;
        int zCount = (int) Math.Ceiling((frontPoints[5] - frontPoints[4]) / cubeSide / 8) * 8 ;
   
        // xCount * yCount * zCount кубов, по 5 треугольников на каждый, по 3 вектора - вершины каждому
        Vector3[] vertex = new Vector3[xCount * yCount * zCount * 5 * 3];
        _vertexBuffer = new ComputeBuffer(xCount * yCount * zCount * 5 * 3, sizeof(float) * 3);
        _vertexBuffer.SetData(vertex);
        _shaderCubes.SetBuffer(_kernelCubes, "vertexes", _vertexBuffer);
        // столько же сколько и вершин
        Vector3[] normalsb = new Vector3[xCount * yCount * zCount * 5 * 3];
        _normalBuffer = new ComputeBuffer(xCount * yCount * zCount * 5 * 3, sizeof(float) * 3);
        _vertexBuffer.SetData(normalsb);
        _shaderCubes.SetBuffer(_kernelCubes, "normals", _normalBuffer);
        // тоже столько же
        int[] ib = new int[xCount * yCount * zCount * 5 * 3];
        _indexesBuffer = new ComputeBuffer(xCount * yCount * zCount * 5 * 3, sizeof(int) );
        _indexesBuffer.SetData(ib);
        _shaderCubes.SetBuffer(_kernelCubes, "indexes", _indexesBuffer);
        
        Vector3[] ballCenters = Field.Balls.Select(ball => ball.position).ToArray(); 
        _cubesCenters = new ComputeBuffer(ballCenters.Length, sizeof(float) * 3);
        _cubesCenters.SetData(ballCenters);
        _shaderCubes.SetBuffer(_kernelCubes, "cubesCenters", _cubesCenters);

        int3[] caseToEdgesData = MarchingCubes.Tables.CaseToEdges.SelectMany(x=>x).ToArray();
        _caseToEdges = new ComputeBuffer(caseToEdgesData.Length, sizeof(int)  * 3 );
        _caseToEdges.SetData(caseToEdgesData);
        _shaderCubes.SetBuffer(_kernelCubes, "CaseToEdges", _caseToEdges);
        
        _shaderCubes.SetFloat("delta", cubeSide);
        _shaderCubes.SetInt("xCount", xCount); _shaderCubes.SetFloat("xStartPoint", frontPoints[0]);
        _shaderCubes.SetInt("yCount", yCount); _shaderCubes.SetFloat("yStartPoint", frontPoints[2]);
        _shaderCubes.SetInt("zCount", zCount); _shaderCubes.SetFloat("zStartPoint", frontPoints[4]);
        _shaderCubes.SetInt("cubesCount", ballCenters.Length);
        
        _shaderCubes.Dispatch(_kernelCubes, xCount , yCount, zCount);
       
        _vertexBuffer.GetData(vertex);
        _indexesBuffer.GetData(ib);
        _normalBuffer.GetData(normalsb);

        // vertecis = vertex.Where(v => v.x != 0 || v.y != 0 || v.z != 0).ToList();


        _mesh.Clear();
        _mesh.SetVertices(vertex);
        _mesh.SetTriangles(ib, 0);
        _mesh.SetNormals(normalsb); // Use _mesh.SetNormals(normals) instead when you calculate them
        // Upload mesh data to the GPU
        _mesh.UploadMeshData(false);
    }

    private void OnDestroy()
    {
        _vertexBuffer.Dispose();
    }

    private float[] CalculateWorkingArea()
    {
        int radiusCoeff = 1;
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