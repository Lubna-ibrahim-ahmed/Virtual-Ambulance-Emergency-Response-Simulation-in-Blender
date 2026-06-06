using System.Collections;
using UnityEngine;

public class WitnessReaction : MonoBehaviour
{
    [Header("Movement")]
    public Transform backPoint;
    public float stepBackDuration = 1f;

    [Header("Phone")]
    public GameObject phoneObject;

    private bool hasReacted = false;

    public void ReactToExplosion()
    {
        if (hasReacted) return;
        hasReacted = true;

        StartCoroutine(ReactionRoutine());
    }

    private IEnumerator ReactionRoutine()
    {
        Vector3 startPosition = transform.position;
        Vector3 endPosition = backPoint.position;

        Quaternion startRotation = transform.rotation;

        Vector3 directionAway = endPosition - startPosition;
        directionAway.y = 0;

        Quaternion endRotation = startRotation;

        if (directionAway != Vector3.zero)
        {
            endRotation = Quaternion.LookRotation(directionAway);
        }

        float timer = 0f;

        while (timer < stepBackDuration)
        {
            timer += Time.deltaTime;
            float t = timer / stepBackDuration;

            transform.position = Vector3.Lerp(startPosition, endPosition, t);
            transform.rotation = Quaternion.Slerp(startRotation, endRotation, t);

            yield return null;
        }

        transform.position = endPosition;

        yield return new WaitForSeconds(0.5f);

        CallAmbulance();
    }

    private void CallAmbulance()
    {
        if (phoneObject != null)
        {
            phoneObject.SetActive(true);
        }

        Debug.Log("Witness is calling ambulance...");
    }
}