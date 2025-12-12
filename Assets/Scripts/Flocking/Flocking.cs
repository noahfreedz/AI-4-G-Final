using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

class Boid{
    public GameObject gameAgent;
    public Vector3 velocity;
};

class Preditor
{
    public GameObject gameAgent;
    public Vector3 velocity;
    public int targetIndex;
}

struct BoundingBox{
    public Vector3 min;
    public Vector3 max;
};


public class Flocking : MonoBehaviour
{

    private List<Boid> boids = new List<Boid>();
    private Preditor eagle;
    private float maxForce = 10;
    private float maxSpeed = 20;
    private float eagleMaxSpeed = 10;



    [SerializeField] private int numBoids = 50;
    [SerializeField] private GameObject boidPrefab;
    [SerializeField] private GameObject eaglePrefab;
    
    //Where the boids can be
    public Vector3 areaOfEffect;
    public Vector3 boundingBoxCenter = Vector3.zero;
    public float cohesionRadius = 1;
    public float separationRadius = 1;
    public float alignmentRadius = 1;
    public float k = 1;
    public float eagleRadius = 5;
    
    BoundingBox boundingBox;

    private void Start()
    {
        boundingBox = new BoundingBox();
        boundingBox.min = boundingBoxCenter - areaOfEffect;
        boundingBox.max = boundingBoxCenter + areaOfEffect;
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
                //reverse direction, and smooth out the bounce
                boids[i].velocity = -boids[i].velocity;
            }
            
            Vector3 force = Seperation(i) + Alignment(i) + Cohesion(i);
            
            boids[i].velocity += force * Time.deltaTime;
        
            //Limit speed
            if (boids[i].velocity.magnitude > maxSpeed)
            {
                boids[i].velocity = boids[i].velocity.normalized * maxSpeed;
            }
        
            //Update GameObject position
            boids[i].gameAgent.transform.position += boids[i].velocity * Time.deltaTime;
            
