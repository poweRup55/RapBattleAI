using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PopUpGameText : MonoBehaviour
{
    private TextMeshProUGUI textComponent;
    private Animator animator;

    private void Awake()
    {
        textComponent = GetComponent<TextMeshProUGUI>();
        animator = GetComponent<Animator>();
        animator.enabled = false;
    }

    public void ShowText(string content)
    {
        textComponent.text = content;
        animator.enabled = true;
        StartCoroutine(WaitForAnimationAndDestroy());
    }

    private IEnumerator WaitForAnimationAndDestroy()
    {
        yield return new WaitForSeconds(animator.GetCurrentAnimatorStateInfo(0).length);
        Destroy(gameObject);
    }
}
