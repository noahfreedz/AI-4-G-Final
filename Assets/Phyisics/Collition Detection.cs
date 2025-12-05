using Unity.Mathematics;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    void Start()
    {

    }

    void Update()
    {

    }
}

public enum ColliderType
{
    Sphere = 0,
    Plane,
    Box
}

public class Collider
{
    protected ColliderType type;
    protected RigidBody pBody;
    protected float4x4 offset;
    protected float4x4 transform;
    protected int overlapCount;

    public Collider(ColliderType t)
    {
        type = t;  // Fixed: was assigning type to type instead of t to type
        pBody = null;
        overlapCount = 0;
        offset = float4x4.identity;
        transform = float4x4.identity;
    }

    public void SetBody(RigidBody body) { pBody = body; }
    public RigidBody GetBody() { return pBody; }

    public void SetOffset(float4x4 off) { offset = off; }
    public float4x4 GetTransform() { return transform; }

    public ColliderType GetType() { return type; }
    public int GetOverlapCount() { return overlapCount; }
    public void ResetOverlapCount() { overlapCount = 0; }
    public void IncrementOverlapCount() { overlapCount++; }

    public void UpdateInternals()
    {
        if (pBody != null)
        {
            transform = math.mul(pBody.transformMatrix, offset);
        }
        else
        {
            transform = offset;
        }
    }

    public float3 GetAxis(int index)
    {
        return new float3(transform[index].x, transform[index].y, transform[index].z);
    }
}

public class SphereCollider : Collider
{
    public float radius;

    public SphereCollider(float r) : base(ColliderType.Sphere)
    {
        radius = r;
    }
}

public class PlaneCollider : Collider
{
    public float3 normal;
    public float offset;

    public PlaneCollider(float3 n, float o) : base(ColliderType.Plane)
    {
        normal = n;
        offset = o;
    }
}

public class BoxCollider : Collider
{
    private float3 halfSize;

    public BoxCollider(float3 hs) : base(ColliderType.Box)
    {
        halfSize = hs;
    }

    public float3 GetHalfSize() { return halfSize; }
}

public static class IntersectionTests
{
    public static bool TestCollision(Collider a, Collider b)
    {
        ColliderType typeA = a.GetType();
        ColliderType typeB = b.GetType();

        if (typeA == ColliderType.Sphere && typeB == ColliderType.Sphere)
            return SphereSphere((SphereCollider)a, (SphereCollider)b);
        if (typeA == ColliderType.Sphere && typeB == ColliderType.Plane)
            return SpherePlane((SphereCollider)a, (PlaneCollider)b);
        if (typeA == ColliderType.Plane && typeB == ColliderType.Sphere)
            return SpherePlane((SphereCollider)b, (PlaneCollider)a);
        if (typeA == ColliderType.Sphere && typeB == ColliderType.Box)
            return SphereBox((SphereCollider)a, (BoxCollider)b);
        if (typeA == ColliderType.Box && typeB == ColliderType.Sphere)
            return SphereBox((SphereCollider)b, (BoxCollider)a);
        if (typeA == ColliderType.Box && typeB == ColliderType.Box)
            return BoxBox((BoxCollider)a, (BoxCollider)b);
        if (typeA == ColliderType.Box && typeB == ColliderType.Plane)
            return BoxPlane((BoxCollider)a, (PlaneCollider)b);
        if (typeA == ColliderType.Plane && typeB == ColliderType.Box)
            return BoxPlane((BoxCollider)b, (PlaneCollider)a);

        return false;
    }

    public static bool SphereSphere(SphereCollider a, SphereCollider b)
    {
        float3 posA = new float3(a.GetTransform().c3.x, a.GetTransform().c3.y, a.GetTransform().c3.z);
        float3 posB = new float3(b.GetTransform().c3.x, b.GetTransform().c3.y, b.GetTransform().c3.z);
        float3 delta = posA - posB;
        float distSq = math.lengthsq(delta);
        float radiusSum = a.radius + b.radius;
        return distSq < radiusSum * radiusSum;
    }

