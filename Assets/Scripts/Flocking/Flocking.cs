using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

class boid{
    public GameObject gameAgent;
    public Vector3 velocity;
};

struct BoundingBox{
    public Vector3 min;
    public Vector3 max;
};


public class Flocking : MonoBehaviour
{

    private List<boid> boids = new List<boid>();
    private float maxForce = 10;
    private float maxSpeed = 10;


    [SerializeField] private int numBoids = 50;
    [SerializeField] private GameObject boidPrefab;
    
    //Where the boids can be
    public Vector3 areaOfEffect;
    public float cohesionRadius = 1;
    public float separationRadius = 1;
    public float alignmentRadius = 1;
    public float k = 1;
    
    BoundingBox boundingBox;

    private void Start()
    {
        boundingBox = new BoundingBox();
        boundingBox.min = transform.position - areaOfEffect;
        boundingBox.max = transform.position + areaOfEffect;
        GenerateBoids();
    }

    // Update is called once per frame
    void Update()
    {
        //make boids do stuff
        IntegrateBoidMovement();
    }
    
    void IntegrateBoidMovement()
    {
        for (int i = 0; i < boids.Count; i++)
        {
            //if boid is outside of the area of effect, reverse direction back into the bounding box
            if (boids[i].gameAgent.transform.position.x > boundingBox.max.x
                || boids[i].gameAgent.transform.position.x < boundingBox.min.x
                || boids[i].gameAgent.transform.position.z > boundingBox.max.z
                || boids[i].gameAgent.transform.position.z < boundingBox.min.z)
            {
                boids[i].velocity = -boids[i].velocity;
            }
            
            Vector3 force = Seperation(i) + Alignment(i) + Cohesion(i);
            //Vector3 force = Alignment(i);
            
            boids[i].velocity += force * Time.deltaTime;
        
            // Limit speed
            if (boids[i].velocity.magnitude > maxSpeed)
            {
                boids[i].velocity = boids[i].velocity.normalized * maxSpeed;
            }
        
            // Update GameObject position
            boids[i].gameAgent.transform.position += boids[i].velocity * Time.deltaTime;
        }
    }

    void GenerateBoids()
    {
        for (int i = 0; i < numBoids; i++)
        {
            print("Creating boid");
    
            GameObject boidInstance = Instantiate(boidPrefab);
            boid newBoid = new boid();
        
            float minX = -10f;
            float maxX = 10f;
            float minY = -5f;
            float maxY = 5f;
            float minZ = -10f;
            float maxZ = 10f;
    
            Vector3 randomPos = new Vector3(
                UnityEngine.Random.Range(minX, maxX),
                UnityEngine.Random.Range(minY, maxY),
                UnityEngine.Random.Range(minZ, maxZ));
        
            boidInstance.transform.position = randomPos;
        
            // Give each boid a random initial velocity
            Vector3 randomVelocity = new Vector3(
                UnityEngine.Random.Range(-1f, 1f),
                UnityEngine.Random.Range(-1f, 1f),
                UnityEngine.Random.Range(-1f, 1f)).normalized * UnityEngine.Random.Range(maxSpeed * 0.3f, maxSpeed * 0.7f);
        
            newBoid.gameAgent = boidInstance;
            newBoid.velocity = randomVelocity;
            boids.Add(newBoid);
        }
    }
    

    Vector3 Seperation(int boidIndex)
    {
        List<boid> neighbors  = new List<boid>();
        

        for (int i = 0; i < boids.Count; i++)
        {
            if (i == boidIndex) continue;
            
            float distance = Vector3.Distance(boids[boidIndex].gameAgent.transform.position, boids[i].gameAgent.transform.position);
            if (distance < separationRadius)
            {
                neighbors.Add(boids[i]);
            }
        }

        if (neighbors.Count == 0)
        {
            return Vector3.zero;
        }
        
        
        Vector3 totalRepulsionForce = Vector3.zero;

        foreach (boid b in neighbors)
        {
            float distance = Vector3.Distance(boids[boidIndex].gameAgent.transform.position, b.gameAgent.transform.position);
            
            Vector3 direction = (boids[boidIndex].gameAgent.transform.position - b.gameAgent.transform.position).normalized;
            float magnitude = k / distance;
            
            totalRepulsionForce += direction * magnitude;
        }

        if (totalRepulsionForce.magnitude > maxForce)
        {
            totalRepulsionForce = totalRepulsionForce.normalized *  maxForce;
        }

        return totalRepulsionForce;
    }

    Vector3 Alignment(int boidIndex)
    {
        List<boid> neighbors = new List<boid>();
        Vector3 velocitySum = Vector3.zero;
        Vector3 desiredVelocity = Vector3.zero;

        for (int i = 0; i < boids.Count; i++) 
        {
            if (i == boidIndex) continue;  // Skip self
        
            double distance = Vector3.Distance(boids[boidIndex].gameAgent.transform.position, 
                boids[i].gameAgent.transform.position);
            if (distance < alignmentRadius)
            {
                neighbors.Add(boids[i]);
            }
        }

        if (neighbors.Count == 0)
        {
            return Vector3.zero;
        }

        foreach (boid b in neighbors)
        {
            velocitySum += b.velocity;
        }

        Vector3 averageVelocity = velocitySum / neighbors.Count;
        desiredVelocity = averageVelocity * k;
    
        return desiredVelocity;
    }
    
    Vector3 Cohesion(int boidIndex)
    {
        List<boid> neighbors  = new List<boid>();
        Vector3 centerMass = Vector3.zero;
        
        for (int i = 0; i < boids.Count; i++)
        {
            
            if (i == boidIndex) continue;
            
            double distance = Vector3.Distance(boids[boidIndex].gameAgent.transform.position, boids[i].gameAgent.transform.position);
            if (distance < cohesionRadius)
            {
                neighbors.Add(boids[i]);
            }
        }
        
        if (neighbors.Count == 0)
        {
            return Vector3.zero;
        }
        
        //average position
        foreach (boid b in neighbors)
        {
            centerMass += b.gameAgent.transform.position;
        }
        
        //average it out
        centerMass /= neighbors.Count;
        
        Vector3 forceToCenter = centerMass - boids[boidIndex].gameAgent.transform.position;
        return forceToCenter;
    }

}
