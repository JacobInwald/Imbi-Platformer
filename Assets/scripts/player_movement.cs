using System.Collections;
using System.Collections.Generic;
using UnityEngine;
 
public class PlayerController : MonoBehaviour
{

    // Collision Checks
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private BoxCollider2D wallCheck;
    [SerializeField] private BoxCollider2D groundCheck;

    // Normal Movement
    [Range(0, 20f)] [SerializeField] private float speed = 5.0f;
    [Range(0, 20f)] [SerializeField] private float jumpvel = 5.0f;
    [Range(0, 20f)] [SerializeField] private float acceleration = 5.0f;
    [Range(0, 20f)] [SerializeField] private float terminalVelocity = 5.0f;

    // Friction
    [Range(0, 1f)] [SerializeField] private float groundedFriction = 0.5f;
    [Range(0, 1f)] [SerializeField] private float airFriction = 0.1f;
    [Range(0, 1f)] [SerializeField] private float slideFriction = 0.1f;

    // Gravity Multipliers
    [Range(0, 5f)] [SerializeField] private float tallJumpMult = 0.85f;
    [Range(0, 5f)] [SerializeField] private float shortJumpMult = 1.7f;
    [Range(0, 5f)] [SerializeField] private float fallMult = 1.7f;
    [Range(0, 5f)] [SerializeField] private float apexMult = 1.7f;
    // Corner Correction
    [Range(0, 100)] [SerializeField] private int cornerCorrection = 8;
    
    // Collision Checks
    Rigidbody2D rigidBody2D;
    BoxCollider2D playerCollider;
    RaycastHit2D[] results = new RaycastHit2D[100];

    // Movement
    Vector2 velocity = new Vector2(0, 0);
    float horizontal = 0f;
    
    // Sliding
    bool sliding = false, slide = false;

    // Memory
    bool groundedLastFrame = false;
    Vector2 prevVelocity = Vector2.zero;

    // Speedy Apex
    float antiGravAccelMult = 1.1f;

    // Jumping 
    bool isFacingRight = true;
    bool jump = false, jumpHeld = false, grounded = false;
    float gMult = 1f;

    // Coyote Time
    float coyoteTime = 0.1f;
    float coyoteTimer = 0f;
    
    // Jump Buffering
    float jumpBufferTime = 0.075f;
    float jumpBufferTimer = 0f;
    
    // Wall Jumping
    bool wallJump = false, walled = false, wallSlide = false;
    float wallJumpTime = 0.25f, wallJumpTimer = 0f;
    Vector2 wallJumpPower;
    float wallJumpAccelMult = 0.15f;

    // Debugging
    Vector3 lastPos = Vector3.zero;
    Color lineColor = Color.white;
    bool debug = true;

    // Scale Lerping
    Vector3 scaleModifier = new Vector3(1, 1, 1);
    bool squishing = false;
    
    void Start()
    {
        rigidBody2D = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<BoxCollider2D>();
        coyoteTimer = coyoteTime;
        lineColor = Color.white;
        lastPos = transform.position;
        wallJumpPower =  new Vector2(speed, jumpvel);
        debug = true;
    }
 
    void Update() {
        
        horizontal = Input.GetAxisRaw("Horizontal");
        groundedLastFrame = grounded;
        grounded = isOnGround();
        walled = isOnWall();

        // Timers
        TimerUpdate();

        // Sliding
        if (grounded && Input.GetButtonDown("Slide") && !sliding) slide = true;
        sliding = grounded && Input.GetButton("Slide");

        // Wall Juping
        if ((Input.GetButtonDown("Jump") || jumpBufferTimer > 0) 
                && walled && !grounded) 
                wallJump = true;
        wallSlide = walled && !grounded && velocity.x != 0f;


        // Jump Checking
        if ((coyoteTimer > 0 || grounded) && Input.GetButtonDown("Jump") 
            || (grounded && jumpBufferTimer > 0)) 
            jump = true;
        
        // Early Jump Release
        jumpHeld = ((!grounded || jumpBufferTimer > 0f) && Input.GetButton("Jump"));

        // play correct animations while moving...
        if (grounded && horizontal.Equals(0))
            GetComponent<Animator>().Play("Imbi-Idle");
        else if (grounded && (horizontal > 0 || horizontal < 0))
            GetComponent<Animator>().Play("Imbi-Walk");
        else if (!grounded && groundedLastFrame)
            GetComponent<Animator>().Play("Imbi-Squeeze");
        else if (velocity.y < -0.1f)
            GetComponent<Animator>().Play("Imbi-Fall");
        if (grounded && !groundedLastFrame) {
            StartCoroutine(Squash(transform.localScale));
        }

    }
 
