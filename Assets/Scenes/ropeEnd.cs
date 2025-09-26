using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
public class myColumn{
    public GameObject column;
    public Vector3 columnWorldPos;
    public Vector3 startWorldPos;
    public Vector3 preTangentWorldPos;//前一个点
    public float rad;//绕柱子的弧度值  带方向
    public myColumn(GameObject c,Vector3 swp){
        this.column=c;
        this.columnWorldPos=this.column.transform.position;
        this.startWorldPos=swp;
        this.preTangentWorldPos=this.startWorldPos;
        this.rad=0;
    }
    public string  mytoString(){
        
        return this.column.name+this.columnWorldPos+this.startWorldPos+"\n"+this.preTangentWorldPos+"\t"+this.rad;
    }
}
public class ropeEnd : MonoBehaviour
{
    // Start is called before the first frame update
    public meshScript ms;
    public Transform ropeStart;
    public float lineWidth =1;
    bool flag=false;
    List <myColumn>columns=new List<myColumn>{};
    Vector3 mouseWorldPos=new Vector3(0,0,0);
    Vector3 newTangentP=new Vector3(0,1,0);
    void Start()
    {
        flag=false;
        
        this.columns.Add(new myColumn(this.ropeStart.gameObject,ropeStart.position));
    }
    private void OnMouseDown() {
        //Debug.Log(Camera.main.ScreenToWorldPoint(Input.mousePosition));
        flag=true;
        
        mouseWorldPos=Camera.main.ScreenToWorldPoint(Input.mousePosition)+new Vector3(0,10,0);
    }
    private void OnMouseDrag() {
        mouseWorldPos=Camera.main.ScreenToWorldPoint(Input.mousePosition)+new Vector3(0,10,0);
    }
    private void OnMouseUp() {
        flag=false;
    }
    // Update is called once per frame
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

    /// <summary>
    /// 输出调试信息
    /// 打印所有锚点的详细信息，用于调试和监控绳子状态
    /// </summary>
    private void LogDebugInfo()
    {
        for(var i = 0; i < columns.Count; i++)
        {
            Debug.Log(i + "\tcolumn:" + columns[i].mytoString());
        }
    }

