using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 网格生成脚本 - 负责生成绳子的顶点、三角形和UV数据
/// </summary>
public class RopeMeshGenerator : MonoBehaviour
{
    #region 组件和变量

    private MeshFilter filter;
    private Mesh mesh;
    private Vector3 startPos = new Vector3(0, 0, 0);

    public List<Vector3> meshVert;
    public List<int> meshTriangles;     // 三角形
    public List<Vector2> meshUV;
    public float Width = 1;

    private float offset = 0;           // 保存上一段结尾的x偏移量，附加给这一段
    private bool needStart = true;      // 需要添加4个点。在初始或者 moveTo时
    [SerializeField]
    private int pointNum = 0;           // 顶点总数。按照 sr，sl，er，el添加

    #endregion

    #region Unity生命周期

    void Start()
    {
        filter = GetComponent<MeshFilter>();
        mesh = new Mesh();
        filter.mesh = mesh;
        meshVert = new List<Vector3>{};
        meshTriangles = new List<int>{};
        meshUV = new List<Vector2>{};
    }

    void Update()
    {
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 应用网格数据
    /// </summary>
    public void stroke()
    {
        mesh.name = "MyMesh";
        mesh.Clear();
        
        // 为网格创建顶点数组
        Vector3[] vertices = meshVert.ToArray();
        
        // 通过顶点为网格创建三角形
        int[] triangles = meshTriangles.ToArray();
        
        // 为mesh设置纹理贴图坐标
        Vector2[] uv = meshUV.ToArray();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
    }

    /// <summary>
    /// 绘制直线到指定位置
    /// </summary>
    public void lineTo(Vector3 endPos)
    {
        // 步骤1：计算线段基本信息
        Vector3 sToEnd = endPos - startPos;
        float length = Vector3.Distance(startPos, endPos);
        
        // 步骤2：计算垂直方向向量（用于确定线段宽度）
        Vector3 left, Right;
        CalculatePerpendicularVectors(sToEnd, out left, out Right);
        
        // 步骤3：添加起始点顶点
        AddLineStartVertices(left, Right);
        
        // 步骤4：添加终点顶点
        AddLineEndVertices(endPos, left, Right, length);
        
        // 步骤5：生成连接三角形
        AddLineTriangles();
        
        // 步骤6：更新状态
        UpdateLineState(endPos);
    }

    /// <summary>
    /// 计算垂直方向向量
    /// 根据线段方向计算左右两个垂直向量，用于确定线段的宽度边界
    /// 算法：将方向向量旋转90度得到垂直向量，然后标准化并按宽度缩放
    /// </summary>
    /// <param name="direction">线段方向向量</param>
    /// <param name="leftVector">输出左侧垂直向量</param>
    /// <param name="rightVector">输出右侧垂直向量</param>
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
    /// 在线段起始位置添加左右两个顶点，形成线段的起始边界
    /// 同时设置对应的UV坐标
    /// </summary>
    /// <param name="leftOffset">左侧偏移向量</param>
    /// <param name="rightOffset">右侧偏移向量</param>
    private void AddLineStartVertices(Vector3 leftOffset, Vector3 rightOffset)
    {
        // 初始添加4个点，后可只添加两个点
        if (true)
        {
            meshVert.Add(startPos + rightOffset);
            meshVert.Add(startPos + leftOffset);
            pointNum += 2;
            
            // 添加UV坐标，使用当前偏移量
            meshUV.Add(new Vector2(offset, 0));
            meshUV.Add(new Vector2(offset, 1));
        }
    }

    /// <summary>
    /// 添加线段终点顶点
    /// 在线段终点位置添加左右两个顶点，形成线段的结束边界
    /// 根据线段长度计算新的UV偏移量，实现纹理的连续映射
    /// </summary>
    /// <param name="endPosition">线段终点位置</param>
    /// <param name="leftOffset">左侧偏移向量</param>
    /// <param name="rightOffset">右侧偏移向量</param>
    /// <param name="segmentLength">线段长度</param>
    private void AddLineEndVertices(Vector3 endPosition, Vector3 leftOffset, Vector3 rightOffset, float segmentLength)
    {
        meshVert.Add(endPosition + rightOffset);
        meshVert.Add(endPosition + leftOffset);
        
        // 计算新的UV偏移量，基于线段长度进行纹理映射
        float newUVOffset = offset + segmentLength / 5;
        meshUV.Add(new Vector2(newUVOffset, 0));
        meshUV.Add(new Vector2(newUVOffset, 1));
        
        // 更新UV偏移量，使用模运算实现循环纹理
        offset = ((offset * 5 + segmentLength) % 5) / 5.0f;
        pointNum += 2;
    }

    /// <summary>
    /// 添加线段三角形
    /// 使用最新添加的4个顶点生成2个三角形，形成一个完整的线段四边形
    /// 三角形顶点按照正确的绕序排列，确保面朝向正确
    /// 
    /// 顶点索引布局：
    /// pointNum-4 (右上) ---- pointNum-2 (右下)
    ///     |                      |
    /// pointNum-3 (左上) ---- pointNum-1 (左下)
    /// </summary>
    private void AddLineTriangles()
    {
        // 第一个三角形：左下 -> 右下 -> 右上
        meshTriangles.Add(pointNum - 1);    // 左下
        meshTriangles.Add(pointNum - 2);    // 右下
        meshTriangles.Add(pointNum - 4);    // 右上
        
        // 第二个三角形：左下 -> 右上 -> 左上
        meshTriangles.Add(pointNum - 1);    // 左下
        meshTriangles.Add(pointNum - 4);    // 右上
        meshTriangles.Add(pointNum - 3);    // 左上
    }

    /// <summary>
    /// 更新线段状态
    /// 更新起始位置和状态标志，为下一次绘制做准备
    /// </summary>
    /// <param name="newStartPos">新的起始位置</param>
    private void UpdateLineState(Vector3 newStartPos)
    {
        startPos = newStartPos;
        needStart = false;
    }

    /// <summary>
    /// 添加弧线路径
    /// </summary>
    /// <param name="circlePoint">圆心位置</param>
    /// <param name="radius">弧线中点到圆心距离</param>
    /// <param name="startRad">起始角，以弧度计。（弧的圆形的三点钟位置是 0 度）。</param>
    /// <param name="endRad">结束角，以弧度计。三点钟为2pi</param>
    /// <param name="clockwise">可选。规定应该逆时针还是顺时针绘图，默认false逆指针。False = 逆时针，true = 顺时针</param>
    public void circle(Vector3 circlePoint, float radius, float startRad, float endRad, bool clockwise = false)
    {
        // 步骤1：计算弧线长度
        float cirLength = CalculateArcLength(startRad, endRad, radius, clockwise);
        float temOffset = offset;
        
        // 步骤2：添加弧线起始点
        AddArcStartPoint(circlePoint, radius, startRad, clockwise);
        
        // 步骤3：生成弧线中间段
        GenerateArcSegments(circlePoint, radius, startRad, endRad, cirLength, clockwise);
        
        // 步骤4：确保弧线终点精确
        EnsureArcEndPoint(circlePoint, radius, endRad, cirLength, temOffset, clockwise);
        
        // 步骤5：更新偏移量和状态
        offset = (offset * 5 % 5) / 5.0f;
        needStart = true;
    }

    /// <summary>
    /// 计算弧线长度
    /// 根据起始角度、结束角度、半径和绘制方向计算弧线的实际长度
    /// </summary>
    /// <param name="startRad">起始角度（弧度）</param>
    /// <param name="endRad">结束角度（弧度）</param>
    /// <param name="radius">弧线半径</param>
    /// <param name="clockwise">是否顺时针绘制</param>
    /// <returns>弧线长度</returns>
    private float CalculateArcLength(float startRad, float endRad, float radius, bool clockwise)
    {
        float cirLength = 0;
        
        if (clockwise) // 顺时针
        {
            if (endRad > startRad)
            {
                cirLength = (startRad + 2 * Mathf.PI - endRad) * radius;
            }
            else
            {
                cirLength = (startRad - endRad) * radius;
            }
        }
        else // 逆时针
        {
            if (endRad > startRad)
            {
                cirLength = (endRad - startRad) * radius;
            }
            else
            {
                cirLength = (endRad + 2 * Mathf.PI - startRad) * radius;
                Debug.LogError("cirLength:" + cirLength);
            }
        }
        
        return cirLength;
    }

    /// <summary>
    /// 添加弧线起始点
    /// 在弧线的起始角度位置添加一对顶点（内外侧各一个）
    /// </summary>
    /// <param name="circlePoint">圆心位置</param>
    /// <param name="radius">弧线半径</param>
    /// <param name="startRad">起始角度</param>
    /// <param name="clockwise">是否顺时针绘制</param>
    private void AddArcStartPoint(Vector3 circlePoint, float radius, float startRad, bool clockwise)
    {
        float rad = startRad;
        Vector3 temPoint = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0); // 临时的点

        if (clockwise)
        {
            meshVert.Add(temPoint * (radius - Width / 2) + circlePoint);
            meshVert.Add(temPoint * (radius + Width / 2) + circlePoint);
        }
        else
        {
            meshVert.Add(temPoint * (radius + Width / 2) + circlePoint);
            meshVert.Add(temPoint * (radius - Width / 2) + circlePoint);
        }
        
        meshUV.Add(new Vector2(offset, 0));
        meshUV.Add(new Vector2(offset, 1));
        pointNum += 2;
    }