    public static bool SpherePlane(SphereCollider sphere, PlaneCollider plane)
    {
        float3 spherePos = new float3(sphere.GetTransform().c3.x, sphere.GetTransform().c3.y, sphere.GetTransform().c3.z);
        float dist = math.dot(plane.normal, spherePos) - plane.offset;
        return math.abs(dist) <= sphere.radius;
    }

    public static bool SphereBox(SphereCollider sphere, BoxCollider box)
    {
        float3 spherePos = new float3(sphere.GetTransform().c3.x, sphere.GetTransform().c3.y, sphere.GetTransform().c3.z);
        float3 boxPos = new float3(box.GetTransform().c3.x, box.GetTransform().c3.y, box.GetTransform().c3.z);
        float3 relPos = spherePos - boxPos;

        float3 closestPoint = float3.zero;
        for (int i = 0; i < 3; i++)
        {
            float3 axis = box.GetAxis(i);
            float dist = math.dot(relPos, axis);
            dist = math.clamp(dist, -box.GetHalfSize()[i], box.GetHalfSize()[i]);
            closestPoint += dist * axis;
        }

        closestPoint += boxPos;
        float3 delta = closestPoint - spherePos;
        return math.lengthsq(delta) < sphere.radius * sphere.radius;
    }

    public static bool BoxBox(BoxCollider a, BoxCollider b)
    {
        float3 toCentre = new float3(b.GetTransform().c3.x - a.GetTransform().c3.x,
                                      b.GetTransform().c3.y - a.GetTransform().c3.y,
                                      b.GetTransform().c3.z - a.GetTransform().c3.z);

        return (
            OverlapOnAxis(a, b, a.GetAxis(0), toCentre) &&
            OverlapOnAxis(a, b, a.GetAxis(1), toCentre) &&
            OverlapOnAxis(a, b, a.GetAxis(2), toCentre) &&
            OverlapOnAxis(a, b, b.GetAxis(0), toCentre) &&
            OverlapOnAxis(a, b, b.GetAxis(1), toCentre) &&
            OverlapOnAxis(a, b, b.GetAxis(2), toCentre) &&
            OverlapOnAxis(a, b, math.cross(a.GetAxis(0), b.GetAxis(0)), toCentre) &&
            OverlapOnAxis(a, b, math.cross(a.GetAxis(0), b.GetAxis(1)), toCentre) &&
            OverlapOnAxis(a, b, math.cross(a.GetAxis(0), b.GetAxis(2)), toCentre) &&
            OverlapOnAxis(a, b, math.cross(a.GetAxis(1), b.GetAxis(0)), toCentre) &&
            OverlapOnAxis(a, b, math.cross(a.GetAxis(1), b.GetAxis(1)), toCentre) &&
            OverlapOnAxis(a, b, math.cross(a.GetAxis(1), b.GetAxis(2)), toCentre) &&
            OverlapOnAxis(a, b, math.cross(a.GetAxis(2), b.GetAxis(0)), toCentre) &&
            OverlapOnAxis(a, b, math.cross(a.GetAxis(2), b.GetAxis(1)), toCentre) &&
            OverlapOnAxis(a, b, math.cross(a.GetAxis(2), b.GetAxis(2)), toCentre)
        );
    }

    public static bool BoxPlane(BoxCollider box, PlaneCollider plane)
    {
        float3 boxPos = new float3(box.GetTransform().c3.x, box.GetTransform().c3.y, box.GetTransform().c3.z);
        float projectedRadius = SizeAlongAxis(box, plane.normal);
        float boxDistance = math.dot(plane.normal, boxPos) - plane.offset;
        return math.abs(boxDistance) <= projectedRadius;
    }

