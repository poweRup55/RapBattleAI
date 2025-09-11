using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class RapperController : MonoBehaviour
{
    private Animator animator;

    // Start is called before the first frame update
    void Start()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("Animator component not found on the GameObject.");
        }
    }

    // Update is called once per frame
    void Update() { }

    void StartRapping()
    {
        if (animator != null)
        {
            animator.SetTrigger("StartRapping");
        }
    }

    void StopRapping()
    {
        if (animator != null)
        {
            animator.SetTrigger("StopRapping");
        }
    }
}
