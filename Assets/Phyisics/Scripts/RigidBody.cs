using UnityEngine;
using Unity.Mathematics;

public static class QuaternionExtensions
{
    public static Quaternion AddScaledVector(this Quaternion q, Vector3 v, float scale)
    {
        Quaternion qv = new Quaternion(v.x * scale, v.y * scale, v.z * scale, 0);
        Quaternion rq = q * qv;
        q.x += 0.5f * rq.x;
        q.y += 0.5f * rq.y;
        q.z += 0.5f * rq.z;
        q.w += 0.5f * rq.w;
        return q;
    }
}

public class RigidBody : MonoBehaviour
{
    public Vector3 position;
    public Vector3 velocity;
    public Vector3 acceleration;
    public float linearDamping;
    public float inverseMass;
    public Vector3 forceAccum;
    public Quaternion orientation;
    public Vector3 angularVelocity;
    public Vector3 torqueAccum;
    public float3x3 inverseInertiaTensor;
    public float angularDamping;
    public float3x3 inverseInertiaTensorWorld;
    public float4x4 transformMatrix;

    static void _calculateTransformMatrix(
        ref float4x4 transformMatrix,
        Vector3 position,
        Quaternion orientation)
    {
        float x = orientation.x;
        float y = orientation.y;
        float z = orientation.z;
        float w = orientation.w;
        float xx = x * x;
        float yy = y * y;
        float zz = z * z;
        float xy = x * y;
        float xz = x * z;
        float yz = y * z;
        float wx = w * x;
        float wy = w * y;
        float wz = w * z;

        transformMatrix.c0 = new float4(
            1 - 2 * (yy + zz),
            2 * (xy + wz),
            2 * (xz - wy),
            0);
        transformMatrix.c1 = new float4(
            2 * (xy - wz),
            1 - 2 * (xx + zz),
            2 * (yz + wx),
            0);
        transformMatrix.c2 = new float4(
            2 * (xz + wy),
            2 * (yz - wx),
            1 - 2 * (xx + yy),
            0);
        transformMatrix.c3 = new float4(
            position.x,
            position.y,
            position.z,
            1);
    }

    public void integrate(float deltaTime)
    {
        Vector3 linearAcceleration = forceAccum * inverseMass;
        velocity += linearAcceleration * deltaTime;
        velocity *= Mathf.Pow(linearDamping, deltaTime);
        position += velocity * deltaTime;

        float3 angAcc3 = math.mul(inverseInertiaTensorWorld, (float3)torqueAccum);
        Vector3 angularAcceleration = new Vector3(angAcc3.x, angAcc3.y, angAcc3.z);
        angularVelocity += angularAcceleration * deltaTime;
        angularVelocity *= Mathf.Pow(angularDamping, deltaTime);
        orientation = QuaternionExtensions.AddScaledVector(orientation, angularVelocity, deltaTime);

        calculateDerivedData();
        clearAccumulators();
    }

    public void calculateDerivedData()
    {
        orientation = Quaternion.Normalize(orientation);
        _calculateTransformMatrix(ref transformMatrix, position, orientation);
        float3x3 rot = new float3x3(orientation);
        inverseInertiaTensorWorld = math.mul(rot, inverseInertiaTensor);
    }

    public void addForce(Vector3 force)
    {
        forceAccum += force;
    }

    public void addTorque(Vector3 torque)
    {
        torqueAccum += torque;
    }

    public void addForceAtPoint(Vector3 force, Vector3 point)
    {
        Vector3 pt = point - position;
        forceAccum += force;
        torqueAccum += Vector3.Cross(pt, force);
    }

    void clearAccumulators()
    {
        forceAccum = Vector3.zero;
        torqueAccum = Vector3.zero;
    }
}