    private static float SizeAlongAxis(BoxCollider box, float3 axis)
    {
        float3 halfSize = box.GetHalfSize();
        return
            halfSize.x * math.abs(math.dot(axis, box.GetAxis(0))) +
            halfSize.y * math.abs(math.dot(axis, box.GetAxis(1))) +
            halfSize.z * math.abs(math.dot(axis, box.GetAxis(2)));
    }

    private static bool OverlapOnAxis(BoxCollider one, BoxCollider two, float3 axis, float3 toCenter)
    {
        if (math.lengthsq(axis) < 0.01f)
            return true;

        float oneProject = SizeAlongAxis(one, axis);
        float twoProject = SizeAlongAxis(two, axis);
        float distance = math.abs(math.dot(toCenter, axis));
        return distance <= oneProject + twoProject;
    }
}

public struct Contact
{
    public float3 point;
    public float3 normal;
    public float penetration;
    public RigidBody[] body;
}

public class CollisionData
{
    public Contact[] contactArray;
    public int contactCount;
    public int contactsLeft;
    private int currentIndex;

    public void Reset(int maxContacts)
    {
        contactCount = 0;
        contactsLeft = maxContacts;
        currentIndex = 0;
        contactArray = new Contact[maxContacts];
    }

    public void AddContacts(int num)
    {
        contactsLeft -= num;
        contactCount += num;
        currentIndex += num;
    }

    public ref Contact GetCurrentContact()
    {
        return ref contactArray[currentIndex];
    }
}

public static class ContactResolver
{
    public static void ResolveContacts(Contact[] contacts, int contactCount, float restitution = 0.8f)
    {
        // First resolve interpenetration for all contacts
        for (int i = 0; i < contactCount; i++)
        {
            ResolveInterpenetration(ref contacts[i]);
        }

        // Then resolve velocity for all contacts
        for (int i = 0; i < contactCount; i++)
        {
            ResolveVelocity(ref contacts[i], restitution);
        }
    }

    private static void ResolveInterpenetration(ref Contact c)
    {
        float d = c.penetration;
        if (d <= 0) return;

        float invA = c.body[0] != null ? c.body[0].inverseMass : 0.0f;
        float invB = c.body[1] != null ? c.body[1].inverseMass : 0.0f;

        float totalInv = invA + invB;
        if (totalInv <= 0) return;

        // Slop and beta for Baumgarte stabilization
        const float slop = 0.01f;
        const float beta = 0.4f;
        d = math.max(0.0f, d - slop) * beta;
        if (d <= 0) return;

        float3 movePerInv = c.normal * (d / totalInv);

        // Handle case where body[1] is null (static object like plane)
        if (c.body[1] == null)
        {
            if (invA > 0)
            {
                c.body[0].position += (Vector3)(movePerInv * invA);
                c.body[0].calculateDerivedData();
            }
            return;
        }

        // Handle case where body[0] is null
        if (c.body[0] == null)
        {
            if (invB > 0)
            {
                c.body[1].position -= (Vector3)(movePerInv * invB);
                c.body[1].calculateDerivedData();
            }
            return;
        }

        // Both bodies exist
        if (invA > 0)
        {
            c.body[0].position -= (Vector3)(movePerInv * invA);
            c.body[0].calculateDerivedData();
        }
        if (invB > 0)
        {
            c.body[1].position += (Vector3)(movePerInv * invB);
            c.body[1].calculateDerivedData();
        }
    }

