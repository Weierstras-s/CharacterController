using UnityEngine;
using static Math;

public class Math {
    public static float CalcSlope(Vector2 vector) {
        //计算向量与竖直方向夹角(deg)
        return Mathf.Asin(-vector.x / vector.magnitude) * Mathf.Rad2Deg;
    }
    public static float Sin(float degree) { return Mathf.Sin(degree * Mathf.Deg2Rad); }
    public static float Cos(float degree) { return Mathf.Cos(degree * Mathf.Deg2Rad); }
    public static float Abs(float f) { return Mathf.Abs(f); }
    public static float Sign(float f) { return Mathf.Sign(f); }
    public static float Max(float a, float b) { return Mathf.Max(a, b); }
    public static float Min(float a, float b) { return Mathf.Min(a, b); }
}

public class Collider {
    public readonly GameObject gameObject;
    public readonly Vector2 point;
    public readonly Vector2 normal;
    public readonly float slope;
    public readonly int type;
    public Collider(RaycastHit2D hit) {
        gameObject = hit.collider.gameObject;
        point = hit.point;
        normal = hit.normal;
        slope = CalcSlope(hit.normal);
        Platform platform = gameObject.GetComponent<Platform>();
        if (platform != null) {
            type = platform.type;
        } else {
            type = 0;
        }
    }
}

public class CharacterController2D : MonoBehaviour {

    #region Inspector Properties

    //刚体属性
    [Header("Rigid Body")]
    public float leftBound = 0.5f;
    public float rightBound = 0.5f;
    public float lowerBound = 0.8f;
    public float upperBound = 0.8f;
    public float upperBoundCrouch = 0;
    public float skinWidth = 0.015f;

    //运动
    [Header("Kinematics")]
    [Range(0, 1)] public float acceleration = 0.25f; //水平加速度
    [Range(1, 90)] public float slopeLimit = 50f; //坡度上限
    [Range(1, 10)] public int jumpLimit = 2; //跳跃次数上限
    public bool turnWhileJumping = true; //能否跳跃中转向
    [Range(0, 1)] public float airFriction = 0.98f;

    //碰撞检测
    [Header("Collision")]
    [Range(2, 20)] public int horizontalRays = 5; //水平方向射线
    [Range(2, 20)] public int verticalRays = 5; //竖直方向射线

    //层次标记
    [Header("Layers")]
    public LayerMask playerMask = 1 << 8;
    public LayerMask platformMask = 1 << 9;
    public LayerMask defaultMask = 1 << 0;

    #endregion

    #region Hidden Properties

    [Header("Debug")]
    public bool isGrounded; //是否在地面上
    [HideInInspector] public bool isCrouching; //是否蹲下
    [HideInInspector] public bool ignorePlatform; //是否忽略单向平台
    [HideInInspector] public int jumpTime; //剩余跳跃次数
    [HideInInspector] public Vector2 velocity; //当前速度

    //碰撞检测
    private float L, R, U, D; //当前边界
    private const float deltaDistance = 0.03f; //地面判定范围
    [SerializeField] private float slope; //当前坡度

    //重力加速度
    public delegate float Gravity(float velocity, bool isJumping);
    public Gravity gravity = delegate (float velocity, bool isJumping) { return 20; };

    //斜面速度
    public delegate float SlopeVelocity(float slope);
    public SlopeVelocity slopeVelocity = delegate (float slope) { return 1; };

    #endregion

    #region Delegates

    //碰撞事件
    Collider hCollider = null;
    Collider vCollider = null;
    public delegate void OnCollision(Collider collider);
    public OnCollision onCollision = delegate (Collider collider) { };

    #endregion

    #region Collision Detector

    /// <summary> 绘制射线 </summary>
    private RaycastHit2D Raycast(Vector2 origin, Vector2 direction, float distance, LayerMask layerMask, Color color) {
        Debug.DrawRay(origin, direction.normalized * distance, color);
        return Physics2D.Raycast(origin, direction, distance, layerMask);
    }
    /// <summary> 绘制射线 </summary>
    private RaycastHit2D Raycast(Vector2 origin, Vector2 direction, float distance, LayerMask layerMask) {
        return Physics2D.Raycast(origin, direction, distance, layerMask);
    }

