using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;


[RequireComponent(typeof(Rigidbody2D))] // just in case y'know?
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f; // SerializedField allows adjust in Unity Inspector
    [SerializeField] private float dashSpeed = 15f;
    [SerializeField] private float dashDuration = 0.12f; // you can figure this out yourself kiddo
    [SerializeField] private float dashRecovery = 0.18f; // You can't move for this long after dash
    [SerializeField] private float dashCooldown = 1.25f; // dash cooldown, duh
    private Rigidbody2D rb;
    private Animator animator;

    private Damageable damageable;
    private Vector2 moveInput;

    // Dash states and timers
    public bool isDashing {get; private set;}
    public bool isInRecovery {get; private set;}
    private float dashCooldownTimer = 0f;
    private Vector2 dashVelocity;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        rb = GetComponent<Rigidbody2D>();  // get the Rigidbody2D component attached to it -> player
        animator = GetComponent<Animator>(); // get the Animator component attached to it -> player
        damageable = GetComponent<Damageable>(); // get the Damageable component attacked to it -> player
    }

    // Update is called once per frame
    void Update()
    {
        if (dashCooldownTimer > 0f){
            dashCooldownTimer -= Time.deltaTime; // (We don't do by frame cuz lag is stupid)
        }
        
        // MEMO: Dear Alexander: Please normalize your vectors before multiplication. Your method would cause 
        // diagonal movement to be faster than cardinal
        
        // rb.linearVelocity = moveInput * moveSpeed; // set the velocity of the player to the input * speed
        // Debug.Log("Input: " + moveInput);
        // Debug.Log("Velocity: " + rb.linearVelocity);
    }

    void FixedUpdate(){
        if (isDashing){
            rb.linearVelocity = dashVelocity;
        }
        else if (isInRecovery){
            rb.linearVelocity = Vector2.zero;
        }
        else{
            rb.linearVelocity = moveInput.normalized * moveSpeed; // set the velocity of the player to the input * speed
        }
    }

    public void Move(InputAction.CallbackContext context) {
        if (context.performed){
            moveInput = context.ReadValue<Vector2>();

            if (animator != null){
                animator.SetBool("isWalking", true);
                animator.SetFloat("InputX", moveInput.x);
                animator.SetFloat("InputY", moveInput.y);

                // save direction
                animator.SetFloat("LastInputX", moveInput.x);
                animator.SetFloat("LastInputY", moveInput.y);
            }
        }
        
        else if (context.canceled){
            moveInput = Vector2.zero;

            if (animator != null){
                animator.SetBool("isWalking", false);
                animator.SetFloat("InputX", 0);
                animator.SetFloat("InputY", 0);
                // Don't touch last input here so it remembers when you stop moving (lol)
            }
        }
        /*
        animator.SetBool("isWalking", true);
        if (context.canceled) {
            animator.SetBool("isWalking", false);
            // Store last input value for animation as LastInputX and LastInputY
            animator.SetFloat("LastInputX", moveInput.x); 
            animator.SetFloat("LastInputY", moveInput.y); 
        }
        moveInput = context.ReadValue<Vector2>(); // read the input value from the context and store it in moveInput        
        // Get input for input -> use this to move with animation
        animator.SetFloat("InputX", moveInput.x); 
        animator.SetFloat("InputY", moveInput.y); 
        */
        
    }

    public void Dash(InputAction.CallbackContext context){
        if (!context.performed) return;
        if (dashCooldownTimer > 0f || isDashing || isInRecovery) return;

        StartCoroutine(PerformDashRoutine());
    }

    private IEnumerator PerformDashRoutine(){
        isDashing = true;
        dashCooldownTimer = dashCooldown;

        // resolve dash direction
        Vector2 dir;
        if(moveInput.sqrMagnitude >= 0.001f){
            dir = moveInput.normalized;
        }
        else {
            // not moving, read from animator floats
            Vector2 lastInput = Vector2.zero;
            if (animator != null){
                lastInput = new Vector2(
                    animator.GetFloat("LastInputX"),
                    animator.GetFloat("LastInputY")
                );
            }
            if(lastInput.sqrMagnitude >= 0.001f){
                dir = lastInput.normalized;
            }
            else{
                // They haven't moved yet apparently
                dir = Vector2.up;
            }
        }

        dashVelocity = dir*dashSpeed;

        if (damageable != null) damageable.isInvulnerable = true;

        yield return new WaitForSeconds(dashDuration);

        isDashing = false;
        if (damageable != null) damageable.isInvulnerable = false;
            
        isInRecovery = true;
        yield return new WaitForSeconds(dashRecovery);
        isInRecovery = false;
    }
}
