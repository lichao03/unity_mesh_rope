using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

/// <summary>
/// 绳子锚点列数据结构
/// 存储绳子与柱子交互的所有相关信息
/// </summary>
public class myColumn
{
    public GameObject column;
    public Vector3 columnWorldPos;
    public Vector3 startWorldPos;
    public Vector3 preTangentWorldPos;  // 前一个切点
    public float rad;                   // 绕柱子的弧度值，带方向
    
    public myColumn(GameObject c, Vector3 swp)
    {
        this.column = c;
        this.columnWorldPos = this.column.transform.position;
        this.startWorldPos = swp;
        this.preTangentWorldPos = this.startWorldPos;
        this.rad = 0;
    }
    
    public string mytoString()
    {
        return this.column.name + this.columnWorldPos + this.startWorldPos + "\n" + this.preTangentWorldPos + "\t" + this.rad;
    }
}

/// <summary>
/// 绳子末端控制器
/// 负责绳子的物理运动、碰撞检测、路径计算和网格渲染
/// </summary>
public class ropeEnd : MonoBehaviour
{
    #region 公共字段
    public RopeMeshGenerator ms;
    public Transform ropeStart;
    public float lineWidth = 1;
    #endregion

    #region 私有字段
    private bool isDragging = false;                    // 是否正在拖拽
    private List<myColumn> anchorColumns = new List<myColumn>{}; // 锚点列表
    private Vector3 mouseWorldPos = new Vector3(0, 0, 0);        // 鼠标世界坐标
    private Vector3 newTangentPoint = new Vector3(0, 1, 0);      // 新计算的切点
    #endregion

    #region Unity生命周期
    void Start()
    {
        isDragging = false;
        this.anchorColumns.Add(new myColumn(this.ropeStart.gameObject, ropeStart.position));
    }

    void Update()
    {
        // 步骤1：输出调试信息
        LogDebugInfo();
        
        // 步骤2：清空网格并更新物理运动
        ms.clear();
        UpdatePhysicsMovement();
        
        // 步骤3：执行射线检测
        RaycastHit2D[] results = PerformRaycastDetection();
        
        // 步骤4：处理射线检测结果
        ProcessRaycastResults(results);
        
        // 步骤5：执行锚点优化检查
        PerformAnchorOptimization();
        
        // 步骤6：渲染绳子
        RenderRope();
    }
    #endregion

