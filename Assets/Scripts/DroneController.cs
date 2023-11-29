using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class DroneController : Agent
{
    private Rigidbody rb;  // Rigidbody a fizikai mozgásért

    // Az érzékelõk
    private List<RayPerceptionSensorComponent3D> rayPerceptionSensors = new List<RayPerceptionSensorComponent3D>();

    // A drón által elérhetõ checkpointok
    private List<GameObject> checkpoints = new List<GameObject>();

    // Az aktuális epizód során már jutalmazott checkpointok
    private HashSet<GameObject> rewardedCheckpoints = new HashSet<GameObject>();

    private Vector3 lastCheckpointPosition;

    public float maxDistanceToLastCheckpoint = 100f;

    private GameObject nearestCheckpoint;

    public Material highlightMaterial;

    private GameObject lastVisitedCheckpoint;

    private VectorSensor vectorSensor;


    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rayPerceptionSensors.AddRange(GetComponents<RayPerceptionSensorComponent3D>());
        checkpoints.AddRange(GameObject.FindGameObjectsWithTag("Checkpoint"));
        nearestCheckpoint = FindNearestCheckpoint();
    }

    private GameObject FindNearestCheckpoint()
    {
        GameObject nearest = null;
        float minDistance = float.MaxValue;

        foreach (var checkpoint in checkpoints)
        {
            // Ellenõrizd, hogy ez a checkpoint még NEM lett felvéve az aktuális epizódban
            if (!rewardedCheckpoints.Contains(checkpoint))
            {
                float distance = Vector3.Distance(transform.position, checkpoint.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = checkpoint;
                }
            }
        }

        return nearest;
    }


    //private void HighlightNearestCheckpoint()
    //{
    //    if (highlightMaterial != null && nearestCheckpoint != null)
    //    {
    //        // Az anyag cseréje a kijelölt anyagra
    //        Renderer checkpointRenderer = nearestCheckpoint.GetComponent<Renderer>();
    //        checkpointRenderer.material = highlightMaterial;
    //    }
    //}

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Frissítsük a legközelebbi checkpointot
        nearestCheckpoint = FindNearestCheckpoint();

        // Kinyerjük az continuous cselekvéseket
        var continuousActions = actions.ContinuousActions;

        // A continuous cselekvések értékeit felhasználva változtatjuk a drón pozícióját
        Vector3 moveDirection = new Vector3(continuousActions[0], continuousActions[1], continuousActions[2]);
        rb.AddForce(moveDirection * 10f);

        Distance();

        // Jutalmak és büntetések
        float reward = CalculateReward();
        AddReward(reward);

        // Debug információk kiírása
        // Debug.Log($"Drón pozíció: {transform.position}");
        // Debug.Log($"Következõ checkpoint pozíció: {nearestCheckpoint.transform.position}");

        CheckRayCasts();
    }



    private void CheckRayCasts()
    {
        foreach (var rayPerceptionSensor in rayPerceptionSensors)
        {
            var rayOutputs = RayPerceptionSensor.Perceive(rayPerceptionSensor.GetRayPerceptionInput()).RayOutputs;

            int lengthOfRayOutputs = rayOutputs.Length;

            // Alternating Ray Order: it gives an order of
            // (0, -delta, delta, -2delta, 2delta, ..., -ndelta, ndelta)
            // index 0 indicates the center of raycasts
            for (int i = 0; i < lengthOfRayOutputs; i++)
            {
                GameObject goHit = rayOutputs[i].HitGameObject;

                // Ha a látott objektum egy checkpoint, és még nem volt felvéve ebben az epizódban
                if (goHit != null && goHit.CompareTag("Checkpoint") && goHit != lastVisitedCheckpoint)
                {
                    // Felvette a checkpointot, jutalmazható és ne vegye többé számításba
                    float reward = 1.0f;  // Jutalom beállítása, példa érték
                    AddReward(reward);
                    lastVisitedCheckpoint = goHit;
                }

                // A további érzékelési adatok feldolgozása...
            }
        }
    }


    public override void CollectObservations(VectorSensor sensor)
    {
        // Minden érzékelõbõl származó adatok összegyûjtése
        foreach (var rayPerceptionSensor in rayPerceptionSensors)
        {
            var rayOutputs = RayPerceptionSensor.Perceive(rayPerceptionSensor.GetRayPerceptionInput()).RayOutputs;
            foreach (var output in rayOutputs)
            {
                // Az érzékelõ által összegyûjtött adatok hozzáadása a megfigyeléshez
                sensor.AddObservation(output.HitFraction);
            }
        }
        // Egyéb megfigyelések (ha szükséges)
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxis("Horizontal");  // Example: Map the horizontal input to the first continuous action
        continuousActions[1] = Input.GetAxis("Vertical");    // Example: Map the vertical input to the second continuous action
        continuousActions[2] = Input.GetKey(KeyCode.Space) ? 1f : 0f;  // Example: Map the space key to the third continuous action
    }

    private float CalculateReward()
    {
        float reward = 0f;

        // Ellenõrizzük, hogy a drónnak ütközik-e valamilyen objektummal
        bool isCollidingWithObstacle = IsCollidingWithObstacle();
        if (isCollidingWithObstacle)
        {
            // Ha nekimegy valamilyen akadálynak, akkor nagyobb büntetést kap és az epizód véget ér
            reward -= 1.0f;
            Debug.Log("Jutalompontok - ütközött: " + GetCumulativeReward());
            EndEpisode();
        }
        else
        {
            // Ha nem ütközik, akkor jutalmat kap, ha levegõben van és mozog
            if (!IsGrounded() && rb.velocity.magnitude > 0.1f)
            {
                // Most nem kap jutalmat a repülésért
                // reward += 0.01f;
            }
        }

        if (IsCollidingWithCheckpoint())
        {
            GameObject collidedCheckpoint = GetCollidedCheckpoint();

            // Ellenõrizzük, hogy ez a checkpoint már jutalmazva lett-e az aktuális epizód során
            if (!rewardedCheckpoints.Contains(collidedCheckpoint))
            {
                reward += 5.0f;
                Debug.Log("JUTALOMPONTOK - CHECKPOINT: " + GetCumulativeReward());
                Debug.Log($"TÁVOLSÁG A KÖVETKEZÕ CHECKPOINTTÓL: {Vector3.Distance(transform.position, nearestCheckpoint.transform.position)}");
                Debug.Log($"KÖVETKEZÕ CHECKPOINT POZI: {nearestCheckpoint.transform.position}");

                rewardedCheckpoints.Add(collidedCheckpoint);

                // Kiírjuk a legközelebbi checkpointot
                nearestCheckpoint = FindNearestCheckpoint();
                Debug.Log("Legközelebbi checkpoint most: " + nearestCheckpoint.name);
            }
        }

        // Kevesebb büntetés az idõ múlásával
        reward -= Time.fixedDeltaTime * 0.5f;  // Példa érték, állítsd be aszerint, amilyen nagyságrendben büntetni szeretnéd

        return reward;
    }

    private void Distance()
    {
        float distanceToNextCheckpoint = Vector3.Distance(transform.position, nearestCheckpoint.transform.position);
        if (distanceToNextCheckpoint > maxDistanceToLastCheckpoint)
        {
            Debug.Log("A következõ checkpointtól túl messzire ment a drón. Epizód vége.");
            Debug.Log("DISTANCE!!!!" + distanceToNextCheckpoint + "MAXDISTANCE: " + maxDistanceToLastCheckpoint);
            EndEpisode();
        }
    }


    private bool IsCollidingWithObstacle()
    {
        // Ellenõrizzük, hogy a drónnak ütközik-e olyan objektummal, amelynek a tag-jei: "Wall", "Building", "Vehicle", "Ground"
        Collider[] colliders = Physics.OverlapSphere(transform.position, 1f);
        foreach (var collider in colliders)
        {
            if (collider.CompareTag("Wall") || collider.CompareTag("Building") || collider.CompareTag("Vehicle") || collider.CompareTag("Ground"))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsCollidingWithCheckpoint()
    {
        // Ellenõrizzük, hogy a drónnak ütközik-e egy "Checkpoint" objektummal
        Collider[] colliders = Physics.OverlapSphere(transform.position, 1f);
        foreach (var collider in colliders)
        {
            if (collider.CompareTag("Checkpoint"))
            {
                return true;
            }
        }

        return false;
    }

    private GameObject GetCollidedCheckpoint()
    {
        // Ellenõrizzük, hogy a drónnak ütközik-e egy "Checkpoint" objektummal, és visszaadjuk azt
        Collider[] colliders = Physics.OverlapSphere(transform.position, 1f);
        foreach (var collider in colliders)
        {
            if (collider.CompareTag("Checkpoint"))
            {
                return collider.gameObject;
            }
        }

        return null;
    }

    private bool IsGrounded()
    {
        // Ellenõrizzük, hogy a drón a talajon van-e
        float distanceToGround = 0.1f;
        return Physics.Raycast(transform.position, Vector3.down, distanceToGround + 0.1f);
    }

    //private bool IsOutOfBounds()
    //{
    //    // Példa: Ha a drón túl messze van a középponttól, akkor kiesett a területrõl
    //    return Vector3.Distance(transform.position, Vector3.zero) > 40f;
    //}

    public override void OnEpisodeBegin()
    {
        // Az epizód kezdetekor végrehajtandó inicializáció (ha szükséges)
        transform.localPosition = new Vector3(-59.0f, 53.0f, -170.0f);
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        lastVisitedCheckpoint = null;

        // Az aktuális epizód során már jutalmazott checkpointokat ürítjük
        rewardedCheckpoints.Clear();

        nearestCheckpoint = FindNearestCheckpoint();
        Debug.Log("Legközelebbi checkpoint az epizód kezdetén: " + nearestCheckpoint.name);
    }
}
