using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class DroneController : Agent
{
    private Rigidbody rb;  // Rigidbody a fizikai mozg�s�rt

    // Az �rz�kel�k
    private List<RayPerceptionSensorComponent3D> rayPerceptionSensors = new List<RayPerceptionSensorComponent3D>();

    // A dr�n �ltal el�rhet� checkpointok
    private List<GameObject> checkpoints = new List<GameObject>();

    // Az aktu�lis epiz�d sor�n m�r jutalmazott checkpointok
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
            // Ellen�rizd, hogy ez a checkpoint m�g NEM lett felv�ve az aktu�lis epiz�dban
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
    //        // Az anyag cser�je a kijel�lt anyagra
    //        Renderer checkpointRenderer = nearestCheckpoint.GetComponent<Renderer>();
    //        checkpointRenderer.material = highlightMaterial;
    //    }
    //}

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Friss�ts�k a legk�zelebbi checkpointot
        nearestCheckpoint = FindNearestCheckpoint();

        // Kinyerj�k az continuous cselekv�seket
        var continuousActions = actions.ContinuousActions;

        // A continuous cselekv�sek �rt�keit felhaszn�lva v�ltoztatjuk a dr�n poz�ci�j�t
        Vector3 moveDirection = new Vector3(continuousActions[0], continuousActions[1], continuousActions[2]);
        rb.AddForce(moveDirection * 10f);

        Distance();

        // Jutalmak �s b�ntet�sek
        float reward = CalculateReward();
        AddReward(reward);

        // Debug inform�ci�k ki�r�sa
        // Debug.Log($"Dr�n poz�ci�: {transform.position}");
        // Debug.Log($"K�vetkez� checkpoint poz�ci�: {nearestCheckpoint.transform.position}");

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

                // Ha a l�tott objektum egy checkpoint, �s m�g nem volt felv�ve ebben az epiz�dban
                if (goHit != null && goHit.CompareTag("Checkpoint") && goHit != lastVisitedCheckpoint)
                {
                    // Felvette a checkpointot, jutalmazhat� �s ne vegye t�bb� sz�m�t�sba
                    float reward = 1.0f;  // Jutalom be�ll�t�sa, p�lda �rt�k
                    AddReward(reward);
                    lastVisitedCheckpoint = goHit;
                }

                // A tov�bbi �rz�kel�si adatok feldolgoz�sa...
            }
        }
    }


    public override void CollectObservations(VectorSensor sensor)
    {
        // Minden �rz�kel�b�l sz�rmaz� adatok �sszegy�jt�se
        foreach (var rayPerceptionSensor in rayPerceptionSensors)
        {
            var rayOutputs = RayPerceptionSensor.Perceive(rayPerceptionSensor.GetRayPerceptionInput()).RayOutputs;
            foreach (var output in rayOutputs)
            {
                // Az �rz�kel� �ltal �sszegy�jt�tt adatok hozz�ad�sa a megfigyel�shez
                sensor.AddObservation(output.HitFraction);
            }
        }
        // Egy�b megfigyel�sek (ha sz�ks�ges)
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

        // Ellen�rizz�k, hogy a dr�nnak �tk�zik-e valamilyen objektummal
        bool isCollidingWithObstacle = IsCollidingWithObstacle();
        if (isCollidingWithObstacle)
        {
            // Ha nekimegy valamilyen akad�lynak, akkor nagyobb b�ntet�st kap �s az epiz�d v�get �r
            reward -= 1.0f;
            Debug.Log("Jutalompontok - �tk�z�tt: " + GetCumulativeReward());
            EndEpisode();
        }
        else
        {
            // Ha nem �tk�zik, akkor jutalmat kap, ha leveg�ben van �s mozog
            if (!IsGrounded() && rb.velocity.magnitude > 0.1f)
            {
                // Most nem kap jutalmat a rep�l�s�rt
                // reward += 0.01f;
            }
        }

        if (IsCollidingWithCheckpoint())
        {
            GameObject collidedCheckpoint = GetCollidedCheckpoint();

            // Ellen�rizz�k, hogy ez a checkpoint m�r jutalmazva lett-e az aktu�lis epiz�d sor�n
            if (!rewardedCheckpoints.Contains(collidedCheckpoint))
            {
                reward += 5.0f;
                Debug.Log("JUTALOMPONTOK - CHECKPOINT: " + GetCumulativeReward());
                Debug.Log($"T�VOLS�G A K�VETKEZ� CHECKPOINTT�L: {Vector3.Distance(transform.position, nearestCheckpoint.transform.position)}");
                Debug.Log($"K�VETKEZ� CHECKPOINT POZI: {nearestCheckpoint.transform.position}");

                rewardedCheckpoints.Add(collidedCheckpoint);

                // Ki�rjuk a legk�zelebbi checkpointot
                nearestCheckpoint = FindNearestCheckpoint();
                Debug.Log("Legk�zelebbi checkpoint most: " + nearestCheckpoint.name);
            }
        }

        // Kevesebb b�ntet�s az id� m�l�s�val
        reward -= Time.fixedDeltaTime * 0.5f;  // P�lda �rt�k, �ll�tsd be aszerint, amilyen nagys�grendben b�ntetni szeretn�d

        return reward;
    }

    private void Distance()
    {
        float distanceToNextCheckpoint = Vector3.Distance(transform.position, nearestCheckpoint.transform.position);
        if (distanceToNextCheckpoint > maxDistanceToLastCheckpoint)
        {
            Debug.Log("A k�vetkez� checkpointt�l t�l messzire ment a dr�n. Epiz�d v�ge.");
            Debug.Log("DISTANCE!!!!" + distanceToNextCheckpoint + "MAXDISTANCE: " + maxDistanceToLastCheckpoint);
            EndEpisode();
        }
    }


    private bool IsCollidingWithObstacle()
    {
        // Ellen�rizz�k, hogy a dr�nnak �tk�zik-e olyan objektummal, amelynek a tag-jei: "Wall", "Building", "Vehicle", "Ground"
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
        // Ellen�rizz�k, hogy a dr�nnak �tk�zik-e egy "Checkpoint" objektummal
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
        // Ellen�rizz�k, hogy a dr�nnak �tk�zik-e egy "Checkpoint" objektummal, �s visszaadjuk azt
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
        // Ellen�rizz�k, hogy a dr�n a talajon van-e
        float distanceToGround = 0.1f;
        return Physics.Raycast(transform.position, Vector3.down, distanceToGround + 0.1f);
    }

    //private bool IsOutOfBounds()
    //{
    //    // P�lda: Ha a dr�n t�l messze van a k�z�ppontt�l, akkor kiesett a ter�letr�l
    //    return Vector3.Distance(transform.position, Vector3.zero) > 40f;
    //}

    public override void OnEpisodeBegin()
    {
        // Az epiz�d kezdetekor v�grehajtand� inicializ�ci� (ha sz�ks�ges)
        transform.localPosition = new Vector3(-59.0f, 53.0f, -170.0f);
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        lastVisitedCheckpoint = null;

        // Az aktu�lis epiz�d sor�n m�r jutalmazott checkpointokat �r�tj�k
        rewardedCheckpoints.Clear();

        nearestCheckpoint = FindNearestCheckpoint();
        Debug.Log("Legk�zelebbi checkpoint az epiz�d kezdet�n: " + nearestCheckpoint.name);
    }
}