    /// <summary>
    /// 更新物理运动
    /// 根据当前状态（拖拽或自由）更新绳子末端的物理运动
    /// 算法逻辑：
    /// - 拖拽状态：直接跟随鼠标移动
    /// - 自由状态：向最后锚点的切点移动，近距离时减速，远距离时匀速
    /// </summary>
    private void UpdatePhysicsMovement()
    {
        if(this.flag)
        {
            // 拖拽模式：向鼠标位置移动
            GetComponent<Rigidbody2D>().velocity = (this.mouseWorldPos - transform.position) * 10;
        }
        else
        {
            // 自由模式：向最后锚点的切点移动
            Vector3 targetPos = this.columns[this.columns.Count - 1].preTangentWorldPos;
            float distance = Vector3.Distance(transform.position, targetPos);
            
            if(distance < 1)
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

    /// <summary>
    /// 执行射线检测
    /// 从绳子末端向目标点发射射线，检测路径上的碰撞体
    /// 检测逻辑：
    /// - 多锚点时：向最后锚点的切点发射
    /// - 单锚点时：向起始点发射
    /// </summary>
    /// <returns>射线检测结果数组</returns>
    private RaycastHit2D[] PerformRaycastDetection()
    {
        RaycastHit2D[] results;
        
        if(this.columns.Count > 1)
        {
            // 多锚点情况：向最后锚点的切点发射射线
            Vector3 targetPos = this.columns[this.columns.Count - 1].preTangentWorldPos;
            Vector3 direction = targetPos - transform.position;
            float distance = Vector3.Distance(transform.position, targetPos);
            
            results = Physics2D.RaycastAll(transform.position, direction, distance);
        }
        else
        {
            // 单锚点情况：向起始点发射射线
            Vector3 targetPos = this.columns[this.columns.Count - 1].startWorldPos;
            Vector3 direction = targetPos - transform.position;
            float distance = Vector3.Distance(transform.position, targetPos);
            
            results = Physics2D.RaycastAll(transform.position, direction, distance);
        }

        // 输出射线检测结果用于调试
        LogRaycastResults(results);
        
        return results;
    }

    /// <summary>
    /// 输出射线检测结果
    /// 调试用函数，打印所有检测到的碰撞体信息
    /// </summary>
    /// <param name="results">射线检测结果</param>
    private void LogRaycastResults(RaycastHit2D[] results)
    {
        for(var i = 0; i < results.Length; i++)
        {
            Debug.Log(i + "\t result:" + results[i].transform.name);
        }
        Debug.Log(results);
    }

    /// <summary>
    /// 处理射线检测结果
    /// 根据检测结果决定是添加新锚点还是更新现有锚点
    /// 核心逻辑：
    /// - 检测到碰撞：判断是新物体还是已存在的锚点
    /// - 新物体：添加新锚点
    /// - 已存在锚点：更新缠绕信息，检查是否需要移除
    /// - 无碰撞：更新现有锚点的切点位置
    /// </summary>
    /// <param name="results">射线检测结果数组</param>
    private void ProcessRaycastResults(RaycastHit2D[] results)
    {
        if(results.Length > 1)
        {
            // 检测到碰撞体
            var hitObject = results[1].collider.gameObject;
            var lastColumn = this.columns[this.columns.Count - 1];
            
            if(hitObject != lastColumn.column)
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
            if(this.columns.Count > 1)
            {
                UpdateExistingAnchorWrap();
            }
        }
    }

    /// <summary>
    /// 添加新锚点
    /// 当检测到新的圆柱时，计算切点并创建新的锚点
    /// </summary>
    /// <param name="newColumnObject">新检测到的圆柱对象</param>
    /// <param name="contactPoint">碰撞点位置</param>
    private void AddNewAnchor(GameObject newColumnObject, Vector3 contactPoint)
    {
        float radius = newColumnObject.transform.localScale.x / 10;
        var tangentPoint = this.CalcQieDian(newColumnObject.transform.position, this.transform.position, radius, contactPoint);
        Debug.Log("sp:" + tangentPoint);
        
        var newColumn = new myColumn(newColumnObject, tangentPoint);
        this.columns.Add(newColumn);
    }

    /// <summary>
    /// 更新现有锚点的缠绕信息
    /// 重新计算切点位置和缠绕角度，判断是否需要移除锚点
    /// 算法核心：反向缠绕检测
    /// - 如果当前缠绕角度与新增角度符号相反，说明绳子开始反向缠绕，移除该锚点
    /// - 否则累加缠绕角度并更新切点位置
    /// </summary>
    private void UpdateExistingAnchorWrap()
    {
        var lastColumn = this.columns[this.columns.Count - 1];
        
        // 重新计算切点位置
        this.newTangentP = this.CalcQieDian(
            lastColumn.columnWorldPos, 
            transform.position, 
            lastColumn.column.transform.localScale.x / 10, 
            lastColumn.preTangentWorldPos
        );
        
        // 计算角度变化
        float angleChange = this.radTwoPOnR(lastColumn.preTangentWorldPos, this.newTangentP, lastColumn.columnWorldPos);
        
        // 反向缠绕检测：当前角度 * (当前角度 + 新角度) < 0 表示方向相反
        if(lastColumn.rad * (lastColumn.rad + angleChange) < 0)
        {
            // 检测到反向缠绕，移除该锚点
            RemoveLastAnchor();
        }
        else
        {
            // 正常缠绕，更新角度和切点
            lastColumn.rad += angleChange;
            lastColumn.preTangentWorldPos = this.newTangentP;
        }
    }

    /// <summary>
    /// 移除最后一个锚点
    /// 当检测到反向缠绕时移除锚点，并进行相关清理
    /// </summary>
    private void RemoveLastAnchor()
    {
        var columnToRemove = this.columns[this.columns.Count - 1];
        this.columns.Remove(columnToRemove);
        
        // 如果这是该圆柱的唯一引用，执行清理操作
        if(this.ifOnlyColumn(columnToRemove.column) == 0)
        {
            // 这里可以添加圆柱相关的清理代码，如取消高亮等
        }
    }

    /// <summary>
    /// 执行锚点优化检查
    /// 防止绳子"穿透"圆柱的优化算法
    /// 检查绳子是否真正从柱子上"下来"，如果路径不合理则移除锚点
    /// 
    /// 优化原理：
    /// 1. 计算绳子在圆柱上的中点位置（旋转一半角度）
    /// 2. 检查中点方向与前一段绳子方向的一致性
    /// 3. 如果方向过于一致且缠绕不足，说明绳子没有真正缠绕，应移除锚点
    /// </summary>
    private void PerformAnchorOptimization()
    {
        if(this.columns.Count <= 1) return;
        
        var lastColumn = this.columns[this.columns.Count - 1];
        
        // 计算绳子在圆柱上缠绕的中点位置
        var midPoint = v2Tov3(rotateVec2(
            lastColumn.startWorldPos - lastColumn.columnWorldPos,
            lastColumn.rad / 2
        )) + lastColumn.columnWorldPos;
        
        var midDirection = (midPoint - lastColumn.columnWorldPos).normalized;

        // 防止绳子没从柱子上真正"下来"的检测
        bool isPartialWrap = Mathf.Abs(lastColumn.rad) < (Math.PI * 2) - 0.5f;
        
        if(isPartialWrap && this.columns.Count > 1)
        {
            // 计算前一段绳子的方向
            var previousDirection = (this.columns[this.columns.Count - 2].preTangentWorldPos - lastColumn.startWorldPos).normalized;
            
            // 检查方向一致性
            float directionDot = Vector3.Dot(midDirection, previousDirection);
            
            if(directionDot > 0)
            {
                // 方向过于一致，移除不必要的锚点
                RemoveLastAnchor();
            }
        }
    }

    /// <summary>
    /// 渲染绳子
    /// 根据锚点列表生成完整的绳子网格
    /// 渲染流程：
    /// 1. 设置绳子宽度
    /// 2. 从末端开始逆向渲染每个锚点段
    /// 3. 每个锚点段包含：直线 + 弧线 + 连接段
    /// 4. 最后连接到起始点并应用网格
    /// </summary>
    private void RenderRope()
    {
        this.ms.Width = this.lineWidth;
        this.ms.moveTo(transform.position);
        
        // 逆向遍历锚点，从最后一个向起始点渲染
        for(int i = this.columns.Count - 1; i > 0; i--)
        {
            RenderAnchorSegment(this.columns[i]);
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
    /// <param name="column">要渲染的锚点</param>
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
    /// <param name="column">锚点信息</param>
    private void RenderColumnArc(myColumn column)
    {
        float wrapAngle = column.rad;
        float renderRadius = column.column.transform.localScale.x / 10 + this.lineWidth / 2;
        
        if(Mathf.Abs(wrapAngle) >= Math.PI * 2)
        {
            // 完整圆圈的情况
            if(wrapAngle > 0)
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
        else if(Mathf.Abs(wrapAngle) != 0)
        {
            // 部分弧线的情况
            var startAngle = this.radOnePointOnCircle(column.preTangentWorldPos, column.columnWorldPos);
            this.ms.circle(column.columnWorldPos, renderRadius, startAngle, startAngle + wrapAngle, wrapAngle < 0);
        }
    }
    float radOnePointOnCircle(Vector2 p,Vector2 pR){
        var ra=0.0f;
        var va=p-(pR);
        var vb=new Vector2(1,0);
        ra=Vector2.SignedAngle(vb,va)/180*Mathf.PI;
        
        return ra;
    }
    public Vector2 CalcQieDian(Vector2 ptCenter,Vector2 ptOutside,float dbRadious,Vector2 alignP)
    { 
        
        var E=new Vector2(0,0);
        var F=new Vector2(0,0);
        var G=new Vector2(0,0);
        var H=new Vector2(0,0);
        var r=dbRadious;
        //1. 坐标平移到圆心ptCenter处,求园外点的新坐标E
        E=ptOutside-ptCenter;//平移变换到E
        //2. 求园与OE的交点坐标F, 相当于E的缩放变换
        var t= r /E.magnitude;  //得到缩放比例
        F= E*t;   //缩放变换到F
        var a=0.0f;
        //3. 将E旋转变换角度a到切点G，其中cos(a)=r/OF=t, 所以a=arccos(t);
        if(this.radTwoPOnR(ptOutside,alignP,ptCenter)<0){
            a= -Mathf.Acos(t);   //得到旋转角度 角度为负
        }else{
            a= Mathf.Acos(t);   //得到旋转角度  角度为正
        }
        
        G=rotateVec2(F,a);//旋转变换到G
        
        //4. 将G平移到原来的坐标下得到新坐标H
         H=G+(ptCenter);           //平移变换到H

        //5. 返回H
        return H;
        //6. 实际应用过程中，只要一个中间变量E,其他F,G,H可以不用。
    }
    float ifOnlyColumn(GameObject c){
        float _out=0.0f;
        for(var i=1;i<this.columns.Count;i++){
            if(this.columns[this.columns.Count-1].column==c){
                _out++;
                break;
            }
        }
        return _out;
    }
    float radTwoPOnR(Vector2 pA,Vector2 pB,Vector2 pR){
        var ra=0.0f;
        var va=pA-pR;
        var vb=pB-pR;
        ra=Vector2.SignedAngle(vb,va)/180*Mathf.PI;
        
        return ra;
    }
    Vector3 v2Tov3(Vector2 v){
        return new Vector3(v.x,v.y,0);
    }
    public Vector2 rotateVec2(Vector2 v,float rad){
        Vector2 _out=new Vector2(v.x*Mathf.Cos(rad)+v.y*Mathf.Sin(rad),-v.x*Mathf.Sin(rad)+v.y*Mathf.Cos(rad));
        return _out;
    }
    
}
