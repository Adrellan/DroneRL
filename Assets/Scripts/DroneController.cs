using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class DroneController : Agent
{
    public float speed = 5f;

    private Vector3 initialPosition;

    private enum ACTIONS
    {
        LEFT = 0,
        FORWARD = 1,
        RIGHT = 2,
        BACKWARD = 3,
        UP = 4,
        DOWN = 5
    }

    public override void OnEpisodeBegin()
    {
        initialPosition = new Vector3(-59.0f, 53.0f, -170.0f);
        transform.localPosition = initialPosition;
        Debug.Log("Helló");
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        var actionTaken = actions.DiscreteActions[0];
        var moveDirection = Vector3.zero;
        Debug.Log(actionTaken);

        switch (actionTaken)
        {
            case (int)ACTIONS.FORWARD:
                moveDirection = transform.forward;
                break;
            case (int)ACTIONS.LEFT:
                moveDirection = -transform.right;
                break;
            case (int)ACTIONS.RIGHT:
                moveDirection = transform.right;
                break;
            case (int)ACTIONS.BACKWARD:
                moveDirection = -transform.forward;
                break;
            case (int)ACTIONS.UP:
                moveDirection = transform.up;
                break;
            case (int)ACTIONS.DOWN:
                moveDirection = -transform.up;
                break;
        }

        transform.Translate(moveDirection * speed * Time.fixedDeltaTime);

        if (IsInAir())
        {
            AddReward(0.001f);
        }
        else
        {
            AddReward(-0.05f);
            EndEpisode(); // Azonnal befejezzük az epizódot, ha a drón leér a talajra.
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.localPosition.x);
        sensor.AddObservation(transform.localPosition.y);
        sensor.AddObservation(transform.localPosition.z);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<int> actions = actionsOut.DiscreteActions;

        var horizontal = 0;
        var vertical = 0;
        var ascend = 0;
        var descend = 0;

        if (Input.GetKey(KeyCode.A))
        {
            horizontal = -1;
        }
        else if (Input.GetKey(KeyCode.D))
        {
            horizontal = 1;
        }

        if (Input.GetKey(KeyCode.W))
        {
            vertical = 1;
        }
        else if (Input.GetKey(KeyCode.S))
        {
            vertical = -1;
        }

        if (Input.GetKey(KeyCode.I))
        {
            ascend = 1;
        }
        else if (Input.GetKey(KeyCode.K))
        {
            descend = 1;
        }

        actions[0] = GetAction(horizontal, vertical, ascend, descend);
    }

    private int GetAction(int horizontal, int vertical, int ascend, int descend)
    {
        if (horizontal == -1)
        {
            return (int)ACTIONS.LEFT;
        }
        else if (horizontal == 1)
        {
            return (int)ACTIONS.RIGHT;
        }
        else if (vertical == 1)
        {
            return (int)ACTIONS.FORWARD;
        }
        else if (vertical == -1)
        {
            return (int)ACTIONS.BACKWARD;
        }
        else if (ascend == 1)
        {
            return (int)ACTIONS.UP;
        }
        else if (descend == 1)
        {
            return (int)ACTIONS.DOWN;
        }
        else
        {
            return (int)ACTIONS.FORWARD;
        }
    }

    private bool IsInAir()
    {
        return transform.localPosition.y > 1.0f;
    }

    private void OnCollisionEnter(Collision collision)
    {
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
        else if (collision.collider.CompareTag("Checkpoint"))
        {
            AddReward(1.0f);
        }
    }
}
