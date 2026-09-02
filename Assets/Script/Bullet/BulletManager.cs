using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BulletManager : MonoBehaviour
{
    public static List<GameObject> bullets = new List<GameObject>();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        bullets = new List<GameObject>(); // better list
    }
    

    public static GameObject GetBullet(int layer, string bulletName)
    {
        Debug.Log($"[BulletManager] Looking for: layer={layer}, name='{bulletName}', pool size={bullets.Count}");
        for (int i = 0; i < bullets.Count; i++)
        {
            Debug.Log($"[BulletManager] Bullet {i}: active={bullets[i].activeInHierarchy}, layer={bullets[i].layer}, name='{bullets[i].name}'");
            if (!bullets[i].activeInHierarchy && bullets[i].layer == layer && bullets[i].name == bulletName)
            {
            //     bullets[i].GetComponent<Bullet>().ResetTimer(); // reset the timer of the bullet    
                return bullets[i]; // return the first inactive bullet
            }
        }
        return null; // no inactive bullets
    }
    public static void AddBullet(GameObject bullet)
    {
        BulletManager.bullets.Add(bullet);
    }
}
