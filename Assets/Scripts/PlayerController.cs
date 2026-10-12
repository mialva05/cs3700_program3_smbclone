using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 6f;
    public float runSpeed = 10f;
    public float acceleration = 45f;
    public float deceleration = 60f;

    [Header("Jump")]
    public float jumpVelocity = 16f;
    public float gravityScale = 3.5f;
    public float fallGravityMultiplier = 1.4f;
    public float maxFallSpeed = 22f;
    [Range(0f, 1f)] public float jumpCutMultiplier = 0.5f;
    public float coyoteTime = 0.1f;
    public float jumpBufferTime = 0.1f;
    public float stompBounceVelocity = 11f;

    [Header("Ground Check")]
    public LayerMask groundLayer;

    [Header("Sprites (assign your textures)")]
    public Sprite smallIdle;
    public Sprite smallJump;
    public Sprite bigIdle;
    public Sprite bigJump;

    [Header("Collider Shapes (feet at transform origin)")]
    public Vector2 smallSize = new Vector2(0.8f, 0.9f);
    public Vector2 smallOffset = new Vector2(0f, 0.45f);
    public Vector2 bigSize = new Vector2(0.8f, 1.9f);
    public Vector2 bigOffset = new Vector2(0f, 0.95f);

    Rigidbody2D rb;
    BoxCollider2D col;
    SpriteRenderer sr;

    float inputX;
    bool runHeld;
    bool jumpPressed;
    bool jumpReleased;

    float coyoteCounter;
    float jumpBufferCounter;

    bool grounded;
    bool isBig;
    bool isDead;
    bool isInvincible;

    public bool IsBig => isBig;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<BoxCollider2D>();
        sr = GetComponent<SpriteRenderer>();
        rb.freezeRotation = true;
        rb.gravityScale = gravityScale;
        ApplyForm();
    }

    void Update()
    {
        if (isDead) return;

        ReadInput();

        if (transform.position.y < -12f)
        {
            Die(true);
            return;
        }

        // Coyote time: still allowed to jump for a moment after leaving a ledge.
        coyoteCounter = grounded ? coyoteTime : coyoteCounter - Time.deltaTime;

        // Jump buffer: a jump press just before landing still counts.
        jumpBufferCounter -= Time.deltaTime;
        if (jumpPressed) jumpBufferCounter = jumpBufferTime;

        if (jumpBufferCounter > 0f && coyoteCounter > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpVelocity);
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
            grounded = false;
        }

        // Variable jump height: releasing early cuts the jump short.
        if (jumpReleased && rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
        }

        UpdateVisuals();
    }

    void FixedUpdate()
    {
        if (isDead) return;

        grounded = CheckGround();

        float targetSpeed = inputX * (runHeld ? runSpeed : walkSpeed);
        float rate = Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration;

        Vector2 v = rb.linearVelocity;
        v.x = Mathf.MoveTowards(v.x, targetSpeed, rate * Time.fixedDeltaTime);

        // Heavier gravity on the way down gives the classic snappy Mario arc.
        rb.gravityScale = v.y < 0f ? gravityScale * fallGravityMultiplier : gravityScale;
        if (v.y < -maxFallSpeed) v.y = -maxFallSpeed;

        rb.linearVelocity = v;
    }

    void ReadInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        inputX = 0f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) inputX -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) inputX += 1f;

        runHeld = kb.leftShiftKey.isPressed || kb.xKey.isPressed;

        jumpPressed = kb.spaceKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame ||
                      kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame;

        jumpReleased = kb.spaceKey.wasReleasedThisFrame || kb.zKey.wasReleasedThisFrame ||
                       kb.wKey.wasReleasedThisFrame || kb.upArrowKey.wasReleasedThisFrame;
    }

    bool CheckGround()
    {
        Bounds b = col.bounds;
        Vector2 center = new Vector2(b.center.x, b.min.y - 0.04f);
        Vector2 size = new Vector2(b.size.x * 0.9f, 0.08f);
        bool touching = Physics2D.OverlapBox(center, size, 0f, groundLayer) != null;
        return touching && rb.linearVelocity.y <= 0.1f;
    }

    void UpdateVisuals()
    {
        if (inputX > 0.01f) sr.flipX = false;
        else if (inputX < -0.01f) sr.flipX = true;

        Sprite idle = isBig ? bigIdle : smallIdle;
        Sprite jump = isBig ? bigJump : smallJump;
        Sprite wanted = grounded ? idle : jump;
        if (wanted != null) sr.sprite = wanted;
    }

    void ApplyForm()
    {
        col.size = isBig ? bigSize : smallSize;
        col.offset = isBig ? bigOffset : smallOffset;
    }

    // ---------- Called by other scripts ----------

    public void Bounce()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, stompBounceVelocity);
    }

    public void Grow()
    {
        if (isBig || isDead) return;
        isBig = true;
        ApplyForm();
    }

    public void TakeHit()
    {
        if (isDead || isInvincible) return;

        if (isBig)
        {
            isBig = false;
            ApplyForm();
            StartCoroutine(InvincibleRoutine());
        }
        else
        {
            Die(false);
        }
    }

    // ---------- Internals ----------

    IEnumerator InvincibleRoutine()
    {
        isInvincible = true;
        float t = 0f;
        while (t < 2f)
        {
            sr.enabled = !sr.enabled;
            t += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }
        sr.enabled = true;
        isInvincible = false;
    }

    void Die(bool fellInPit)
    {
        if (isDead) return;
        isDead = true;
        sr.enabled = true;
        col.enabled = false;

        if (fellInPit)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }
        else
        {
            rb.gravityScale = gravityScale;
            rb.linearVelocity = new Vector2(0f, 14f); // classic death hop
        }
        StartCoroutine(RestartRoutine());
    }

    IEnumerator RestartRoutine()
    {
        yield return new WaitForSeconds(2f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}