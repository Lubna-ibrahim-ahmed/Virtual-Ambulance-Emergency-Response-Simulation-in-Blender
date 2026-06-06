using System.Collections;
using UnityEngine;

public class SimplePatientFall : MonoBehaviour
{
    [Header("Fall Settings")]
    public Transform fallenPoint;
    public float fallDuration = 0.8f;

    [Header("Rotation When Fallen")]
    public Vector3 fallenRotation = new Vector3(90f, 0f, 0f);

    private bool hasFallen = false;

    public void Fall()
    {
        if (hasFallen) return;
        hasFallen = true;

        StartCoroutine(FallRoutine());
    }

    private IEnumerator FallRoutine()
    {
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;

        Vector3 endPosition = fallenPoint.position;
        Quaternion endRotation = Quaternion.Euler(fallenRotation);

        float timer = 0f;

        while (timer < fallDuration)
        {
            timer += Time.deltaTime;
            float t = timer / fallDuration;

            transform.position = Vector3.Lerp(startPosition, endPosition, t);
            transform.rotation = Quaternion.Slerp(startRotation, endRotation, t);

            yield return null;
        }

        transform.position = endPosition;
        transform.rotation = endRotation;
    }
}