using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;


public class grapple_script : MonoBehaviour
{

    public LayerMask groundLayer;

    private LineRenderer lineRenderer;
    private BoxCollider2D playerCollider;
    private Rigidbody2D rb;
    Vector2 shootDir = new(1,1);
    Vector2 hitPoint;
    float range = 5f;
    float side= 0, prevSide = 0;
    public static bool isGrappling = false;

    // Start is called before the first frame update
    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        playerCollider = GetComponent<BoxCollider2D>();
        rb = GetComponent<Rigidbody2D>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Mouse0))
        {
            CastLine();
        }

        if (Input.GetKeyUp(KeyCode.Mouse0))
        {
            ReelLine();
        }
    }

    void FixedUpdate()
    {
        if (!isGrappling) return;
        // Swing();
        ZipToPoint();
        rb.velocity = PlayerController.velocity;
        UpdateLine();
    }

    void ZipToPoint() {
        Vector2 zipDir = hitPoint - (Vector2)transform.position;
        float dist = zipDir.magnitude;
        zipDir.Normalize();
        Vector2 zipTangent = Vector2.Perpendicular(hitPoint-(Vector2)transform.position).normalized;
        
        // if (dist <= 0.5f) {
        //     Swing();
        //     return;
        // }
        PlayerController.velocity += (25f * dist * zipDir + 
            0.5f*Vector2.Dot(PlayerController.velocity, zipDir) * zipTangent) 
            * Time.fixedDeltaTime;
        
        // PlayerController.velocity = Vector2.ClampMagnitude(PlayerController.velocity, 100f);
    }

    void Swing() {
        // -1 if on left, 1 if on right
        prevSide = side == 0 ? prevSide : side;
        side = Math.Sign(transform.position.x-hitPoint.x);
        if (side == 0) side = prevSide;

        Vector2 tangentDir = side*Vector2.Perpendicular(hitPoint-(Vector2)transform.position).normalized;
        
        float speed = 0.99f*Vector2.Dot(PlayerController.velocity, tangentDir);

        PlayerController.velocity = speed * tangentDir;
    }

    void UpdateLine(){
        lineRenderer.SetPosition(0, transform.position);
        lineRenderer.SetPosition(1, hitPoint);
    }

    void CastLine() {
        Debug.DrawRay(transform.position, shootDir * range, Color.red, 1f);

        RaycastHit2D hit = 
            Physics2D.Raycast(transform.position, shootDir, range, groundLayer);
        
        if (hit.collider != null) {
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, hit.point);
            hitPoint = hit.point;
            rb.velocity = Vector2.zero;
            lineRenderer.enabled = true;
            isGrappling = true;
        }
    }

    void ReelLine() {
        lineRenderer.enabled = false;
        isGrappling = false;
    }
}
