
using System.Collections;
using UnityEngine;

public class LandingSequenceController : MonoBehaviour
{
    public GameObject lander;
    public GameObject opportunityRover;

    public float landingDuration = 4f;

    void Start()
    {
        // Hide Opportunity while the lander is descending
        opportunityRover.SetActive(false);

        // Start the landing sequence
        StartCoroutine(LandingSequence());
    }

    IEnumerator LandingSequence()
    {
        // Wait for the landing animation to finish
        yield return new WaitForSeconds(landingDuration);

        // Hide the landing sphere
        lander.SetActive(false);

        // Reveal Opportunity on Mars
        opportunityRover.SetActive(true);
    }
}