    void FixedUpdate() {
        // ! Pre-Update
        velocity = rigidBody2D.velocity;

        // ! Horizontal movement
        horizontal *= acceleration * 
                        ((wallJumpTimer > 0) ? wallJumpAccelMult : 1f) * 
                        ((Mathf.Abs(velocity.y) < 0.75f && Mathf.Abs(velocity.y) > 0.005f) ? antiGravAccelMult : 1f);

        if (wallJumpTimer <= 0){
            if (horizontal > 0     && !isFacingRight)    flipSprite();
            else if(horizontal < 0 && isFacingRight)    flipSprite();
        }

        if (!sliding){
            velocity.x = Mathf.Clamp(velocity.x + horizontal * Time.fixedDeltaTime * 10f, -speed, speed);
        }

        Friction();

        // Slide
        if (slide || (sliding && grounded && !groundedLastFrame)) {
            velocity = prevVelocity.magnitude * Vector2.right * (prevVelocity.x / speed);
            slide = false;
        }


        // ! Vertical Movement

        // Jumping
        if (jump) {
            velocity.y = jumpvel;
            jump = false;
            coyoteTimer = 0f;
            jumpBufferTimer = 0f;
        }

        // Gravity
        Gravity();

        // Wall Sliding
        WallSlide();

        // ! Post-Update

        // Movement Clamping
        velocity.y = Mathf.Clamp(velocity.y, -terminalVelocity, terminalVelocity);
        velocity.x = Mathf.Abs(velocity.x) < 0.01f ? 0f : velocity.x;
        velocity.y = Mathf.Abs(velocity.y) < 0.01f ? 0f : velocity.y;

        // Corner Correction
        CornerCorrect(cornerCorrection);

        rigidBody2D.velocity = velocity;

        prevVelocity = velocity;

        if (debug) {
            lineColor = velocity.y <= -terminalVelocity ? Color.red : lineColor;
            Debug.DrawLine(lastPos, transform.position, lineColor, 1.0f);
            lastPos = transform.position;
            lineColor = Color.white;
        }
    }

    IEnumerator Squash(Vector3 startScale) {
        if (!squishing) {
            squishing = true; 
            scaleModifier = startScale;
            yield return StartCoroutine(ScaleLerp(new Vector3(1.2f, 0.8f, 1.0f), 0.15f));
            yield return StartCoroutine(ScaleLerp(scaleModifier, 0.025f));
            yield return StartCoroutine(ScaleLerp(startScale, 0.1f));
            if (isFacingRight != transform.localScale.x > 0)
                transform.localScale = new Vector3(-transform.localScale.x,
                                                    transform.localScale.y,
                                                     transform.localScale.z);
            squishing = false;
        }
    }

    IEnumerator ScaleLerp(Vector3 endValue, float duration)
    {
        float time = 0;
        Vector3 startValue = scaleModifier;
        endValue.x *= Mathf.Sign(endValue.x) != Mathf.Sign(transform.localScale.x) ? -1 : 1;
        
        // Squish 
        while (time < duration)
        {
            scaleModifier.x = Mathf.Lerp(startValue.x, endValue.x, time / duration);
            scaleModifier.y = Mathf.Lerp(startValue.y, endValue.y, time / duration);

            transform.localScale = scaleModifier;
            time += Time.deltaTime;

            yield return null;
        }

        transform.localScale = endValue;
        scaleModifier = endValue;
    }

    // Flip Sprite
    private void flipSprite() {
        isFacingRight = !isFacingRight;
 
        Vector3 transformScale = transform.localScale;
        transformScale.x *= -1;
        transform.localScale = transformScale;
    }

    // Timers
    private void TimerUpdate() {
        // ! Jump Buffer Timer
        if (!grounded && Input.GetButtonDown("Jump"))   jumpBufferTimer = jumpBufferTime;
        else if (jumpBufferTimer > 0f && !grounded)  {jumpBufferTimer -= Time.deltaTime; lineColor = Color.magenta;}
        
        // ! Coyote Time
        if (grounded)                   coyoteTimer = coyoteTime;   
        else if (coyoteTimer > 0f)  {   coyoteTimer -= Time.deltaTime; lineColor = Color.green; }

        if (wallJumpTimer > 0f)    wallJumpTimer -= Time.deltaTime;
        else if (wallJumpTimer <= 0f || grounded)   wallJumpTimer = 0f;
        
    }


    // Wall Sliding
    private void WallSlide() {
        if (wallSlide) {
            velocity.y = velocity.y > 0.05f ? velocity.y * 0.9f : velocity.y;
            velocity.y = Mathf.Clamp(velocity.y, -2f, terminalVelocity);
        }

        if (wallJump) {
                velocity.y = wallJumpPower.y;
                velocity.x = isFacingRight ? -wallJumpPower.x : wallJumpPower.x;
                wallJump = false;
                jumpBufferTimer = 0f;
                coyoteTimer = 0f;
                wallJumpTimer = wallJumpTime;
                flipSprite();
        }
    }

