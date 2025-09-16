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

    public void ShowText()
    {
        gameObject.SetActive(true);
        animator.enabled = true;
        StartCoroutine(WaitForAnimationAndDestroy());
    }

    public void SetColor(Color newColor)
    {
        if (textComponent != null)
        {
            textComponent.color = newColor;
        }
    }

    public void SetText(string message)
    {
        if (textComponent != null)
        {
            textComponent.text = message;
        }
    }

    private IEnumerator WaitForAnimationAndDestroy()
    {
        yield return new WaitForSeconds(animator.GetCurrentAnimatorStateInfo(0).length);
        Destroy(gameObject);
    }
}
