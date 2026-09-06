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
    
    private BulletSpawnData data;
    public void Initialize(float rotation, BulletSpawnData data)
    {
        this.data = data;
        transform.rotation = Quaternion.Euler(0, 0, rotation);
        speed = data.bulletSpeed; despawnDist = data.bulletDespawnDist; damage = data.damage; collisionLayers = data.collisionLayers;
        lifeTime = despawnDist / speed; timer = lifeTime;
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

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (((1 << col.gameObject.layer) & collisionLayers) == 0) return;
        if (data != null && data.isAoe) Explode();
        else col.GetComponentInParent<Damageable>()?.TakeDamage(damage);
        gameObject.SetActive(false);
    }
    private void Explode()
    {
        foreach (var h in Physics2D.OverlapCircleAll(transform.position, data.aoeRadius, collisionLayers))
            h.GetComponentInParent<Damageable>()?.TakeDamage(damage);
        if (data.aoeEffectPrefab) Destroy(Instantiate(data.aoeEffectPrefab, transform.position, Quaternion.identity), 2f);
    }
}
