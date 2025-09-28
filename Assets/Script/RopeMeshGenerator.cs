using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 绳子网格生成器
/// 负责根据路径生成动态的绳子网格，支持直线和弧线
/// </summary>
public class RopeMeshGenerator : MonoBehaviour
{
    #region 公共字段
    public List<Vector3> meshVert;
    public List<int> meshTriangles;
    public List<Vector2> meshUV;
    public float Width = 1;
    #endregion
    
    #region 私有字段
    private MeshFilter meshFilter;
    private Mesh generatedMesh;
    private Vector3 currentStartPos = new Vector3(0, 0, 0);
    private float uvOffset = 0;                     // UV偏移量，用于纹理滚动
    private bool needStartPoint = true;             // 是否需要添加起始点
    [SerializeField]
    private int totalVertexCount = 0;               // 顶点总数
    #endregion

    #region Unity生命周期
    void Start()
    {
        InitializeMeshComponents();
    }

    void Update()
    {
    }
    #endregion

    #region 初始化
    /// <summary>
    /// 初始化网格组件和数据结构
    /// </summary>
    private void InitializeMeshComponents()
    {
        meshFilter = GetComponent<MeshFilter>();
        generatedMesh = new Mesh();
        meshFilter.mesh = generatedMesh;
        meshVert = new List<Vector3>{};
        meshTriangles = new List<int>{};
        meshUV = new List<Vector2>{};
    }
    #endregion

    #region 公共方法
    /// <summary>
    /// 应用网格数据到网格对象
    /// </summary>
    public void stroke()
    {
        generatedMesh.name = "RopeMesh";
        generatedMesh.Clear();
        
        // 将List数据转换为数组并赋值给网格
        generatedMesh.vertices = meshVert.ToArray();
        generatedMesh.triangles = meshTriangles.ToArray();
        generatedMesh.uv = meshUV.ToArray();
    }

    /// <summary>
    /// 绘制直线到指定位置
    /// </summary>
    public void lineTo(Vector3 endPos)
    {
        // 步骤1：计算线段基本信息
        Vector3 directionVector = endPos - currentStartPos;
        float segmentLength = Vector3.Distance(currentStartPos, endPos);
        
        // 步骤2：计算垂直方向向量（用于确定线段宽度）
        Vector3 leftOffset, rightOffset;
        CalculatePerpendicularVectors(directionVector, out leftOffset, out rightOffset);
        
        // 步骤3：添加起始点顶点
        AddLineStartVertices(leftOffset, rightOffset);
        
        // 步骤4：添加终点顶点
        AddLineEndVertices(endPos, leftOffset, rightOffset, segmentLength);
        
        // 步骤5：生成连接三角形
        AddLineTriangles();
        
        // 步骤6：更新状态
        currentStartPos = endPos;
        needStartPoint = false;
    }

    /// <summary>
    /// 添加弧线路径
    /// </summary>
    public void circle(Vector3 circleCenter, float radius, float startAngle, float endAngle, bool clockwise = false)
    {
        // 步骤1：计算弧线长度
        float arcLength = CalculateArcLength(startAngle, endAngle, radius, clockwise);
        float initialOffset = uvOffset;
        
        // 步骤2：添加弧线起始点
        AddArcStartPoint(circleCenter, radius, startAngle, clockwise);
        
        // 步骤3：生成弧线中间段
        GenerateArcSegments(circleCenter, radius, startAngle, endAngle, arcLength, clockwise);
        
        // 步骤4：确保弧线终点精确
        EnsureArcEndPoint(circleCenter, radius, endAngle, arcLength, initialOffset, clockwise);
        
        // 步骤5：更新偏移量和状态
        uvOffset = (uvOffset * 5 % 5) / 5.0f;
        needStartPoint = true;
    }

    /// <summary>
    /// 移动到指定位置
    /// </summary>
    public void moveTo(Vector2 newPosition)
    {
        currentStartPos = newPosition;
        needStartPoint = true;
    }

    /// <summary>
    /// 清空所有数据
    /// </summary>
    public void clear()
    {
        meshVert.Clear();
        meshTriangles.Clear();
        meshUV.Clear();
        needStartPoint = true;
        currentStartPos = new Vector3(0, 0, 0);
        totalVertexCount = 0;
        uvOffset = 0;
    }
    #endregion

