using UnityEngine;

public class PatientFallTester : MonoBehaviour
{
    public SimplePatientFall patientFall;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            patientFall.Fall();
        }
    }
}