using UnityEngine;

[RequireComponent(typeof(Damageable))]
[RequireComponent(typeof(EnemyMovement))]
[RequireComponent(typeof(EnemyPathfinding))]
public class HomingDrone : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 4f;
    [SerializeField] private float repathInterval = 0.3f;
    [SerializeField] private float contactDistance = 0.5f;   // "coming into contact"

    [Header("Explosion")]
    [SerializeField] private float explosionRadius = 2f;
    [SerializeField] private int explosionDamage = 15;
    [SerializeField] private LayerMask damageLayers;          // Player (+ Collision if it should break crates too)
    [SerializeField] private GameObject explosionEffectPrefab;

    [Header("Contact")]
    [Tooltip("What counts as a hit. Include Player and Collision (walls).")]
    [SerializeField] private LayerMask contactLayers;
    private bool exploded;   // guards against Update's distance check and OnTriggerEnter2D both firing the same frame


    private Transform player;
    private EnemyMovement movement;
    private EnemyPathfinding pathfinding;
    private Damageable damageable;
    private float repathTimer;

    // INPUT:  none
    // OUTPUT: refs cached; own death (shot down by the player) wired to the same explosion as contact
    // USE:    Unity
    private void Awake()
    {
        movement = GetComponent<EnemyMovement>();
        pathfinding = GetComponent<EnemyPathfinding>();
        damageable = GetComponent<Damageable>();
        damageable.OnDeath += Explode;
    }

    // INPUT:  scene
    // OUTPUT: player transform cached
    // USE:    Unity
    private void Start()
    {
        Player p = FindAnyObjectByType<Player>();
        if (p != null) player = p.transform;
    }

    // INPUT:  player position
    // OUTPUT: paths toward the player every repathInterval; explodes on proximity
    // USE:    Unity. Proximity check rather than a trigger collider — reliable
    //         even at a closing speed that could tunnel past a small collider in one frame
    private void Update()
    {
        if (player == null) return;

        if (Vector2.Distance(transform.position, player.position) <= contactDistance)
        {
            Explode();
            return;
        }

        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f) { pathfinding.ComputePath(player.position); repathTimer = repathInterval; }

        Vector2 step = pathfinding.HasPath ? pathfinding.GetCurrentSteeringTarget(player.position) : (Vector2)player.position;
        movement.Move((step - (Vector2)transform.position).normalized, speed);
    }

    // INPUT:  none — called by contact, wall collision, or Damageable.OnDeath (shot down)
    // OUTPUT: AoE damage, effect, destroys self. Idempotent — safe to call more than once.
    // USE:    Update, OnTriggerEnter2D, Damageable.OnDeath
    private void Explode()
    {
        if (exploded) return;
        exploded = true;
        damageable.OnDeath -= Explode;

        foreach (var h in Physics2D.OverlapCircleAll(transform.position, explosionRadius, damageLayers))
            h.GetComponentInParent<Damageable>()?.TakeDamage(explosionDamage);
        if (explosionEffectPrefab != null) Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }

    // INPUT:  collider it physically touched
    // OUTPUT: explodes if that collider is on a contactLayers layer (Player or Collision)
    // USE:    Unity, whenever the drone's CircleCollider2D overlaps something
    private void OnTriggerEnter2D(Collider2D other)
    {
        if ((contactLayers.value & (1 << other.gameObject.layer)) == 0) return;
        Explode();
    }

    private void OnDestroy() => damageable.OnDeath -= Explode;
}