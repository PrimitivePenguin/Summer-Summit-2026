using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f; // SerializedField allows adjust in Unity Inspector
    private Rigidbody2D rb;
    private Vector2 moveInput;
    private Animator animator;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        rb = GetComponent<Rigidbody2D>();  // get the Rigidbody2D component attached to it -> player
        animator = GetComponent<Animator>(); // get the Animator component attached to it -> player
    }

    // Update is called once per frame
    void Update()
    {
        rb.linearVelocity = moveInput * moveSpeed; // set the velocity of the player to the input * speed
        // Debug.Log("Input: " + moveInput);
        // Debug.Log("Velocity: " + rb.linearVelocity);
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
}
