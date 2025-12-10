using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;

public class ParticleUpdate : MonoBehaviour
{
    [Header("Water Settings")]
    [SerializeField] private WaterSettings waterSettings;

    [Header("Collision Settings")]
    [SerializeField] private bool enableTerrainCollision = true;
    [SerializeField] public bool enableSphereCollision = true;
    [SerializeField] private float restitution = 0.5f;

    [Header("Destruction Settings")]
    [SerializeField] private bool enableDestructionTimer = true;
    [SerializeField] private float destructionDelay = 5.0f;
    [SerializeField] private bool fadeOut = true;
    [SerializeField] private float fadeStartTime = 1.0f; // Start fading 1 second before destruction

    [Header("Physics Settings")]
    [SerializeField] private float gravity = -9.81f;

    [Header("Particle Settings")]
    [Tooltip("Mass in kg - for radius 2.5m, use ~16,363 kg to float neutrally with density 250")]
    [SerializeField] private float mass = 1.0f;

    [Tooltip("Radius in meters - sphere diameter will be 2x this value")]
    [SerializeField] public float radius = 0.5f;

    [Tooltip("Linear damping - closer to 1.0 = less energy loss")]
    [SerializeField] private float damping = 0.99f;

    public Vector3 position;
    public Vector3 velocity;
    private Vector3 acceleration;
    private Vector3 forceAccum;

    private RigidBody rigidBody;
    private SphereCollider sphereCollider;
    private CollisionData collisionData;
    private List<TerrainCollider> terrainColliders = new List<TerrainCollider>();
    private List<ParticleUpdate> nearbyParticles = new List<ParticleUpdate>();

    // Destruction timer
    private bool destructionTimerStarted = false;
    private float destructionTimer = 0f;
    private Renderer particleRenderer;
    private Color originalColor;
    private bool hasCollided = false;

    void Start()
    {
        position = transform.position;
        velocity = Vector3.zero;
        acceleration = new Vector3(0, gravity, 0);
        forceAccum = Vector3.zero;

        if (waterSettings == null)
        {
            waterSettings = FindObjectOfType<WaterSettings>();
            if (waterSettings == null)
            {
                Debug.LogWarning($"ParticleUpdate on {gameObject.name}: No WaterSettings found in scene!");
            }
        }

        if (enableTerrainCollision || enableSphereCollision)
        {
            InitializeCollisionSystem();
        }

        // Get renderer for fade effect
        particleRenderer = GetComponent<Renderer>();
        if (particleRenderer != null && particleRenderer.material != null)
        {
            originalColor = particleRenderer.material.color;
        }

        ParticleCollisionManager.RegisterParticle(this);
    }

    void OnDestroy()
    {
        ParticleCollisionManager.UnregisterParticle(this);
    }

    void InitializeCollisionSystem()
    {
        rigidBody = GetComponent<RigidBody>();
        if (rigidBody == null)
        {
            rigidBody = gameObject.AddComponent<RigidBody>();
        }

        rigidBody.position = position;
        rigidBody.velocity = velocity;
        rigidBody.orientation = Quaternion.identity;
        rigidBody.inverseMass = 1.0f / mass;

        float inertia = (2.0f / 5.0f) * mass * radius * radius;
        float inverseInertia = 1.0f / inertia;
        rigidBody.inverseInertiaTensor = new float3x3(
            inverseInertia, 0, 0,
            0, inverseInertia, 0,
            0, 0, inverseInertia
        );
        rigidBody.linearDamping = damping;
        rigidBody.angularDamping = 0.99f;
        rigidBody.angularVelocity = Vector3.zero;
        rigidBody.calculateDerivedData();

        sphereCollider = new SphereCollider(radius);
        sphereCollider.SetBody(rigidBody);

        if (enableTerrainCollision)
        {
            TerrainCollider[] foundTerrains = FindObjectsOfType<TerrainCollider>();
            terrainColliders.AddRange(foundTerrains);
        }

        collisionData = new CollisionData();
        collisionData.Reset(10);
    }

    void FixedUpdate()
    {
        if (waterSettings != null)
        {
            float deltaTime = Time.fixedDeltaTime;

            Vector3 gravityForce = new Vector3(0, gravity * mass, 0);
            AddForce(gravityForce);

            ApplyBuoyancy();

            Integrate(deltaTime);

            if (enableTerrainCollision && terrainColliders.Count > 0)
            {
                CheckTerrainCollisions();
            }

            if (enableSphereCollision)
            {
                CheckSphereCollisions();
            }

            transform.position = position;
        }
        else
        {
            waterSettings = FindObjectOfType<WaterSettings>();
        }

        if (destructionTimerStarted && enableDestructionTimer)
        {
            destructionTimer += Time.fixedDeltaTime;

            if (fadeOut && particleRenderer != null && particleRenderer.material != null)
            {
                float fadeThreshold = destructionDelay - fadeStartTime;
                if (destructionTimer >= fadeThreshold)
                {
                    float fadeProgress = (destructionTimer - fadeThreshold) / fadeStartTime;
                    Color newColor = originalColor;
                    newColor.a = Mathf.Lerp(originalColor.a, 0f, fadeProgress);
                    particleRenderer.material.color = newColor;
                }
            }

            if (destructionTimer >= destructionDelay)
            {
                Destroy(gameObject);
            }
        }
    }

