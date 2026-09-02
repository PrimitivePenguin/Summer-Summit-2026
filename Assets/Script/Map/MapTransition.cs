using UnityEngine;
using Unity.Cinemachine;
using System.Collections;
using System.Collections.Generic;

public class MapTransition : MonoBehaviour
{
    [SerializeField] PolygonCollider2D mapBoundary; // one we are transitioning to
    [SerializeField] CinemachineConfiner2D confiner; // the confiner we are transitioning to
    [SerializeField] Direction direction;
    // [SerializeField] float additivePos - 2f; // the scene we are transitioning to


    enum Direction { Up, Down, Left, Right }

    private void Awake() {
        confiner = FindAnyObjectByType<CinemachineConfiner2D>(); // find the confiner in the scene
    }

    private void OnTriggerEnter2D(Collider2D collision) {
        if (collision.gameObject.CompareTag("Player")) { // if the player enters the trigger
            confiner.BoundingShape2D = mapBoundary; // set the confiner to the new map boundary
            UpdatePlayerPosition(collision.gameObject); // update the player position based on the direction of the transition
        }
    }
    private void UpdatePlayerPosition(GameObject player) {
        Vector3 newPos = player.transform.position;
        Debug.Log("Player Position Before Transition: " + newPos);
        Debug.Log("Transition Direction: " + direction);
        switch (direction) {
            case Direction.Up:
                newPos.y += 2f;
                break;
            case Direction.Down:
                newPos.y -= 2f;
                break;
            case Direction.Left:
                newPos.x -= 2f;
                break;
            case Direction.Right:
                newPos.x += 2f;
                break;
        }
        player.transform.position = newPos;

    }
}