    private static void ResolveVelocity(ref Contact contact, float restitution)
    {
        float invA = contact.body[0] != null ? contact.body[0].inverseMass : 0.0f;
        float invB = contact.body[1] != null ? contact.body[1].inverseMass : 0.0f;
        float invSum = invA + invB;
        if (invSum <= 0) return;

        float3 vA = contact.body[0] != null ? (float3)contact.body[0].velocity : float3.zero;
        float3 vB = contact.body[1] != null ? (float3)contact.body[1].velocity : float3.zero;
        float3 relV = vA - vB;

        float sepVel = math.dot(relV, contact.normal);
        if (sepVel > 0) return; // Already separating

        // Restitution threshold - don't bounce slow collisions
        const float restThreshold = 1.0f;
        float e = math.abs(sepVel) < restThreshold ? 0.0f : restitution;

        float newSepVel = -sepVel * e;
        float deltaVel = newSepVel - sepVel;

        float j = deltaVel / invSum;
        float3 impulse = contact.normal * j;

        if (invA > 0 && contact.body[0] != null)
        {
            contact.body[0].velocity = (Vector3)(vA + impulse * invA);
        }
        if (invB > 0 && contact.body[1] != null)
        {
            contact.body[1].velocity = (Vector3)(vB - impulse * invB);
        }
    }
}

public static class CollisionTests
{
    public static int SphereSphere(SphereCollider a, SphereCollider b, CollisionData data)
    {
        a.UpdateInternals();
        b.UpdateInternals();

        float3 pa = new float3(a.GetTransform().c3.x, a.GetTransform().c3.y, a.GetTransform().c3.z);
        float3 pb = new float3(b.GetTransform().c3.x, b.GetTransform().c3.y, b.GetTransform().c3.z);

        float3 direction = pb - pa;
        float centerDistance = math.length(direction);
        float radiusSum = a.radius + b.radius;

        if (centerDistance >= radiusSum)
        {
            return 0;
        }

        float3 normal;
        if (centerDistance < 1e-6f)
        {
            normal = new float3(1, 0, 0);
        }
        else
        {
            normal = direction / centerDistance;
        }

        float penetration = radiusSum - centerDistance;
        float3 contactPoint = pa + normal * a.radius;

        ContactSetup setup = new ContactSetup(normal, contactPoint, penetration);
        SetContacts(a, b, data, setup);
        return 1;
    }

    public static int SphereHalfspace(SphereCollider sphere, PlaneCollider plane, CollisionData data)
    {
        if (sphere.GetBody() != null) sphere.UpdateInternals();

        float3 sphereCenter = new float3(sphere.GetTransform().c3.x, sphere.GetTransform().c3.y, sphere.GetTransform().c3.z);
        float3 normal = plane.normal;
        float distance = math.dot(sphereCenter, normal) - plane.offset;
        float penetration = sphere.radius - distance;

        if (penetration > 0)
        {
            ContactSetup setup = new ContactSetup(normal, sphereCenter - normal * sphere.radius, penetration);
            SetContacts(sphere, plane, data, setup);
            return 1;
        }
        return 0;
    }

    public static int SphereTruePlane(SphereCollider sphere, PlaneCollider plane, CollisionData data)
    {
        if (sphere.GetBody() != null) sphere.UpdateInternals();

        float3 center = new float3(sphere.GetTransform().c3.x, sphere.GetTransform().c3.y, sphere.GetTransform().c3.z);
        float dist = math.dot(center, plane.normal) - plane.offset;
        float3 normal = dist < 0 ? -plane.normal : plane.normal;
        float penetration = sphere.radius - math.abs(dist);

        if (penetration > 0)
        {
            ContactSetup setup = new ContactSetup(normal, center - normal * sphere.radius, penetration);
            SetContacts(sphere, plane, data, setup);
            return 1;
        }
        return 0;
    }

    private struct ContactSetup
    {
        public float3 normal;
        public float3 point;
        public float penetration;

        public ContactSetup(float3 _normal, float3 _point, float _penetration)
        {
            normal = _normal;
            point = _point;
            penetration = _penetration;
        }
    }

    private static void SetContacts(Collider a, Collider b, CollisionData data, ContactSetup setup)
    {
        if (data.contactsLeft <= 0) return;

        ref Contact c = ref data.GetCurrentContact();
        c.normal = setup.normal;
        c.point = setup.point;
        c.penetration = setup.penetration;
        c.body = new RigidBody[2] { a.GetBody(), b.GetBody() };

        data.AddContacts(1);
    }
}