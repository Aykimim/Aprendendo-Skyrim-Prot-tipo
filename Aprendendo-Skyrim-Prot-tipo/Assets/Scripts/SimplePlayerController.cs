using UnityEngine;

public class SimplePlayerController : MonoBehaviour
{
    public float speed = 5f;
    public float rotationSpeed = 180f;

    void Update()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 direction = new Vector3(horizontal, 0f, vertical);
        if (direction.magnitude > 0.1f)
        {
            transform.Translate(direction.normalized * speed * Time.deltaTime, Space.World);
            Quaternion toRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, toRotation, rotationSpeed * Time.deltaTime);
        }
    }
}
