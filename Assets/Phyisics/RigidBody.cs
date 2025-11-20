using UnityEngine;


public class RigidBody : MonoBehaviour
{
    private Vector3 position;
    private Vector3 velocity;
    private Vector3 acceleration;
    private float linearDamping;
    private float inverseMass;
    private Vector3 forceAccum;
    private Quaternion orientation;
    private Vector3 angularVelocity;
    private Vector3 torqueAccum;
    private Matrix3x2 inverseInertiaTensor;
    private float angularDamping;
    private Matrix3x2 inverseInertiaTensorWorld;
    private Matrix4x4 transformMatrix;
}
