using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;


public class DroneController : Agent
{
    private Rigidbody rb;  // Rigidbody a fizikai mozgásért
    private List<RayPerceptionSensorComponent3D> rayPerceptionSensors = new List<RayPerceptionSensorComponent3D>();
    private List<GameObject> checkpoints = new List<GameObject>();
    private HashSet<GameObject> rewardedCheckpoints = new HashSet<GameObject>();
    private Vector3 lastCheckpointPosition;

    public float maxDistanceToLastCheckpoint = 100f;
    private GameObject nearestCheckpoint;
    private GameObject lastVisitedCheckpoint;
    private VectorSensor vectorSensor;
    private float currentCheckpointReward = 1.0f;
    private float checkpointRewardIncrease = 0.1f;
    private float timeSinceLastCheckpoint;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rayPerceptionSensors.AddRange(GetComponents<RayPerceptionSensorComponent3D>());
        checkpoints.AddRange(GameObject.FindGameObjectsWithTag("Checkpoint"));
        nearestCheckpoint = FindNearestCheckpoint();
        timeSinceLastCheckpoint = 0f;
    }

    public override void OnEpisodeBegin()
    {
        transform.localPosition = new Vector3(-59.0f, 53.0f, -170.0f);
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        lastVisitedCheckpoint = null;
        rewardedCheckpoints.Clear();
        nearestCheckpoint = FindNearestCheckpoint();

        timeSinceLastCheckpoint = 0f;
    }

    private GameObject FindNearestCheckpoint()
    {
        GameObject nearest = null;
        float minDistance = float.MaxValue;

        foreach (var checkpoint in checkpoints)
        {
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

    public override void OnActionReceived(ActionBuffers actions)
    {
        nearestCheckpoint = FindNearestCheckpoint();
        var continuousActions = actions.ContinuousActions;
        Vector3 moveDirection = new Vector3(continuousActions[0], continuousActions[1], continuousActions[2]);
        rb.transform.Translate(moveDirection * 5f);

        Distance();
        float reward = CalculateReward();
        AddReward(-0.001f);
        AddReward(reward);

        timeSinceLastCheckpoint += Time.fixedDeltaTime;
        CheckRayCasts();
    }

    private void CheckRayCasts()
    {
        foreach (var rayPerceptionSensor in rayPerceptionSensors)
        {
            var rayOutputs = RayPerceptionSensor.Perceive(rayPerceptionSensor.GetRayPerceptionInput()).RayOutputs;

            int lengthOfRayOutputs = rayOutputs.Length;

            for (int i = 0; i < lengthOfRayOutputs; i++)
            {
                GameObject goHit = rayOutputs[i].HitGameObject;

                if (goHit != null && goHit.CompareTag("Checkpoint") && goHit != lastVisitedCheckpoint)
                {
                    //AddReward(0.005f);
                    lastVisitedCheckpoint = goHit;
                    timeSinceLastCheckpoint = 0f;  // Nullázzuk az időt, mert új checkpoint felvétele történt
                }
                else if (goHit != null && (goHit.CompareTag("Wall") || goHit.CompareTag("Building") || goHit.CompareTag("Vehicle") || goHit.CompareTag("Ground")))
                {
                    AddReward(-0.01f);
                }
            }
        }
    }


    public override void CollectObservations(VectorSensor sensor)
    {
        foreach (var rayPerceptionSensor in rayPerceptionSensors)
        {
            var rayOutputs = RayPerceptionSensor.Perceive(rayPerceptionSensor.GetRayPerceptionInput()).RayOutputs;
            foreach (var output in rayOutputs)
            {
                sensor.AddObservation(output.HitFraction);
            }
        }
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

            if (!rewardedCheckpoints.Contains(collidedCheckpoint))
            {
                reward += currentCheckpointReward;
                Debug.Log("JUTALOMPONTOK - CHECKPOINT: " + GetCumulativeReward());
                Debug.Log($"TÁVOLSÁG A KÖVETKEZŐ CHECKPOINTTÓL: {Vector3.Distance(transform.position, nearestCheckpoint.transform.position)}");

                rewardedCheckpoints.Add(collidedCheckpoint);

                nearestCheckpoint = FindNearestCheckpoint();
                Debug.Log("KÖVETKEZŐ CHECKPOINT: " + nearestCheckpoint.name);

                currentCheckpointReward += checkpointRewardIncrease;
            }
        }

        reward -= timeSinceLastCheckpoint >= 5f ? 0.1f : 0f;

        return reward;
    }

    private void Distance()
    {
        float distanceToNextCheckpoint = Vector3.Distance(transform.position, nearestCheckpoint.transform.position);
        if (distanceToNextCheckpoint > maxDistanceToLastCheckpoint)
        {
            Debug.Log("A következõ checkpointtól túl messzire ment a drón. Epizód vége.");
            Debug.Log("DISTANCE!!!!" + distanceToNextCheckpoint + "MAXDISTANCE: " + maxDistanceToLastCheckpoint);
            AddReward(-0.5f);
            EndEpisode();
        }
    }


    private bool IsCollidingWithObstacle()
    {
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
        float distanceToGround = 0.1f;
        return Physics.Raycast(transform.position, Vector3.down, distanceToGround + 0.1f);
    }
}