            //make the boids rotate in the direction they are moving in
            if (boids[i].velocity.magnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(boids[i].velocity);
                boids[i].gameAgent.transform.rotation = targetRotation * Quaternion.Euler(0, 90, 0);
            }
        }
    
        //Check for eagle catching boids
        float catchRadius = 1.5f;
        for (int i = boids.Count - 1; i >= 0; i--)
        {
            float distance = Vector3.Distance(eagle.gameAgent.transform.position, boids[i].gameAgent.transform.position);
            if (distance < catchRadius)
            {
                Destroy(boids[i].gameAgent);
                boids.RemoveAt(i);
            }
        }
    
        Vector3 eagleForce = ChaseBoids();
        eagle.velocity += eagleForce * Time.deltaTime;

        //Limit eagle speed
        if (eagle.velocity.magnitude > eagleMaxSpeed)
        {
            eagle.velocity = eagle.velocity.normalized * eagleMaxSpeed;
        }

        //Check eagle bounds
        if (eagle.gameAgent.transform.position.x > boundingBox.max.x
            || eagle.gameAgent.transform.position.x < boundingBox.min.x
            || eagle.gameAgent.transform.position.z > boundingBox.max.z
            || eagle.gameAgent.transform.position.z < boundingBox.min.z)
        {
            eagle.velocity = -eagle.velocity;
        }

        eagle.gameAgent.transform.position += eagle.velocity * Time.deltaTime;
        
        //Rotate the eagle to the direction its moving in
        if (eagle.velocity.magnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(eagle.velocity);
            eagle.gameAgent.transform.rotation = targetRotation * Quaternion.Euler(0, 90, 0);
        }
    }

    void GenerateBoids()
    {
        for (int i = 0; i < numBoids; i++)
        {
            print("Creating boid");
    
            GameObject boidInstance = Instantiate(boidPrefab);
            Boid newBoid = new Boid();
        
            float minX = -10f;
            float maxX = 10f;
            float minY = -5f;
            float maxY = 5f;
            float minZ = -10f;
            float maxZ = 10f;
    
            Vector3 randomPos = new Vector3(
                UnityEngine.Random.Range(boundingBox.min.x, boundingBox.max.x),
                UnityEngine.Random.Range(boundingBox.min.y, boundingBox.max.y),
                UnityEngine.Random.Range(boundingBox.min.z, boundingBox.max.z));
        
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
        
        //Create the eagle
        GameObject eagleInstance = Instantiate(eaglePrefab);
        eagle = new Preditor();
        eagle.gameAgent = eagleInstance;
        
        //Find a spawn position far from all boids
        Vector3 eaglePosition = Vector3.zero;
        bool validPosition = false;
        int maxAttempts = 50;
        int attempts = 0;
        float minSafeDistance = 15f; //Minimum distance from any boid
    
        while (!validPosition && attempts < maxAttempts)
        {
            //Try random position within bounding box
            eaglePosition = new Vector3(
                UnityEngine.Random.Range(boundingBox.min.x, boundingBox.max.x),
                UnityEngine.Random.Range(boundingBox.min.y, boundingBox.max.y),
                UnityEngine.Random.Range(boundingBox.min.z, boundingBox.max.z));
        
            validPosition = true;
        
            //Check distance to all boids
            foreach (Boid boid in boids)
            {
                float distance = Vector3.Distance(eaglePosition, boid.gameAgent.transform.position);
                if (distance < minSafeDistance)
                {
                    validPosition = false;
                    break;
                }
            }
        
            attempts++;
        }
    
        eagleInstance.transform.position = eaglePosition;
    
        Vector3 randomEagleVelocity = new Vector3(
            UnityEngine.Random.Range(-1f, 1f),
            UnityEngine.Random.Range(-1f, 1f),
            UnityEngine.Random.Range(-1f, 1f)).normalized * UnityEngine.Random.Range(eagleMaxSpeed * 0.3f, eagleMaxSpeed * 0.7f);
        eagle.velocity = randomEagleVelocity;
    }

    //Makes the eagle chase the nearest boid
    Vector3 ChaseBoids()
    {
        //Find the closest boid
        float closestDistance = float.MaxValue;
        int closestIndex = -1;
    
        for (int i = 0; i < boids.Count; i++)
        {
            float distance = Vector3.Distance(eagle.gameAgent.transform.position, boids[i].gameAgent.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }
    
        //If no boids found return zero
        if (closestIndex == -1)
        {
            return Vector3.zero;
        }
    
        //Calculate steering force toward the target
        Vector3 desiredDirection = (boids[closestIndex].gameAgent.transform.position - eagle.gameAgent.transform.position).normalized;
        Vector3 desiredVelocity = desiredDirection * maxSpeed;
        Vector3 steeringForce = desiredVelocity - eagle.velocity;
    
        //Limit the steering force
        if (steeringForce.magnitude > maxForce)
        {
            steeringForce = steeringForce.normalized * maxForce;
        }
    
        return steeringForce;        
    }
    

    Vector3 Seperation(int boidIndex)
    {
        List<Boid> neighbors  = new List<Boid>();
        

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

        foreach (Boid b in neighbors)
        {
            float distance = Vector3.Distance(boids[boidIndex].gameAgent.transform.position, b.gameAgent.transform.position);
            
            Vector3 direction = (boids[boidIndex].gameAgent.transform.position - b.gameAgent.transform.position).normalized;
            float magnitude = k / distance;
            
            totalRepulsionForce += direction * magnitude;
        }
        
        // Separate from eagle
        float eagleDistance = Vector3.Distance(boids[boidIndex].gameAgent.transform.position, eagle.gameAgent.transform.position);
        if (eagleDistance < eagleRadius)
        {
            Vector3 direction = (boids[boidIndex].gameAgent.transform.position - eagle.gameAgent.transform.position).normalized;
            // Stronger repulsion from predator - multiply by 2 or 3
            float magnitude = (k * 3f) / eagleDistance;
        
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
        List<Boid> neighbors = new List<Boid>();
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

        foreach (Boid b in neighbors)
        {
            velocitySum += b.velocity;
        }

        Vector3 averageVelocity = velocitySum / neighbors.Count;    
        desiredVelocity = averageVelocity * k;
    
        return desiredVelocity;
    }
    
    Vector3 Cohesion(int boidIndex)
    {
        List<Boid> neighbors  = new List<Boid>();
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
        foreach (Boid b in neighbors)
        {
            centerMass += b.gameAgent.transform.position;
        }
        
        //average it out
        centerMass /= neighbors.Count;
        
        Vector3 forceToCenter = centerMass - boids[boidIndex].gameAgent.transform.position;
        
        return forceToCenter;
    }

}