    /// <summary> 更新边界 </summary>
    private void UpdateBounds() {
        L = transform.position.x - leftBound;
        R = transform.position.x + rightBound;
        D = transform.position.y - lowerBound;
        U = transform.position.y + (isCrouching ? upperBoundCrouch : upperBound);
    }

    /// <summary> 水平方向运动 </summary>
    private void MoveX(ref Vector2 deltaPos) {
        UpdateBounds();
        float dir = Sign(deltaPos.x);

        //上方发生碰撞时维持爬行
        if (!isCrouching) {
            float oriy = transform.position.y + upperBoundCrouch;
            float dist = upperBound - upperBoundCrouch - skinWidth;
            RaycastHit2D hitL = Raycast(new Vector2(L + skinWidth, oriy), Vector2.up, dist, defaultMask);
            RaycastHit2D hitR = Raycast(new Vector2(R - skinWidth, oriy), Vector2.up, dist, defaultMask);
            if (hitL.collider != null || hitR.collider != null) {
                isCrouching = true;
            }
        }
        UpdateBounds();

        //斜面运动
        slope = Mathf.Infinity;
        if (isGrounded) {
            //检测地面角度
            RaycastHit2D hitL = Raycast(new Vector2(L, D), Vector2.down, skinWidth + deltaDistance, defaultMask | platformMask, Color.blue);
            RaycastHit2D hitR = Raycast(new Vector2(R, D), Vector2.down, skinWidth + deltaDistance, defaultMask | platformMask, Color.blue);
            if (dir < 0) {
                RaycastHit2D tmp;
                tmp = hitL; hitL = hitR; hitR = tmp;
            }
            //下坡吸附
            if (hitL.collider != null && hitL.normal.x * dir >= 0) {
                slope = CalcSlope(hitL.normal); Debug.DrawRay(hitL.point, hitL.normal, Color.blue);
                hCollider = new Collider(hitL); //水平方向碰撞
            }
            //上坡吸附
            if (hitR.collider != null && hitR.normal.x * dir <= 0) {
                slope = CalcSlope(hitR.normal); Debug.DrawRay(hitR.point, hitR.normal, Color.blue);
                hCollider = new Collider(hitR); //水平方向碰撞
            }
            //沿斜面移动
            if (Abs(slope) < slopeLimit) {
                deltaPos = new Vector2(Cos(slope), Sin(slope)) * deltaPos.magnitude * dir * slopeVelocity(slope * dir);
            }
            Debug.DrawRay(transform.position, deltaPos * 10, Color.yellow);
        }

        //墙
        for (int i = 1; i <= verticalRays + 1; i++) {
            float vPos = D + (U - D) * i / (verticalRays + 1);
            Vector2 ori = new Vector2(dir < 0 ? L : R, vPos);
            RaycastHit2D hit = Raycast(ori, deltaPos, deltaPos.magnitude + skinWidth, defaultMask, Color.red);
            if (hit.collider != null) {
                deltaPos = Min(deltaPos.magnitude, hit.distance - skinWidth) * deltaPos.normalized;
                vCollider = new Collider(hit); //竖直方向碰撞事件
            }
        }

        //防穿模
        Vector2 movePos = deltaPos;
        RaycastHit2D cut = Raycast(new Vector2(L, D), movePos, movePos.magnitude + skinWidth, defaultMask | platformMask, Color.green);
        if (cut.collider != null && !(dir > 0 && CalcSlope(cut.normal) > 0) && cut.normal.y > 0) {
            movePos = Min(movePos.magnitude, cut.distance - skinWidth) * movePos.normalized;
        }
        cut = Raycast(new Vector2(R, D), movePos, movePos.magnitude + skinWidth, defaultMask | platformMask, Color.green);
        if (cut.collider != null && !(dir < 0 && CalcSlope(cut.normal) < 0) && cut.normal.y > 0) {
            movePos = Min(movePos.magnitude, cut.distance - skinWidth) * movePos.normalized;
        }
        transform.Translate(movePos);
    }

