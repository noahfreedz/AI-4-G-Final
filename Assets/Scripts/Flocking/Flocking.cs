using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

class boid{
    public GameObject gameAgent;
    public Vector3 velocity;
};


public class Flocking : MonoBehaviour
{

    private List<boid> boids = new List<boid>();
    private float maxForce = 10;
    private float maxSpeed = 50;


    [SerializeField] private int numBoids = 50;
    [SerializeField] private GameObject boidPrefab;
    
    //Where the boids can be
    public Vector3 areaOfEffect;
    public float cohesionRadius;
    public float separationRadius;
    public float alignmentRadius;
    public float k;

    private void Start()
    {
        GenerateBoids();
    }

    // Update is called once per frame
    void Update()
    {
        //make boids do shit
        IntegrateBoidMovement();
    }
    
    void IntegrateBoidMovement()
    {
        for (int i = 0; i < boids.Count; i++)
        {
            Vector3 force = Seperation(i) + Alignment(i) + Cohesion(i);
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
        //loop through and create boids
        for (int i = 0; i < numBoids; i++)
        {
            print("Creating boid");
        
            // Instantiate the prefab and get a reference to it
            GameObject boidInstance = Instantiate(boidPrefab);
            boid newBoid = new boid();
            
            
        
            float minX = -10f;
            float maxX = 10f;
            float minY = -5f;
            float maxY = 5f;
            float minZ = -10f;
            float maxZ = 10f;
        
            // Create a random pos
            Vector3 randomPos = new Vector3(
                UnityEngine.Random.Range(minX, maxX),
                UnityEngine.Random.Range(minY, maxY),
                UnityEngine.Random.Range(minZ, maxZ));
            
            boidInstance.transform.position = randomPos;
            
            newBoid.gameAgent = boidInstance;
            newBoid.velocity = boidInstance.transform.forward;
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
        List<boid> neighbors  = new List<boid>();
        Vector3 velocitySum = Vector3.zero;
        Vector3 desiredVelocity = Vector3.zero;

        foreach (boid b in boids)
        {
            double distance = Vector3.Distance(boids[boidIndex].gameAgent.transform.position, b.gameAgent.transform.position);
            if (distance < alignmentRadius)
            {
                neighbors.Add(b);
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