    #region 直线绘制方法
    /// <summary>
    /// 计算垂直方向向量
    /// 根据线段方向计算左右两个垂直向量，用于确定线段的宽度边界
    /// </summary>
    private void CalculatePerpendicularVectors(Vector3 direction, out Vector3 leftVector, out Vector3 rightVector)
    {
        // 通过交换x,y分量并取负号来获得垂直向量
        leftVector = new Vector3(-direction.y, direction.x);      // 左侧向量（逆时针90度旋转）
        rightVector = new Vector3(direction.y, -direction.x);     // 右侧向量（顺时针90度旋转）
        
        // 标准化并按半宽度缩放
        leftVector = leftVector.normalized * Width / 2;
        rightVector = rightVector.normalized * Width / 2;
    }

    /// <summary>
    /// 添加线段起始点顶点
    /// </summary>
    private void AddLineStartVertices(Vector3 leftOffset, Vector3 rightOffset)
    {
        // 初始添加4个点，后可只添加两个点
        if (true)
        {
            meshVert.Add(currentStartPos + rightOffset);
            meshVert.Add(currentStartPos + leftOffset);
            totalVertexCount += 2;
            
            // 添加UV坐标，使用当前偏移量
            meshUV.Add(new Vector2(uvOffset, 0));
            meshUV.Add(new Vector2(uvOffset, 1));
        }
    }

    /// <summary>
    /// 添加线段终点顶点
    /// </summary>
    private void AddLineEndVertices(Vector3 endPosition, Vector3 leftOffset, Vector3 rightOffset, float segmentLength)
    {
        meshVert.Add(endPosition + rightOffset);
        meshVert.Add(endPosition + leftOffset);
        
        // 计算新的UV偏移量，基于线段长度进行纹理映射
        float newUVOffset = uvOffset + segmentLength / 5;
        meshUV.Add(new Vector2(newUVOffset, 0));
        meshUV.Add(new Vector2(newUVOffset, 1));
        
        // 更新UV偏移量，使用模运算实现循环纹理
        uvOffset = ((uvOffset * 5 + segmentLength) % 5) / 5.0f;
        totalVertexCount += 2;
    }

    /// <summary>
    /// 添加线段三角形
    /// 使用最新添加的4个顶点生成2个三角形，形成一个完整的线段四边形
    /// </summary>
    private void AddLineTriangles()
    {
        // 第一个三角形：左下 -> 右下 -> 右上
        meshTriangles.Add(totalVertexCount - 1);    // 左下
        meshTriangles.Add(totalVertexCount - 2);    // 右下
        meshTriangles.Add(totalVertexCount - 4);    // 右上
        
        // 第二个三角形：左下 -> 右上 -> 左上
        meshTriangles.Add(totalVertexCount - 1);    // 左下
        meshTriangles.Add(totalVertexCount - 4);    // 右上
        meshTriangles.Add(totalVertexCount - 3);    // 左上
    }
    #endregion

    #region 弧线绘制方法
    /// <summary>
    /// 计算弧线长度
    /// 根据起始角度、结束角度、半径和绘制方向计算弧线的实际长度
    /// </summary>
    private float CalculateArcLength(float startAngle, float endAngle, float radius, bool clockwise)
    {
        float arcLength = 0;
        
        if (clockwise) // 顺时针
        {
            if (endAngle > startAngle)
            {
                arcLength = (startAngle + 2 * Mathf.PI - endAngle) * radius;
            }
            else
            {
                arcLength = (startAngle - endAngle) * radius;
            }
        }
        else // 逆时针
        {
            if (endAngle > startAngle)
            {
                arcLength = (endAngle - startAngle) * radius;
            }
            else
            {
                arcLength = (endAngle + 2 * Mathf.PI - startAngle) * radius;
                Debug.LogError("arcLength:" + arcLength);
            }
        }
        
        return arcLength;
    }

