using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;

public class ParticleUpdate : MonoBehaviour
{
    [Header("Water Settings")]
    [SerializeField] private WaterSettings waterSettings;

    [Header("Collision Settings")]
    [SerializeField] private bool enableTerrainCollision = true;
    [SerializeField] private float restitution = 0.5f;

    [Header("Physics Settings")]
    [SerializeField] private float gravity = -9.81f;

    [Header("Particle Settings")]
    [Tooltip("Mass in kg - for radius 2.5m, use ~16,363 kg to float neutrally with density 250")]
    [SerializeField] private float mass = 1.0f;

    [Tooltip("Radius in meters - sphere diameter will be 2x this value")]
    [SerializeField] private float radius = 0.5f;

    [Tooltip("Linear damping - closer to 1.0 = less energy loss")]
    [SerializeField] private float damping = 0.99f;

    private Vector3 position;
    private Vector3 velocity;
    private Vector3 acceleration;
    private Vector3 forceAccum;

    private RigidBody rigidBody;
    private SphereCollider sphereCollider;
    private CollisionData collisionData;
    private List<TerrainCollider> terrainColliders = new List<TerrainCollider>();

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
            else
            {
                Debug.Log($"ParticleUpdate on {gameObject.name}: Found WaterSettings on {waterSettings.gameObject.name}");
            }
        }

        if (enableTerrainCollision)
        {
            InitializeCollisionSystem();
        }
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

        TerrainCollider[] foundTerrains = FindObjectsOfType<TerrainCollider>();
        terrainColliders.AddRange(foundTerrains);
        Debug.Log($"ParticleUpdate on {gameObject.name}: Found {terrainColliders.Count} terrain colliders");

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

            transform.position = position;
        }
        else
        {
            waterSettings = FindObjectOfType<WaterSettings>();
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

            if (contacts > 0)
            {
                Debug.Log($"Collision detected! {contacts} contacts, penetration: {collisionData.contactArray[0].penetration}");
            }
        }

        if (collisionData.contactCount > 0)
        {
            Debug.Log($"Resolving {collisionData.contactCount} contacts");
            ContactResolver.ResolveContacts(collisionData.contactArray, collisionData.contactCount, restitution);

            position = rigidBody.position;
            velocity = rigidBody.velocity;
        }
    }

    void OnDrawGizmos()
    {

        Gizmos.color = Color.yellow;
        Vector3 pos = Application.isPlaying ? position : transform.position;
        Gizmos.DrawWireSphere(pos, radius);

        if (Application.isPlaying)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(pos, velocity);
        }
    }
}