    /// <summary> 竖直方向运动 </summary>
    private void MoveY(ref Vector2 deltaPos) {
        UpdateBounds();
        float dir = Sign(deltaPos.y);
        deltaPos.y *= dir;

        //离开地面时跳跃次数减少
        if (isGrounded) {
            isGrounded = false;
            jumpTime--;
        }

        bool ignorePlatformNext = false;
        for (int i = 0; i <= horizontalRays + 1; i++) {
            float hPos = L + (R - L) * i / (horizontalRays + 1);
            Vector2 ori = new Vector2(hPos, (dir < 0 ? D : U) - skinWidth * dir);

            //与路面碰撞
            RaycastHit2D hit = Raycast(ori, Vector2.up * dir, skinWidth * 2 + deltaDistance + deltaPos.y, defaultMask, Color.red);
            if (hit.collider != null) {
                deltaPos.y = Min(deltaPos.y, hit.distance - skinWidth * 2);
                if (hCollider == null) hCollider = new Collider(hit); //水平方向碰撞事件
                if (dir < 0) {
                    float slopev = CalcSlope(hit.normal);
                    if (Abs(slopev) >= slopeLimit) {
                        deltaPos.x -= Sign(slopev) * 0.025f;
                        if (vCollider == null) vCollider = new Collider(hit); //竖直方向碰撞事件
                    } else {
                        isGrounded = true;
                        jumpTime = jumpLimit;
                    }
                }
            }

            //与平台碰撞
            if (dir < 0) {
                hit = Raycast(ori, Vector2.up * dir, skinWidth * 2 + deltaDistance + deltaPos.y, platformMask, Color.red);
                if (hit.collider != null) {
                    float slopev = CalcSlope(hit.normal);
                    Platform platform = hit.collider.GetComponent<Platform>();
                    bool goDown = true;
                    if (platform != null && platform.goDown == false) goDown = false;
                    if (slopev > 0 && i > horizontalRays || slopev < 0 && i < 1 || slopev == 0) {
                        if (!ignorePlatform || !goDown) {
                            deltaPos.y = Min(deltaPos.y, hit.distance - skinWidth * 2);
                            isGrounded = true;
                            jumpTime = jumpLimit;
                            if (hCollider == null) hCollider = new Collider(hit); //水平方向碰撞事件
                        } else ignorePlatformNext = true;
                    }
                }
            }
        }
        ignorePlatform = ignorePlatformNext;
        if (deltaPos.y < 0) {
            transform.Translate(new Vector2(0, deltaPos.y * dir));
            deltaPos.y = 0;
        }
        deltaPos.y *= dir;
        transform.Translate(deltaPos);
    }

    #endregion

    #region Public Methods

    /// <summary> 尝试向指定方向移动 </summary>
    public void Move(Vector2 curVelocity, bool isJumping) {
        hCollider = null;
        vCollider = null;
        //当前速度
        if (turnWhileJumping || isGrounded) {
            curVelocity.x = velocity.x + (curVelocity.x - velocity.x) * acceleration;
        } else {
            curVelocity.x = velocity.x * airFriction;
        }
        curVelocity.y = velocity.y - gravity(velocity.y, isJumping) * Time.deltaTime;
        //当前位移
        Vector2 deltaPos = curVelocity * Time.deltaTime;
        Vector2 deltaX = new Vector2(deltaPos.x, 0);
        Vector2 deltaY = new Vector2(0, deltaPos.y);
        //横向移动
        MoveX(ref deltaX);
        //纵向移动
        MoveY(ref deltaY);
        //更新速度
        velocity.x = deltaX.x + deltaY.x;
        velocity.y = deltaY.y;
        velocity /= Time.deltaTime;
        //添加委托
        if (hCollider != null) onCollision(hCollider);
        if (vCollider != null) onCollision(vCollider);
        if (hCollider == null && vCollider == null) {
            onCollision(null);
        }
    }

    #endregion

    #region API

    private void Awake() {
        isGrounded = false;
        isCrouching = false;
        ignorePlatform = false;
        jumpTime = 0;
        velocity = new Vector2(0, 0);
    }

    #endregion

}