    void Integrate(float deltaTime)
    {
        Vector3 resultingAcc = forceAccum / mass;
        velocity += resultingAcc * deltaTime;
        velocity *= Mathf.Pow(damping, deltaTime);
        position += velocity * deltaTime;
        forceAccum = Vector3.zero;
    }

    void AddForce(Vector3 force)
    {
        forceAccum += force;
    }

    void ApplyBuoyancy()
    {
        if (waterSettings == null || !waterSettings.HasWaterSurface()) return;

        float waterHeight = waterSettings.GetWaterHeight();
        float depth = waterHeight - position.y;

        if (depth <= -radius) return;

        float maxDepth = radius * 2;
        float submersionRatio = Mathf.Clamp01((depth + radius) / maxDepth);
        float totalVolume = (4.0f / 3.0f) * Mathf.PI * radius * radius * radius;
        float submergedVolume = totalVolume * submersionRatio;
        float buoyancyMagnitude = waterSettings.GetWaterDensity() * submergedVolume * Mathf.Abs(gravity);

        if (velocity.y > 0)
        {
            float velocityDamping = Mathf.Max(0.1f, 1.0f - velocity.y * 0.15f);
            buoyancyMagnitude *= velocityDamping;
        }

        Vector3 buoyancyForce = new Vector3(0, buoyancyMagnitude, 0);
        AddForce(buoyancyForce);

        if (velocity.sqrMagnitude > 0.01f)
        {
            Vector3 dragForce = -velocity * waterSettings.GetWaterDrag() * submersionRatio;
            AddForce(dragForce);
        }
    }

    void CheckTerrainCollisions()
    {
        rigidBody.position = position;
        rigidBody.velocity = velocity;
        rigidBody.calculateDerivedData();
        sphereCollider.UpdateInternals();

        collisionData.Reset(10);

        foreach (TerrainCollider terrain in terrainColliders)
        {
            if (terrain == null) continue;

            MeshCollider meshCollider = terrain.GetMeshCollider();
            if (meshCollider == null) continue;

            int contacts = MeshCollisionDetection.SphereMesh(sphereCollider, meshCollider, collisionData);

            if (contacts > 0 && !hasCollided)
            {
                OnFirstCollision();
            }
        }

        if (collisionData.contactCount > 0)
        {
            ContactResolver.ResolveContacts(collisionData.contactArray, collisionData.contactCount, restitution);
            position = rigidBody.position;
            velocity = rigidBody.velocity;
        }
    }

    void CheckSphereCollisions()
    {
        rigidBody.position = position;
        rigidBody.velocity = velocity;
        rigidBody.calculateDerivedData();
        sphereCollider.UpdateInternals();

        ParticleCollisionManager.GetNearbyParticles(this, position, radius, nearbyParticles);

        collisionData.Reset(10);

        foreach (ParticleUpdate other in nearbyParticles)
        {
            if (other == null || !other.enableSphereCollision) continue;

            ParticleCollisionManager.IncrementChecks();

            other.rigidBody.position = other.position;
            other.rigidBody.velocity = other.velocity;
            other.rigidBody.calculateDerivedData();
            other.sphereCollider.UpdateInternals();

            int contacts = CollisionTests.SphereSphere(sphereCollider, other.sphereCollider, collisionData);

            if (contacts > 0)
            {
                ParticleCollisionManager.IncrementCollisions();

                if (!hasCollided)
                {
                    OnFirstCollision();
                }
            }
        }

        if (collisionData.contactCount > 0)
        {
            ContactResolver.ResolveContacts(collisionData.contactArray, collisionData.contactCount, restitution);
            position = rigidBody.position;
            velocity = rigidBody.velocity;
        }
    }

    void OnFirstCollision()
    {
        if (!hasCollided && enableDestructionTimer)
        {
            hasCollided = true;
            destructionTimerStarted = true;
            destructionTimer = 0f;
            Debug.Log($"{gameObject.name}: Collision detected! Destruction in {destructionDelay} seconds.");
        }
    }

    public Vector3 GetPosition() { return position; }
    public float GetRadius() { return radius; }
    public bool GetEnableSphereCollision() { return enableSphereCollision; }
    public SphereCollider GetSphereCollider() { return sphereCollider; }
    public RigidBody GetRigidBody() { return rigidBody; }
    public bool IsMarkedForDestruction() { return destructionTimerStarted; }
    public float GetDestructionTimeRemaining() { return Mathf.Max(0, destructionDelay - destructionTimer); }

    void OnDrawGizmos()
    {
        Gizmos.color = destructionTimerStarted ? Color.red : Color.yellow;
        Vector3 pos = Application.isPlaying ? position : transform.position;
        Gizmos.DrawWireSphere(pos, radius);

        if (Application.isPlaying)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(pos, velocity);

            if (destructionTimerStarted)
            {
                float timeRemaining = destructionDelay - destructionTimer;
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(pos, radius * 1.2f);

                float maxHeight = 5f;
                float currentHeight = (timeRemaining / destructionDelay) * maxHeight;
                Gizmos.DrawLine(pos, pos + Vector3.up * currentHeight);
            }
        }
    }
}