    #region 鼠标交互事件
    private void OnMouseDown()
    {
        isDragging = true;
        mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition) + new Vector3(0, 10, 0);
    }

    private void OnMouseDrag()
    {
        mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition) + new Vector3(0, 10, 0);
    }

    private void OnMouseUp()
    {
        isDragging = false;
    }
    #endregion

    #region 物理更新
    /// <summary>
    /// 更新物理运动
    /// 根据当前状态（拖拽或自由）更新绳子末端的物理运动
    /// </summary>
    private void UpdatePhysicsMovement()
    {
        if (this.isDragging)
        {
            // 拖拽模式：向鼠标位置移动
            GetComponent<Rigidbody2D>().velocity = (this.mouseWorldPos - transform.position) * 10;
        }
        else
        {
            // 自由模式：向最后锚点的切点移动
            Vector3 targetPos = this.anchorColumns[this.anchorColumns.Count - 1].preTangentWorldPos;
            float distance = Vector3.Distance(transform.position, targetPos);
            
            if (distance < 1)
            {
                // 近距离时使用比例控制，避免震荡
                this.GetComponent<Rigidbody2D>().velocity = (targetPos - transform.position) * 10;
            }
            else
            {
                // 远距离时使用固定速度
                this.GetComponent<Rigidbody2D>().velocity = (targetPos - transform.position).normalized * 100;
            }
        }
    }
    #endregion

    #region 射线检测和碰撞处理
    /// <summary>
    /// 执行射线检测
    /// 从绳子末端向目标点发射射线，检测路径上的碰撞体
    /// </summary>
    private RaycastHit2D[] PerformRaycastDetection()
    {
        RaycastHit2D[] results;
        
        if (this.anchorColumns.Count > 1)
        {
            // 多锚点情况：向最后锚点的切点发射射线
            Vector3 targetPos = this.anchorColumns[this.anchorColumns.Count - 1].preTangentWorldPos;
            Vector3 direction = targetPos - transform.position;
            float distance = Vector3.Distance(transform.position, targetPos);
            
            results = Physics2D.RaycastAll(transform.position, direction, distance);
        }
        else
        {
            // 单锚点情况：向起始点发射射线
            Vector3 targetPos = this.anchorColumns[this.anchorColumns.Count - 1].startWorldPos;
            Vector3 direction = targetPos - transform.position;
            float distance = Vector3.Distance(transform.position, targetPos);
            
            results = Physics2D.RaycastAll(transform.position, direction, distance);
        }

        // 输出射线检测结果用于调试
        LogRaycastResults(results);
        
        return results;
    }

    /// <summary>
    /// 处理射线检测结果
    /// 根据检测结果决定是添加新锚点还是更新现有锚点
    /// </summary>
    private void ProcessRaycastResults(RaycastHit2D[] results)
    {
        if (results.Length > 1)
        {
            // 检测到碰撞体
            var hitObject = results[1].collider.gameObject;
            var lastColumn = this.anchorColumns[this.anchorColumns.Count - 1];
            
            if (hitObject != lastColumn.column)
            {
                // 碰撞到新的圆柱 - 添加新锚点
                AddNewAnchor(hitObject, results[1].point);
            }
            else
            {
                // 再次碰撞到同一圆柱 - 更新缠绕信息
                UpdateExistingAnchorWrap();
            }
        }
        else
        {
            // 无碰撞 - 更新现有锚点
            if (this.anchorColumns.Count > 1)
            {
                UpdateExistingAnchorWrap();
            }
        }
    }
    #endregion

    #region 锚点管理
    /// <summary>
    /// 添加新锚点
    /// 当检测到新的圆柱时，计算切点并创建新的锚点
    /// </summary>
    private void AddNewAnchor(GameObject newColumnObject, Vector3 contactPoint)
    {
        float radius = newColumnObject.transform.localScale.x / 10;
        var tangentPoint = this.CalculateTangentPoint(newColumnObject.transform.position, this.transform.position, radius, contactPoint);
        Debug.Log("sp:" + tangentPoint);
        
        var newColumn = new myColumn(newColumnObject, tangentPoint);
        this.anchorColumns.Add(newColumn);
    }

    /// <summary>
    /// 更新现有锚点的缠绕信息
    /// 重新计算切点位置和缠绕角度，判断是否需要移除锚点
    /// </summary>
    private void UpdateExistingAnchorWrap()
    {
        var lastColumn = this.anchorColumns[this.anchorColumns.Count - 1];
        
        // 重新计算切点位置
        this.newTangentPoint = this.CalculateTangentPoint(
            lastColumn.columnWorldPos, 
            transform.position, 
            lastColumn.column.transform.localScale.x / 10, 
            lastColumn.preTangentWorldPos
        );
        
        // 计算角度变化
        float angleChange = this.CalculateAngleBetweenPoints(lastColumn.preTangentWorldPos, this.newTangentPoint, lastColumn.columnWorldPos);
        
        // 反向缠绕检测：当前角度 * (当前角度 + 新角度) < 0 表示方向相反
        if (lastColumn.rad * (lastColumn.rad + angleChange) < 0)
        {
            // 检测到反向缠绕，移除该锚点
            RemoveLastAnchor();
        }
        else
        {
            // 正常缠绕，更新角度和切点
            lastColumn.rad += angleChange;
            lastColumn.preTangentWorldPos = this.newTangentPoint;
        }
    }

    /// <summary>
    /// 移除最后一个锚点
    /// 当检测到反向缠绕时移除锚点，并进行相关清理
    /// </summary>
    private void RemoveLastAnchor()
    {
        var columnToRemove = this.anchorColumns[this.anchorColumns.Count - 1];
        this.anchorColumns.Remove(columnToRemove);
        
        // 如果这是该圆柱的唯一引用，执行清理操作
        if (this.CountColumnReferences(columnToRemove.column) == 0)
        {
            // 这里可以添加圆柱相关的清理代码，如取消高亮等
        }
    }

    /// <summary>
    /// 执行锚点优化检查
    /// 防止绳子"穿透"圆柱的优化算法
    /// </summary>
    private void PerformAnchorOptimization()
    {
        if (this.anchorColumns.Count <= 1) return;
        
        var lastColumn = this.anchorColumns[this.anchorColumns.Count - 1];
        
        // 计算绳子在圆柱上缠绕的中点位置
        var midPoint = Vector2ToVector3(RotateVector2D(
            lastColumn.startWorldPos - lastColumn.columnWorldPos,
            lastColumn.rad / 2
        )) + lastColumn.columnWorldPos;
        
        var midDirection = (midPoint - lastColumn.columnWorldPos).normalized;

        // 防止绳子没从柱子上真正"下来"的检测
        bool isPartialWrap = Mathf.Abs(lastColumn.rad) < (Math.PI * 2) - 0.5f;
        
        if (isPartialWrap && this.anchorColumns.Count > 1)
        {
            // 计算前一段绳子的方向
            var previousDirection = (this.anchorColumns[this.anchorColumns.Count - 2].preTangentWorldPos - lastColumn.startWorldPos).normalized;
            
            // 检查方向一致性
            float directionDot = Vector3.Dot(midDirection, previousDirection);
            
            if (directionDot > 0)
            {
                // 方向过于一致，移除不必要的锚点
                RemoveLastAnchor();
            }
        }
    }
    #endregion

    #region 绳子渲染
    /// <summary>
    /// 渲染绳子
    /// 根据锚点列表生成完整的绳子网格
    /// </summary>
    private void RenderRope()
    {
        this.ms.Width = this.lineWidth;
        this.ms.moveTo(transform.position);
        
        // 逆向遍历锚点，从最后一个向起始点渲染
        for (int i = this.anchorColumns.Count - 1; i > 0; i--)
        {
            RenderAnchorSegment(this.anchorColumns[i]);
        }
        
        // 连接到起始点
        this.ms.lineTo(ropeStart.position);
        
        // 应用网格渲染
        this.ms.stroke();
    }

    /// <summary>
    /// 渲染单个锚点段
    /// 包含直线段、弧线段和位置移动
    /// </summary>
    private void RenderAnchorSegment(myColumn column)
    {
        // 1. 绘制到切点的直线（考虑绳子宽度偏移）
        var tangentNormal = (column.preTangentWorldPos - column.columnWorldPos).normalized;
        var offsetTangentPoint = column.preTangentWorldPos + (tangentNormal * (this.lineWidth / 2));
        this.ms.lineTo(new Vector2(offsetTangentPoint.x, offsetTangentPoint.y));
        
        // 2. 绘制圆柱上的弧线段
        RenderColumnArc(column);
        
        // 3. 移动到起始点（为下一段做准备）
        var startNormal = (column.startWorldPos - column.columnWorldPos).normalized;
        var offsetStartPoint = column.startWorldPos + (startNormal * (this.lineWidth / 2));
        this.ms.moveTo(offsetStartPoint);
    }

    /// <summary>
    /// 渲染圆柱上的弧线段
    /// 根据缠绕角度决定渲染完整圆圈还是部分弧线
    /// </summary>
    private void RenderColumnArc(myColumn column)
    {
        float wrapAngle = column.rad;
        float renderRadius = column.column.transform.localScale.x / 10 + this.lineWidth / 2;
        
        if (Mathf.Abs(wrapAngle) >= Math.PI * 2)
        {
            // 完整圆圈的情况
            if (wrapAngle > 0)
            {
                // 顺时针绕上柱子，但线要逆时针从结点向起始点画
                this.ms.circle(column.columnWorldPos, renderRadius, 0, Mathf.PI * 2, false);
            }
            else
            {
                this.ms.circle(column.columnWorldPos, renderRadius, Mathf.PI * 2, 0, true);
            }
            
            this.ms.moveTo(column.startWorldPos);
        }
        else if (Mathf.Abs(wrapAngle) != 0)
        {
            // 部分弧线的情况
            var startAngle = this.GetPointAngleOnCircle(column.preTangentWorldPos, column.columnWorldPos);
            this.ms.circle(column.columnWorldPos, renderRadius, startAngle, startAngle + wrapAngle, wrapAngle < 0);
        }
    }
    #endregion

    #region 数学工具方法
    /// <summary>
    /// 计算点在圆上的角度
    /// </summary>
    private float GetPointAngleOnCircle(Vector2 point, Vector2 center)
    {
        var directionVector = point - center;
        var referenceVector = new Vector2(1, 0);
        return Vector2.SignedAngle(referenceVector, directionVector) / 180 * Mathf.PI;
    }

    /// <summary>
    /// 计算从圆外一点到圆的切点
    /// </summary>
    private Vector2 CalculateTangentPoint(Vector2 circleCenter, Vector2 externalPoint, float radius, Vector2 alignPoint)
    { 
        var translatedPoint = new Vector2(0, 0);
        var projectedPoint = new Vector2(0, 0);
        var rotatedPoint = new Vector2(0, 0);
        var finalPoint = new Vector2(0, 0);
        
        // 1. 坐标平移到圆心处,求园外点的新坐标
        translatedPoint = externalPoint - circleCenter;
        
        // 2. 求园与OE的交点坐标, 相当于缩放变换
        var scaleFactor = radius / translatedPoint.magnitude;
        projectedPoint = translatedPoint * scaleFactor;
        
        var rotationAngle = 0.0f;
        
        // 3. 将E旋转变换角度a到切点，其中cos(a)=r/OF=t, 所以a=arccos(t);
        if (this.CalculateAngleBetweenPoints(externalPoint, alignPoint, circleCenter) < 0)
        {
            rotationAngle = -Mathf.Acos(scaleFactor);   // 得到旋转角度 角度为负
        }
        else
        {
            rotationAngle = Mathf.Acos(scaleFactor);    // 得到旋转角度  角度为正
        }
        
        rotatedPoint = RotateVector2D(projectedPoint, rotationAngle);
        
        // 4. 将G平移到原来的坐标下得到新坐标
        finalPoint = rotatedPoint + circleCenter;

        // 5. 返回结果
        return finalPoint;
    }

    /// <summary>
    /// 统计指定柱子在锚点列表中的引用次数
    /// </summary>
    private float CountColumnReferences(GameObject targetColumn)
    {
        float count = 0.0f;
        for (var i = 1; i < this.anchorColumns.Count; i++)
        {
            if (this.anchorColumns[this.anchorColumns.Count - 1].column == targetColumn)
            {
                count++;
                break;
            }
        }
        return count;
    }

    /// <summary>
    /// 计算两点在圆周上的夹角
    /// </summary>
    private float CalculateAngleBetweenPoints(Vector2 pointA, Vector2 pointB, Vector2 center)
    {
        var directionA = pointA - center;
        var directionB = pointB - center;
        return Vector2.SignedAngle(directionB, directionA) / 180 * Mathf.PI;
    }

    /// <summary>
    /// Vector2转Vector3
    /// </summary>
    private Vector3 Vector2ToVector3(Vector2 vector)
    {
        return new Vector3(vector.x, vector.y, 0);
    }

    /// <summary>
    /// 2D向量旋转
    /// </summary>
    private Vector2 RotateVector2D(Vector2 vector, float angleInRadians)
    {
        Vector2 rotatedVector = new Vector2(
            vector.x * Mathf.Cos(angleInRadians) + vector.y * Mathf.Sin(angleInRadians),
            -vector.x * Mathf.Sin(angleInRadians) + vector.y * Mathf.Cos(angleInRadians)
        );
        return rotatedVector;
    }
    #endregion

    #region 调试方法
    /// <summary>
    /// 输出调试信息
    /// 打印所有锚点的详细信息，用于调试和监控绳子状态
    /// </summary>
    private void LogDebugInfo()
    {
        for (var i = 0; i < anchorColumns.Count; i++)
        {
            Debug.Log(i + "\tcolumn:" + anchorColumns[i].mytoString());
        }
    }

    /// <summary>
    /// 输出射线检测结果
    /// 调试用函数，打印所有检测到的碰撞体信息
    /// </summary>
    private void LogRaycastResults(RaycastHit2D[] results)
    {
        for (var i = 0; i < results.Length; i++)
        {
            Debug.Log(i + "\t result:" + results[i].transform.name);
        }
        Debug.Log(results);
    }
    #endregion
}