    /// <summary>
    /// 添加弧线起始点
    /// 在弧线的起始角度位置添加一对顶点（内外侧各一个）
    /// </summary>
    private void AddArcStartPoint(Vector3 circleCenter, float radius, float startAngle, bool clockwise)
    {
        float currentAngle = startAngle;
        Vector3 pointOnCircle = new Vector3(Mathf.Cos(currentAngle), Mathf.Sin(currentAngle), 0);

        if (clockwise)
        {
            meshVert.Add(pointOnCircle * (radius - Width / 2) + circleCenter);
            meshVert.Add(pointOnCircle * (radius + Width / 2) + circleCenter);
        }
        else
        {
            meshVert.Add(pointOnCircle * (radius + Width / 2) + circleCenter);
            meshVert.Add(pointOnCircle * (radius - Width / 2) + circleCenter);
        }
        
        meshUV.Add(new Vector2(uvOffset, 0));
        meshUV.Add(new Vector2(uvOffset, 1));
        totalVertexCount += 2;
    }

    /// <summary>
    /// 生成弧线中间段
    /// 在起始点和结束点之间生成中间的顶点和三角形
    /// </summary>
    private void GenerateArcSegments(Vector3 circleCenter, float radius, float startAngle, float endAngle, float arcLength, bool clockwise)
    {
        float currentAngle = startAngle;
        
        for (int i = 0; i < arcLength - 1; i++)
        {
            uvOffset += 1 / 5.0f;
            
            // 计算当前角度
            if (clockwise)
            {
                currentAngle -= Mathf.Abs((startAngle - endAngle)) / arcLength;
            }
            else
            {
                currentAngle += Mathf.Abs((startAngle - endAngle)) / arcLength;
            }

            // 生成当前角度的顶点
            Vector3 pointOnCircle = new Vector3(Mathf.Cos(currentAngle), Mathf.Sin(currentAngle), 0);
            AddArcVertexPair(pointOnCircle, circleCenter, radius, clockwise);
            
            // 生成连接前一对顶点的三角形
            AddArcTriangles();
        }
    }

    /// <summary>
    /// 添加弧线顶点对
    /// 在指定位置添加内外两个顶点，并设置对应的UV坐标
    /// </summary>
    private void AddArcVertexPair(Vector3 pointOnCircle, Vector3 circleCenter, float radius, bool clockwise)
    {
        if (clockwise)
        {
            meshVert.Add(pointOnCircle * (radius - Width / 2) + circleCenter);
            meshVert.Add(pointOnCircle * (radius + Width / 2) + circleCenter);
        }
        else
        {
            meshVert.Add(pointOnCircle * (radius + Width / 2) + circleCenter);
            meshVert.Add(pointOnCircle * (radius - Width / 2) + circleCenter);
        }
        
        meshUV.Add(new Vector2(uvOffset, 0));
        meshUV.Add(new Vector2(uvOffset, 1));
        totalVertexCount += 2;
    }

    /// <summary>
    /// 添加弧线三角形
    /// 使用最新的4个顶点生成2个三角形，形成一个四边形段
    /// </summary>
    private void AddArcTriangles()
    {
        meshTriangles.Add(totalVertexCount - 1);
        meshTriangles.Add(totalVertexCount - 2);
        meshTriangles.Add(totalVertexCount - 4);
        meshTriangles.Add(totalVertexCount - 1);
        meshTriangles.Add(totalVertexCount - 4);
        meshTriangles.Add(totalVertexCount - 3);
    }

    /// <summary>
    /// 确保弧线终点精确
    /// 检查是否需要添加精确的终点，确保弧线结束在指定角度
    /// </summary>
    private void EnsureArcEndPoint(Vector3 circleCenter, float radius, float endAngle, float arcLength, float initialOffset, bool clockwise)
    {
        if (uvOffset != initialOffset + arcLength / 5.0f)
        {
            uvOffset = initialOffset + arcLength / 5.0f;
            float currentAngle = endAngle;
            Vector3 pointOnCircle = new Vector3(Mathf.Cos(currentAngle), Mathf.Sin(currentAngle), 0);
            
            AddArcVertexPair(pointOnCircle, circleCenter, radius, clockwise);
            AddArcTriangles();
        }
    }
    #endregion
}
