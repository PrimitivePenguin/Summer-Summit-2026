using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;


public class Bullet : MonoBehaviour
{
    [Header("Bullet Settings")]
    float speed;
    float despawnDist = 20f;
    float lifeTime;
    float timer;
    LayerMask collisionLayers; // layer for collision detection

    private Vector3 spawnPos;
    int damage;
    
    public void Initialize(float rotation, float speed, float despawnDist, int damage, LayerMask collisionLayers)
    {
        transform.rotation = Quaternion.Euler(0, 0, rotation);
        this.speed = speed;
        this.despawnDist = despawnDist;
        this.damage = damage;
        this.collisionLayers = collisionLayers;
        spawnPos = transform.position; // store the spawn position of the bullet
        this.lifeTime = despawnDist / speed; // calculate the lifetime of the bullet based on its speed and despawn distance
        timer = lifeTime;

    }

    void Update()
    {
        transform.Translate(Vector2.up * speed * Time.deltaTime); // move the bullet in the direction of its rotation at the speed value
        timer -= Time.deltaTime;
        if (timer <= 0) // if the distance from the spawn position to the current position is greater than the despawn distance
        {
            Debug.Log("Bullet despawned from distance");
            gameObject.SetActive(false); // deactivate the bullet
        }
    }
    public void ResetTimer()
    {
        timer = lifeTime; // reset the timer to the lifetime value
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (((1 << collision.gameObject.layer) & collisionLayers) == 0)
            return;

        Damageable target = collision.GetComponent<Damageable>();
        if (target != null)
            target.TakeDamage(damage);

        gameObject.SetActive(false);
    }
}
