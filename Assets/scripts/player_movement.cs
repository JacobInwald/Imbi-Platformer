using System.Collections;
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

    // Movement
    public static Vector2 velocity = new(0, 0);
    float horizontal = 0f;
    
    // Sliding
    bool sliding = false, slide = false;
    float landTime = 0.1f, landTimer = 0f;
    Vector2 landingVel = Vector2.zero;

    // Memory
    bool groundedLastFrame = false;

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
    bool scaleChangin = false;
    
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
        grounded = IsOnGround();
        walled = IsOnWall();

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
        jumpHeld = (!grounded || jumpBufferTimer > 0f) && Input.GetButton("Jump");

        // play correct animations while moving...
        if (grounded && horizontal.Equals(0))
            GetComponent<Animator>().Play("Imbi-Idle");
        else if (grounded && (horizontal > 0 || horizontal < 0))
            GetComponent<Animator>().Play("Imbi-Walk");
        else if (!grounded && groundedLastFrame)
            StartCoroutine(JumpAnim(transform.localScale));
        else if (velocity.y < -0.1f && !walled)
            GetComponent<Animator>().Play("Imbi-Fall");
        if (grounded && !groundedLastFrame) {
            StartCoroutine(LandAnim(transform.localScale));
        }

    }
 
    void FixedUpdate() {
        if (grapple_script.isGrappling){ 
            Gravity();
            return;
        }
        // ! Pre-Update
        velocity = rigidBody2D.velocity;
        float horIncr;
        // ! Horizontal movement
        horIncr = horizontal * acceleration * 10f *
                        ((wallJumpTimer > 0) ? wallJumpAccelMult : 1f) * 
                        ((Mathf.Abs(velocity.y) < 0.75f && Mathf.Abs(velocity.y) > 0.005f) ? antiGravAccelMult : 1f);
        
        if (Mathf.Abs(velocity.x) > speed) 
            horIncr = -1* Mathf.Sign(velocity.x)*(Mathf.Abs(velocity.x)-speed+0.01f) * acceleration;
        else if (Mathf.Abs(velocity.x + horizontal * Time.fixedDeltaTime) >= speed)
            horIncr = -1* Mathf.Sign(velocity.x)*(Mathf.Abs(velocity.x)-speed+0.01f) * acceleration*3f;

        if (!sliding)   
            velocity += horIncr * Time.fixedDeltaTime * Vector2.right;
        else if (sliding && landTimer > 0f) 
            velocity = landingVel.x / speed * landingVel.magnitude * Vector2.right;

        Friction();

        // Animations
        if (wallJumpTimer <= 0){
            if (horizontal > 0     && !isFacingRight)    FlipSprite();
            else if(horizontal < 0 && isFacingRight)    FlipSprite();
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
        velocity.x = Mathf.Clamp(velocity.x, -speed*1.5f, speed*1.5f);
        velocity.x = Mathf.Abs(velocity.x) < 0.01f ? 0f : velocity.x;
        velocity.y = Mathf.Abs(velocity.y) < 0.01f ? 0f : velocity.y;

        // Corner Correction
        CornerCorrect(cornerCorrection);

        rigidBody2D.velocity = velocity;

        if (debug) {
            lineColor = velocity.y <= -terminalVelocity ? Color.red : lineColor;
            Debug.DrawLine(lastPos, transform.position, lineColor, 1.0f);
            lastPos = transform.position;
            lineColor = Color.white;
        }
    }

    IEnumerator LandAnim(Vector3 startScale) {
        yield return StartCoroutine(Squish(startScale, 0.25f));
        // yield return StartCoroutine(Squeeze(startScale, 0.01f));
    }

    IEnumerator JumpAnim(Vector3 startScale) {
        // yield return StartCoroutine(Squish(startScale, 0.1f));
        // Can jump here
        yield return StartCoroutine(Squeeze(startScale, 0.4f)); 
    }

    IEnumerator Squish(Vector3 startScale, float duration = 0.25f) {
        if (!scaleChangin) {
            scaleChangin = true; 
            scaleModifier = startScale;
            yield return StartCoroutine(ScaleLerp(new Vector3(1.2f, 0.8f, 1.0f), 0.5f*duration));
            yield return StartCoroutine(ScaleLerp(scaleModifier, 0.1f*duration));
            yield return StartCoroutine(ScaleLerp(startScale, 0.4f*duration));
            if (isFacingRight != transform.localScale.x > 0)
                transform.localScale = new Vector3(-transform.localScale.x,
                                                    transform.localScale.y,
                                                     transform.localScale.z);
            scaleChangin = false;
        }
    }

    IEnumerator Squeeze(Vector3 startScale, float duration = 0.25f) {
        if (!scaleChangin) {
            scaleChangin = true; 
            scaleModifier = startScale;

            yield return StartCoroutine(ScaleLerp(new Vector3(0.8f, 1.2f, 1.0f), 0.5f*duration));
            yield return StartCoroutine(ScaleLerp(scaleModifier, 0.1f*duration));
            yield return StartCoroutine(ScaleLerp(startScale, 0.4f*duration));
            if (isFacingRight != transform.localScale.x > 0)
                transform.localScale = new Vector3(-transform.localScale.x,
                                                    transform.localScale.y,
                                                     transform.localScale.z);
            scaleChangin = false;
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
    private void FlipSprite() {
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

        if (grounded && !groundedLastFrame) {landTimer = landTime;  landingVel = velocity;}
        else if (landTimer > 0f)    landTimer -= Time.deltaTime;
        else if (landTimer <= 0f)   landTimer = 0f;
        
    }


    // Wall Sliding
    private void WallSlide() {
        if (wallSlide) {
            velocity.y = velocity.y > 0.05f ? velocity.y * 0.9f : velocity.y;
            velocity.y = Mathf.Clamp(velocity.y, -1.5f, terminalVelocity);
        }

        if (wallJump) {
                velocity.y = wallJumpPower.y;
                velocity.x = isFacingRight ? -wallJumpPower.x : wallJumpPower.x;
                wallJump = false;
                jumpBufferTimer = 0f;
                coyoteTimer = 0f;
                wallJumpTimer = wallJumpTime;
                FlipSprite();
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
        velocity += gMult * SystemConstants.Gravity * Time.fixedDeltaTime * Vector2.down;
    }

    // Friction
    private void Friction() {
        if      (horizontal == 0f && grounded)  velocity.x *= 1-groundedFriction;
        else if (horizontal == 0f && !grounded 
                 && (wallJumpTimer <= 0f)) velocity.x *= 1-airFriction;
        else if (sliding)                       velocity.x *= 1-slideFriction; 
    }

    // Corner Correction
    private void CornerCorrect(int pixels) {
        attempt_correction_x(pixels);
        attempt_correction_y(pixels);
    }

    // Corner Correction on Head Bumps
    private void attempt_correction_y(int pixels) {
        var up = Vector2.up * velocity * Time.fixedDeltaTime;

        if (velocity.y > 0 && TestMove(up)) {
            for (float j = -1.0f; j <= 1.0f; j += 2.0f) {
                for (int i = 1; i <= pixels; i++) {
                    if (!TestMove(new Vector2(i*j/100, 0), up)) {
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

        if (TestMove(hor)) {
            for (float i = 1; i <= pixels; i++) {
                if (!TestMove(new Vector2(0, i/100), hor)) {
                    transform.position += new Vector3(0,i/100);
                    if (velocity.y < 0) velocity.y = 0;
                    return;
                }
            }
        }
    }

    // Test move
    private bool TestMove(Vector2 t, Vector2 dir) {
        // Debugging
        if (debug) {
            Debug.DrawRay((Vector2) playerCollider.bounds.center + t + 
                                    playerCollider.bounds.extents * (dir.normalized + (dir.x == 0 ? Vector2.left: Vector2.up)),
                                    dir,
                                    Color.red,
                                    1.0f);
            Debug.DrawRay((Vector2) playerCollider.bounds.center + t + 
                                    playerCollider.bounds.extents * (dir.normalized + (dir.x == 0 ? Vector2.right: Vector2.down)),
                                    dir,
                                    Color.red,
                                    1.0f);
        }
        // Check if left side collides
        return !(Physics2D.Raycast((Vector2) playerCollider.bounds.center + t + 
                                playerCollider.bounds.extents * (dir.normalized + (dir.x == 0 ? Vector2.left: Vector2.up)),
                                        dir,
                                        0.08f, 
                                        groundLayer).collider == null
                &&
        //Center
                Physics2D.Raycast((Vector2) playerCollider.bounds.center + t + 
                                playerCollider.bounds.extents * dir.normalized,
                                        dir,
                                        0.08f, 
                                        groundLayer).collider == null
                &&
        // Check if right side collides
                Physics2D.Raycast((Vector2) playerCollider.bounds.center + t + 
                                playerCollider.bounds.extents * (dir.normalized + (dir.x == 0 ? Vector2.right: Vector2.down)),
                                        dir,
                                        0.08f, 
                                        groundLayer).collider == null);
    }

    private bool TestMove(Vector2 dir) {
        // Check if left side collides
        return !(Physics2D.Raycast((Vector2) playerCollider.bounds.center + 
                                playerCollider.bounds.extents * (dir.normalized + (dir.x == 0 ? Vector2.left: Vector2.up)),
                                        dir,
                                        0.08f, 
                                        groundLayer).collider == null
                &&
        //Center
                Physics2D.Raycast((Vector2) playerCollider.bounds.center + 
                                playerCollider.bounds.extents * dir.normalized,
                                        dir,
                                        0.08f, 
                                        groundLayer).collider == null
                &&
        // Check if right side collides
                Physics2D.Raycast((Vector2) playerCollider.bounds.center  + 
                                playerCollider.bounds.extents * (dir.normalized + (dir.x == 0 ? Vector2.right: Vector2.down)),
                                        dir,
                                        0.08f, 
                                        groundLayer).collider == null);
    }

    // Collision Checks
    private bool IsOnGround() {
        return groundCheck.IsTouchingLayers(groundLayer);
    }

    private bool IsOnWall() {
        return wallCheck.IsTouchingLayers(groundLayer);
    }

}