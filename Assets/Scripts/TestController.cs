using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestController : MonoBehaviour
{
    public float speed = 5f;
    public float rotationSpeed = 3f;
    public float continuousUpForce = 5f;
    public float continuousDownForce = 5f; // Új változó a folyamatos lefelé mozgáshoz

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        // Mozgás vezérlése
        float moveHorizontal = Input.GetAxis("Horizontal");
        float moveVertical = Input.GetAxis("Vertical");
        Vector3 movement = new Vector3(moveHorizontal, 0f, moveVertical) * speed * Time.deltaTime;
        transform.Translate(movement);

        // Forgás vezérlése az I és L billentyûkkel
        float rotateAmount = 0f;
        if (Input.GetKey(KeyCode.J))
        {
            rotateAmount = -rotationSpeed;
        }
        else if (Input.GetKey(KeyCode.L))
        {
            rotateAmount = rotationSpeed;
        }

        Vector3 rotation = new Vector3(0f, rotateAmount, 0f);
        transform.Rotate(rotation);

        // Folyamatos felfelé mozgás
        if (Input.GetKey(KeyCode.I))
        {
            rb.AddForce(Vector3.up * continuousUpForce);
        }

        // Folyamatos lefelé mozgás
        if (Input.GetKey(KeyCode.K))
        {
            rb.AddForce(Vector3.down * continuousDownForce);
        }

        // Korlátozás a földön tartózkodáshoz
        transform.position = new Vector3(transform.position.x, Mathf.Max(transform.position.y, 0.1f), transform.position.z);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Checkpoint"))
        {
            Debug.Log("Ügyes vagy");
        }
    }
}