    // Gravity
    private void Gravity() {
        gMult = fallMult;

        // Jump height control 
        if(jumpHeld && velocity.y > 0)          gMult = tallJumpMult;
        else if(!jumpHeld && velocity.y > 0)    gMult = shortJumpMult;
        
        // Anti-Gravity Apex
        if (Mathf.Abs(velocity.y) < 0.55f && Mathf.Abs(velocity.y) > 0.005f)   {  
            gMult = apexMult;
            lineColor = Color.blue; 
        }

        // Gravity...
        velocity += Vector2.down * SystemConstants.Gravity * gMult * Time.fixedDeltaTime;
    }

    // Friction
    private void Friction() {
        if      (horizontal == 0f && grounded)  velocity.x *= (1-groundedFriction);
        else if (horizontal == 0f && !grounded 
                 && (wallJumpTimer <= 0f)) velocity.x *= (1-airFriction);
        else if (sliding)                       velocity.x *= (1-slideFriction); 
    }

    // Corner Correction
    private void CornerCorrect(int pixels) {
        attempt_correction_x(pixels);
        attempt_correction_y(pixels);
    }

    // Corner Correction on Head Bumps
    private void attempt_correction_y(int pixels) {
        var up = Vector2.up * velocity * Time.fixedDeltaTime;

        if (velocity.y > 0 && testMove(up)) {
            for (float j = -1.0f; j <= 1.0f; j += 2.0f) {
                for (int i = 1; i <= pixels; i++) {
                    if (!testMove(new Vector2(i*j/100, 0), up)) {
                        transform.position += new Vector3(i*j/100, 0);
                        if (velocity.x < 0) velocity.x = 0;
                        return;
                    }
                }
            }

        }
    }

    // Corner Correction on Ledge Misses
    private void attempt_correction_x(int pixels) {
        var hor = Vector2.right * velocity * Time.fixedDeltaTime;

        if (testMove(hor)) {
            for (float i = 1; i <= pixels; i++) {
                if (!testMove(new Vector2(0, i/100), hor)) {
                    transform.position += new Vector3(0,i/100);
                    if (velocity.y < 0) velocity.y = 0;
                    return;
                }
            }
        }
    }

    // Test move
    private bool testMove(Vector2 t, Vector2 dir) {
        // Debugging
        if (debug) {
            Debug.DrawRay((Vector2) (playerCollider.bounds.center) + t + 
                                    playerCollider.bounds.extents * (dir.normalized + (dir.x == 0 ? Vector2.left: Vector2.up)),
                                    dir,
                                    Color.red,
                                    1.0f);
            Debug.DrawRay((Vector2) (playerCollider.bounds.center) + t + 
                                    playerCollider.bounds.extents * (dir.normalized + (dir.x == 0 ? Vector2.right: Vector2.down)),
                                    dir,
                                    Color.red,
                                    1.0f);
        }
        // Check if left side collides
        return !(Physics2D.Raycast((Vector2) (playerCollider.bounds.center) + t + 
                                playerCollider.bounds.extents * (dir.normalized + (dir.x == 0 ? Vector2.left: Vector2.up)),
                                        dir,
                                        0.08f, 
                                        groundLayer).collider == null
                &&
        //Center
                Physics2D.Raycast((Vector2) (playerCollider.bounds.center) + t + 
                                playerCollider.bounds.extents * (dir.normalized),
                                        dir,
                                        0.08f, 
                                        groundLayer).collider == null
                &&
        // Check if right side collides
                Physics2D.Raycast((Vector2) (playerCollider.bounds.center) + t + 
                                playerCollider.bounds.extents * (dir.normalized + (dir.x == 0 ? Vector2.right: Vector2.down)),
                                        dir,
                                        0.08f, 
                                        groundLayer).collider == null);
    }

    private bool testMove(Vector2 dir) {
        // Check if left side collides
        return !(Physics2D.Raycast((Vector2) (playerCollider.bounds.center) + 
                                playerCollider.bounds.extents * (dir.normalized + (dir.x == 0 ? Vector2.left: Vector2.up)),
                                        dir,
                                        0.08f, 
                                        groundLayer).collider == null
                &&
        //Center
                Physics2D.Raycast((Vector2) (playerCollider.bounds.center) + 
                                playerCollider.bounds.extents * (dir.normalized),
                                        dir,
                                        0.08f, 
                                        groundLayer).collider == null
                &&
        // Check if right side collides
                Physics2D.Raycast((Vector2) (playerCollider.bounds.center)  + 
                                playerCollider.bounds.extents * (dir.normalized + (dir.x == 0 ? Vector2.right: Vector2.down)),
                                        dir,
                                        0.08f, 
                                        groundLayer).collider == null);
    }

    // Collision Checks
    private bool isOnGround() {
        return groundCheck.IsTouchingLayers(groundLayer);
    }

    private bool isOnWall() {
        return wallCheck.IsTouchingLayers(groundLayer);
    }

}