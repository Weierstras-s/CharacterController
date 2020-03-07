using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.Mathf;

public class Player : MonoBehaviour {

	#region Inspector Properties

    [Header("Debug")]
    [Range(-1, 1)] public int debugVel = 0;

    [Header("Gravity")]
    public float gravity = 20f;
    public float jumpVelocitySmall = -1f;
    public float deltaGravity = 25f;
    public float lowGravity = 10f;
    public float lowGravityAreaMax = 1f;
    public float lowGravityAreaMin = -1f;
    public float fallingVelocity = -12f; //下降速度
    public float decayVelocity = -9f;

    [Header("Kinematics")]
    public float jumpVelocity = 10f; //跳跃速度
    public float maxVelocity = 8f; //速度
    public float maxVelocityCrouching = 4f;

    [Header("Keys")]
    public KeyCode keyLeft = KeyCode.LeftArrow;
    public KeyCode keyRight = KeyCode.RightArrow;
    public KeyCode keyUp = KeyCode.UpArrow;
    public KeyCode keyDown = KeyCode.DownArrow;
    public KeyCode keyJump = KeyCode.UpArrow;
    public KeyCode keyCrouch = KeyCode.LeftShift;

	#endregion

	#region Functions

	private float F1(float x) {
        if (x > lowGravityAreaMin && x < lowGravityAreaMax) return lowGravity;
        if (x > decayVelocity) return gravity;
        if (x < fallingVelocity) return 0;
        return gravity * (x - fallingVelocity) / (decayVelocity - fallingVelocity);
    }
    private float F2(float x, bool jump) {
        if (jump || x < jumpVelocitySmall) return 0;
        return deltaGravity;
    }
    public float Gravity(float vy, bool jump) {
        return F1(vy) + F2(vy, jump);
    }
    private float SlopeVelocity(float slope) {
        //slope: 0f - 90f
        if (slope <= 0) return 1 - slope / 100;
        else if (slope < 30) return 1;
        else return 1 - (slope - 30) / 100;
    }

    #endregion

    CharacterController2D self;
    Animator anim;
    private Vector2 velocity = new Vector2();
    private Vector2 moveVelocity = new Vector2();
    private Vector2 checkPoint = new Vector2();

    //常熟备份
    private Vector3 scale;
    private float oriAcceleration;
    private void OnCollision(Collider collider) {
        if(collider!=null) Debug.DrawRay(collider.point, collider.normal, Color.white);
        moveVelocity.x = 0;
        if (collider == null || collider.type == 0) {
            self.acceleration = oriAcceleration;
            return;
        }
        switch(collider.type) {
            case 1:
                //传送带
                moveVelocity.x = -5f;
                break;
            case 2:
                //摩擦力减少
                self.acceleration = 0.0f;
                break;
            case 3:
                //弹性路面
                if (Abs(collider.slope) < 89) break;
                self.velocity.x = -velocity.x * 28;
                break;
            case 4:
                //存档
                checkPoint = collider.gameObject.transform.position;
                break;
        }
    }


    #region API
    
    private void Start() {
        self = GetComponent<CharacterController2D>();
        anim = GetComponent<Animator>();
        anim.Play(Animator.StringToHash("Idle"));
        self.gravity = Gravity;
        self.slopeVelocity = SlopeVelocity;
        self.onCollision = OnCollision;
        //常数备份
        scale = transform.localScale;
        oriAcceleration = self.acceleration;
    }

    private void Update() {
        //更新速度
        Vector2 inputVelocity = new Vector2();
        if (Input.GetKey(keyRight)) inputVelocity.x++;
        if (Input.GetKey(keyLeft)) inputVelocity.x--;
        inputVelocity.x *= self.isCrouching ? maxVelocityCrouching : maxVelocity;
        moveVelocity += inputVelocity;

        moveVelocity.x += maxVelocity * debugVel; //Debug

        //动画控制1
        int animHash;
        if (inputVelocity.x > 0) transform.localScale = scale;
        else if (inputVelocity.x < 0) transform.localScale = Vector2.Scale(scale, new Vector3(-1, 1, 1));
        if (self.isGrounded) {
            if (inputVelocity.x != 0) animHash = Animator.StringToHash("Run");
            else animHash = Animator.StringToHash("Idle");
        } else {
            if (self.velocity.y > 0) animHash = Animator.StringToHash("Jump1");
            else animHash = Animator.StringToHash("Jump2");
        }


        //跳跃
        if (Input.GetKeyDown(keyJump) && self.jumpTime > 0 && !self.isCrouching) {
            self.velocity.y = jumpVelocity;
            self.isGrounded = false;
            self.jumpTime--;
        }
        //下平台
        if (Input.GetKey(keyDown) && self.isGrounded) {
            self.ignorePlatform = true;
        }
        //蹲下
        self.isCrouching = Input.GetKey(keyCrouch);

        //移动
        self.Move(moveVelocity, Input.GetKey(keyJump));
        velocity = self.velocity;
        if (self.isGrounded) {
            Debug.DrawRay(transform.position, Vector2.down, Color.white);
        }

        //动画控制2
        if (self.isCrouching) animHash = Animator.StringToHash("Crouch");
        //播放动画
        anim.Play(animHash);

        //重新开始
        if(Input.GetKeyDown(KeyCode.R)){
            self.velocity = new Vector2(0, 0);
            transform.position = new Vector3(checkPoint.x, checkPoint.y + 2, -5);
        }
    }

    #endregion

}
