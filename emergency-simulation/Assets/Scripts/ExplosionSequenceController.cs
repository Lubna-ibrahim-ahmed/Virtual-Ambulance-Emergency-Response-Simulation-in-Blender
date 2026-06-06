using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class ExplosionSequenceController : MonoBehaviour
{
    [Header("Effects")]
    public GameObject gasVapor;
    public GameObject gasExplosion;
    public GameObject fireAfterExplosion;
    public GameObject smokeAfterExplosion;

    [Header("Characters")]
    public SimplePatientFall patientFall;
    public WitnessReaction witnessReaction;

    [Header("Audio Optional")]
    public AudioSource explosionSound;
    public AudioSource fireSound;

    [Header("Timing")]
    public float explosionDuration = 0.6f;

    private bool explosionAlreadyHappened = false;

    void Start()
    {
        if (gasExplosion != null)
            gasExplosion.SetActive(false);

        if (fireAfterExplosion != null)
            fireAfterExplosion.SetActive(false);

        if (smokeAfterExplosion != null)
            smokeAfterExplosion.SetActive(false);
    }

    void Update()
    {
        // Read X via the Input System (project runs with the Input System package active;
        // the legacy UnityEngine.Input path throws under that handler). Works in Both mode too.
        if (Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame)
        {
            StartExplosion();
        }
    }

    public void StartExplosion()
    {
        if (explosionAlreadyHappened)
            return;

        explosionAlreadyHappened = true;
        StartCoroutine(ExplosionRoutine());
    }

    private IEnumerator ExplosionRoutine()
    {
        Debug.Log("Explosion started");

        if (gasVapor != null)
            gasVapor.SetActive(false);

        if (gasExplosion != null)
        {
            gasExplosion.SetActive(true);

            ParticleSystem explosionParticles = gasExplosion.GetComponent<ParticleSystem>();

            if (explosionParticles != null)
            {
                explosionParticles.Clear();
                explosionParticles.Play();
            }
        }

        if (explosionSound != null)
            explosionSound.Play();

        if (patientFall != null)
            patientFall.Fall();

        if (witnessReaction != null)
            witnessReaction.ReactToExplosion();

        yield return new WaitForSeconds(explosionDuration);

        if (gasExplosion != null)
            gasExplosion.SetActive(false);

        if (fireAfterExplosion != null)
        {
            fireAfterExplosion.SetActive(true);

            ParticleSystem fireParticles = fireAfterExplosion.GetComponent<ParticleSystem>();

            if (fireParticles != null)
            {
                fireParticles.Clear();
                fireParticles.Play();
            }
        }

        if (smokeAfterExplosion != null)
        {
            smokeAfterExplosion.SetActive(true);

            ParticleSystem smokeParticles = smokeAfterExplosion.GetComponent<ParticleSystem>();

            if (smokeParticles != null)
            {
                smokeParticles.Clear();
                smokeParticles.Play();
            }
        }

        if (fireSound != null)
            fireSound.Play();

        Debug.Log("Fire and smoke started");
    }
}