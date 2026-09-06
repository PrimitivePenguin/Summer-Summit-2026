using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

// inspired by SkyanSam

public class BulletSpawn : MonoBehaviour
{
    public BulletSpawnData[] spawnDatas;
    public bool isSeqRandom;
    public bool isAutomaticSpawn;
    public int bulletLayer;

    public InputAction click;

    int index = 0;
    private float timer;
    private float[] rotations;
    // Prevents another burst from starting while current is active
    private bool isBursting = false;
    public BulletSpawnData GetSpawnData()
    {
        return spawnDatas[index];
    }
    public BulletSpawnData GetCurrentData() => spawnDatas[index];
    private float curMinDeg;
    private float curMaxDeg;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        // Initialize values from bullet spawn data
        timer = 0; // GetSpawnData().cooldown;                
        rotations = new float[GetSpawnData().numBullets]; 


        if (!GetSpawnData().isRandom)
        {
            DistributedRotations();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (timer > 0f) timer -= Time.deltaTime;

        if (isAutomaticSpawn)
        {
            UpdateAutomatic();
        }
    }

    private void UpdateAutomatic()
    {
        if (timer <= 0f && !isBursting)
        {
            Fire();
            ChangeSpawnData();
        }
    }

    public bool Fire()
    {   
        // Debug.Log($"[BulletSpawn.cs] - Fire() isBursting: {isBursting}");
        if (isBursting || timer > 0f) {return false;} // Prevents another burst from starting while current is active
        isBursting = true;
        timer = GetSpawnData().cooldown + (GetSpawnData().burstDelay * (GetSpawnData().numBullets - 1)); // cooldown + burstdelay * numbullets
        StartCoroutine(SpawnBurstCoroutine());
        return true;
    }
    private void ChangeSpawnData()
    {
        if (isSeqRandom)
        {
            index = Random.Range(0, spawnDatas.Length);
        }
        else
        {
            index = (index + 1) % spawnDatas.Length;
        }
    }
    public float[] RandomRotations()
    {
        for (int i = 0; i < GetSpawnData().numBullets; i++)
        {
            rotations[i] = Random.Range(curMinDeg, curMaxDeg);
        }
        return rotations;
    }
    public float[] DistributedRotations()
    {
        for (int i = 0; i < GetSpawnData().numBullets; i++)
        {
            // only 1 bullet
            if (GetSpawnData().numBullets == 1)
            {
                rotations[i] = (GetSpawnData().minRotation + GetSpawnData().maxRotation) / 2; // set to middle
                break;
            }
            var fraction = (float)i / ((float)GetSpawnData().numBullets - 1); // get the distribution btw each bullet
            // set above to -1 cause it can count from 0
            var difference = curMaxDeg - curMinDeg;
            var fracDif = fraction * difference; // get the difference btw each bullet
            rotations[i] = curMinDeg + fracDif; // set the rotation of each bullet
        }

        // maybe make the order random -> shuffle random
        return rotations;
    }

    IEnumerator SpawnBurstCoroutine()
    {
        // Debug.Log($"[BulletSpawn.cs] - SpawnBurstCoroutine() started");
        BulletSpawnData data = GetSpawnData();
        // Debug.Log($"SpawnData: {data}, numBullets: {data?.numBullets}");
        // Check coroutine
        // Debug.Log($"Coroutine started — numBullets: {data.numBullets}, burstDelay: {data.burstDelay}");
        rotations = new float[data.numBullets];

        if (data.isRandom)
        { 
            RandomRotations();
        } else 
        {
            DistributedRotations();
        }

        // Debug.Log($"Rotations: {string.Join(", ", rotations)}");

        for (int i = 0; i < data.numBullets; i++)
        {
            // Debug.Log($"Loop iteration {i}, gameObject active: {gameObject.activeInHierarchy}");
            if (this == null || !gameObject.activeInHierarchy)
            {
                // Debug.Log("Coroutine aborted — object inactive"); // is the guard killing it?
                isBursting = false;
                yield break;
            }
            // Debug.Log($"Spawning bullet {i + 1}/{data.numBullets} at rotation {rotations[i]}");
            
            SpawnSingleBullet(data, i);

            if (data.burstDelay > 0f) {
                yield return new WaitForSeconds(data.burstDelay);
            }
        }
        isBursting = false;
    }

    void SpawnSingleBullet(BulletSpawnData data, int i)
    {
        // Debug.Log($"BulletSpawn object: {gameObject.name}, position: {transform.position}");
        // Debug.Log($"BulletSpawn parent: {transform.parent?.name}, parent position: {transform.parent?.position}");
        int layer = bulletLayer;
        string bulletName = data.name;
    
        GameObject spawnedBullet = BulletManager.GetBullet(layer, bulletName);
        if (spawnedBullet == null)
        {
            spawnedBullet = Instantiate(data.bulletResource, transform.position, Quaternion.identity);
            spawnedBullet.name = data.name;
            BulletManager.AddBullet(spawnedBullet);
        }
        else
        {
            spawnedBullet.transform.SetParent(null);
            spawnedBullet.transform.position = transform.position;
        }
        spawnedBullet.layer = bulletLayer;

        Bullet bullet = spawnedBullet.GetComponent<Bullet>();
        bullet.Initialize(rotations[i], data.bulletSpeed, data.bulletDespawnDist, data.damage, data.collisionLayers);
        if (!spawnedBullet.activeInHierarchy)
        {
            spawnedBullet.SetActive(true);
        }
    }
    // Called by AimController to update min and max rotation angle
    public void SetFiringArc(float minDeg, float maxDeg)
    {
        BulletSpawnData data = GetCurrentData(); // use this cause this is called by aimcontroller outside
        curMinDeg = minDeg;
        curMaxDeg = maxDeg;
    }
}