    /// <summary>
    /// 生成弧线中间段
    /// 在起始点和结束点之间生成中间的顶点和三角形
    /// 每个步进生成一对顶点，并连接前一对顶点形成四边形（两个三角形）
    /// </summary>
    /// <param name="circlePoint">圆心位置</param>
    /// <param name="radius">弧线半径</param>
    /// <param name="startRad">起始角度</param>
    /// <param name="endRad">结束角度</param>
    /// <param name="cirLength">弧线长度</param>
    /// <param name="clockwise">是否顺时针绘制</param>
    private void GenerateArcSegments(Vector3 circlePoint, float radius, float startRad, float endRad, float cirLength, bool clockwise)
    {
        float rad = startRad;
        
        for (int i = 0; i < cirLength - 1; i++)
        {
            offset += 1 / 5.0f;
            
            // 计算当前角度
            if (clockwise)
            {
                rad -= Mathf.Abs((startRad - endRad)) / cirLength;
            }
            else
            {
                rad += Mathf.Abs((startRad - endRad)) / cirLength;
            }

            // 生成当前角度的顶点
            Vector3 temPoint = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0);
            AddArcVertexPair(temPoint, circlePoint, radius, clockwise);
            
            // 生成连接前一对顶点的三角形
            AddArcTriangles();
        }
    }

    /// <summary>
    /// 添加弧线顶点对
    /// 在指定位置添加内外两个顶点，并设置对应的UV坐标
    /// </summary>
    /// <param name="pointOnCircle">圆周上的单位向量点</param>
    /// <param name="circlePoint">圆心位置</param>
    /// <param name="radius">弧线半径</param>
    /// <param name="clockwise">是否顺时针绘制</param>
    private void AddArcVertexPair(Vector3 pointOnCircle, Vector3 circlePoint, float radius, bool clockwise)
    {
        if (clockwise)
        {
            meshVert.Add(pointOnCircle * (radius - Width / 2) + circlePoint);
            meshVert.Add(pointOnCircle * (radius + Width / 2) + circlePoint);
        }
        else
        {
            meshVert.Add(pointOnCircle * (radius + Width / 2) + circlePoint);
            meshVert.Add(pointOnCircle * (radius - Width / 2) + circlePoint);
        }
        
        meshUV.Add(new Vector2(offset, 0));
        meshUV.Add(new Vector2(offset, 1));
        pointNum += 2;
    }

    /// <summary>
    /// 添加弧线三角形
    /// 使用最新的4个顶点生成2个三角形，形成一个四边形段
    /// 三角形顶点顺序确保正确的面朝向
    /// </summary>
    private void AddArcTriangles()
    {
        meshTriangles.Add(pointNum - 1);
        meshTriangles.Add(pointNum - 2);
        meshTriangles.Add(pointNum - 4);
        meshTriangles.Add(pointNum - 1);
        meshTriangles.Add(pointNum - 4);
        meshTriangles.Add(pointNum - 3);
    }

    /// <summary>
    /// 确保弧线终点精确
    /// 检查是否需要添加精确的终点，确保弧线结束在指定角度
    /// 这是为了处理循环步进可能产生的精度误差
    /// </summary>
    /// <param name="circlePoint">圆心位置</param>
    /// <param name="radius">弧线半径</param>
    /// <param name="endRad">结束角度</param>
    /// <param name="cirLength">弧线长度</param>
    /// <param name="temOffset">原始偏移量</param>
    /// <param name="clockwise">是否顺时针绘制</param>
    private void EnsureArcEndPoint(Vector3 circlePoint, float radius, float endRad, float cirLength, float temOffset, bool clockwise)
    {
        if (offset != temOffset + cirLength / 5.0f)
        {
            offset = temOffset + cirLength / 5.0f;
            float rad = endRad;
            Vector3 temPoint = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0);
            
            AddArcVertexPair(temPoint, circlePoint, radius, clockwise);
            AddArcTriangles();
        }
    }

    /// <summary>
    /// 移动到指定位置
    /// </summary>
    public void moveTo(Vector2 endPos)
    {
        startPos = endPos;
        needStart = true;
    }

    /// <summary>
    /// 清空所有数据
    /// </summary>
    public void clear()
    {
        meshVert.Clear();
        meshTriangles.Clear();
        meshUV.Clear();
        needStart = true;
        startPos = new Vector3(0, 0, 0);
        pointNum = 0;
        offset = 0;
    }

    #endregion
}
