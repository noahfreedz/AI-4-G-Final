using System.Collections;
using System.Collections.Generic;
using UnityEngine;

struct boid{
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;
};


public class Flocking : MonoBehaviour
{

    private List<boid> boids;
    private float maxForce = 10;
    
    //Where the boids can be
    public Vector3 areaOfEffect;
    public float cohesionRadius;
    public float separationRadius;
    public float alignmentRadius;
    public float k;

    // Update is called once per frame
    void Update()
    {

    }
    

    Vector3 Seperation(int boidIndex)
    {
        List<boid> neighbors  = new List<boid>();
        

        for (int i = 0; i < boids.Count; i++)
        {
            if (i == boidIndex) continue;
            
            float distance = Vector3.Distance(boids[boidIndex].position, boids[i].position);
            if (distance < separationRadius)
            {
                neighbors.Add(boids[i]);
            }
        }

        if (neighbors.Count > 0)
        {
            return Vector3.zero;
        }
        
        
        Vector3 totalRepulsionForce = Vector3.zero;

        foreach (boid b in neighbors)
        {
            float distance = Vector3.Distance(boids[boidIndex].position, b.position);
            
            Vector3 direction = (boids[boidIndex].position - b.position).normalized;
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
            double distance = Vector3.Distance(boids[boidIndex].position, b.position);
            if (distance < alignmentRadius)
            {
                neighbors.Add(b);
            }
        }

        if (neighbors.Count > 0)
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
            
            double distance = Vector3.Distance(transform.position, boids[i].position);
            if (distance < cohesionRadius)
            {
                neighbors.Add(boids[i]);
            }
        }
        
        //average position
        foreach (boid b in neighbors)
        {
            centerMass += b.position;
        }
        
        //average it out
        centerMass /= boids.Count;
        
        Vector3 forceToCenter = centerMass - boids[boidIndex].position;
        return forceToCenter;
    }

}
