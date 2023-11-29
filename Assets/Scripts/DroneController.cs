using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class DroneController : Agent
{
    public float speed = 5f;
    public Transform checkpointsContainer;
    public List<Transform> waypoints;
    public float proximityThreshold = 1.0f;

    private int currentWaypointIndex = 0;

    public override void OnEpisodeBegin()
    {
        transform.localPosition = new Vector3(-59.0f, 53.0f, -170.0f);

        waypoints = new List<Transform>();
        foreach (Transform waypoint in checkpointsContainer)
        {
            waypoints.Add(waypoint);
        }
        Debug.Log("WAYPOINTS COUNT: " + waypoints.Count);

        currentWaypointIndex = 0;
        ResetWaypoints();
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        var continuousActions = actionBuffers.ContinuousActions;

        if (continuousActions.Length >= 3)
        {
            var moveDirection = new Vector3(continuousActions[0], continuousActions[1], continuousActions[2]);

            transform.Translate(moveDirection * speed * Time.fixedDeltaTime);
        }
        else
        {
            Debug.LogError("Invalid number of continuous actions received.");
        }

        if (Vector3.Distance(transform.position, waypoints[currentWaypointIndex].position) < proximityThreshold)
        {
            AddReward(0.1f);

            if (currentWaypointIndex >= waypoints.Count)
            {
                EndEpisode();
                return;
            }

            Collider checkpointCollider = waypoints[currentWaypointIndex].GetComponent<Collider>();
            if (checkpointCollider != null)
            {
                checkpointCollider.isTrigger = true;
                AddReward(1.0f);
                DisableColliderAtIndex(currentWaypointIndex);
            }

            Debug.Log("Current Total Reward: " + GetCumulativeReward());

            ResetWaypoints();
        }

        if (StepCount % 100 == 0)
        {
            AddReward(-0.01f);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.localPosition);

        float distanceToWaypoint = Vector3.Distance(transform.position, waypoints[currentWaypointIndex].position);
        sensor.AddObservation(distanceToWaypoint);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Implement Heuristic control if needed for manual testing.
    }

    private void ResetWaypoints()
    {
        foreach (Transform waypoint in waypoints)
        {
            Collider checkpointCollider = waypoint.GetComponent<Collider>();
            if (checkpointCollider != null)
            {
                checkpointCollider.isTrigger = true;
                waypoint.gameObject.SetActive(true);
            }
        }
    }
    private void DisableColliderAtIndex(int index)
    {
        if (index >= 0 && index < waypoints.Count)
        {
            Collider checkpointCollider = waypoints[index].GetComponent<Collider>();
            if (checkpointCollider != null)
            {
                checkpointCollider.gameObject.SetActive(false);
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log("Collision detected: " + collision.collider.tag);

        if (collision.collider.CompareTag("Wall"))
        {
            AddReward(-1);
            EndEpisode();
        }
        else if (collision.collider.CompareTag("Ground"))
        {
            AddReward(-0.5f);
            EndEpisode();
        }
        else if (collision.collider.CompareTag("Vehicle"))
        {
            AddReward(-0.5f);
            EndEpisode();
        }
        else if (collision.collider.CompareTag("Building"))
        {
            AddReward(-0.5f);
            EndEpisode();
        }
        else if (collision.collider.CompareTag("Checkpoint"))
        {
            int nextCheckpointIndex = currentWaypointIndex + 1;

            // Ellen�rizz�k, hogy a k�vetkez� checkpoint indexe a megl�v� checkpointok k�z�tt van-e
            if (currentWaypointIndex < waypoints.Count - 1)
            {
                // A k�d itt marad v�ltozatlan
                if (currentWaypointIndex == waypoints.IndexOf(collision.transform))
                {
                    AddReward(1.0f);
                    currentWaypointIndex++;

                    Debug.Log("Checkpoint passed: " + collision.collider.name);

                    for (int i = 0; i < currentWaypointIndex; i++)
                    {
                        DisableColliderAtIndex(i);
                    }
                }
            }
            else
            {
                // Ha nincs t�bb checkpoint, akkor befejezz�k az epiz�dot
                EndEpisode();
            }
        }
    }
}
