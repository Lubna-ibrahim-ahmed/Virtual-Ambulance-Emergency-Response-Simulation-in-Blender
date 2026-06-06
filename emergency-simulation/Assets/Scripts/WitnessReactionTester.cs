using UnityEngine;

public class WitnessReactionTester : MonoBehaviour
{
    public WitnessReaction witnessReaction;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.O))
        {
            witnessReaction.ReactToExplosion();
        }
    }
}