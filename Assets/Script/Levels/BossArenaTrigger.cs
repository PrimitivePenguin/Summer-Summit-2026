using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BossArenaTrigger : MonoBehaviour
{
    [SerializeField] private GameObject boss;                 // inactive in scene until entry
    [SerializeField] private GameObject[] doorBlockers;       // colliders on Collision layer, inactive until entry
    [SerializeField] private PolygonCollider2D arenaBounds;   // confiner shape, like MapTransition
    [SerializeField] private TMPro.TMP_Text bossLine;         // "My name is Bullet Hell. You can call me Hell."
    [SerializeField] private float lineDuration = 3f;
    private bool fired;

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (fired || !col.CompareTag("Player")) return;
        fired = true;
        foreach (var d in doorBlockers) d.SetActive(true);
        FindAnyObjectByType<Unity.Cinemachine.CinemachineConfiner2D>().BoundingShape2D = arenaBounds;
        GridManager.Instance?.MarkDirty();                    // blockers are obstacles now
        boss.SetActive(true);
        LevelManager.Instance.SetKillTarget(boss.GetComponent<Damageable>());
        if (bossLine) StartCoroutine(ShowLine());
    }
    private IEnumerator ShowLine() { bossLine.gameObject.SetActive(true); yield return new WaitForSeconds(lineDuration); bossLine.gameObject.SetActive(false); }
}