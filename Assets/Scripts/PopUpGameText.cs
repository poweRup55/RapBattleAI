using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PopUpGameText : MonoBehaviour
{
    private TextMeshProUGUI textComponent;
    private Animator animator;
    private AudioSource audioSource;

    [SerializeField]
    private AudioClip positiveSoundEffect;

    [SerializeField]
    private AudioClip negativeSoundEffect;

    private void Awake()
    {
        textComponent = GetComponent<TextMeshProUGUI>();
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        animator.enabled = false;
    }

    public void ShowText()
    {
        if (textComponent.text == "" || textComponent.text == null)
        {
            Destroy(gameObject);
            Debug.LogWarning("PopUpGameText: No text to display, destroying object.");
            return;
        }
        gameObject.SetActive(true);
        animator.enabled = true;
        audioSource.Play();

        StartCoroutine(WaitForAnimationAndDestroy());
    }

    private void SetColor(Color newColor)
    {
        if (textComponent != null)
        {
            textComponent.color = newColor;
        }
    }

    private void SetText(string message)
    {
        if (textComponent != null)
        {
            textComponent.text = message;
        }
    }

    private void SetSoundEffect(bool isPositive)
    {
        audioSource.clip = isPositive ? positiveSoundEffect : negativeSoundEffect;
    }

    public void SetReaction(string message, bool isPositive)
    {
        Color reactionColor = isPositive
            ? new Color(0.2f, 1f, 0.2f) // Green for positive reactions
            : new Color(1f, 0.2f, 0.2f); // Red for negative reactions

        SetColor(reactionColor);
        SetText(message);
        SetSoundEffect(isPositive);
    }

    private IEnumerator WaitForAnimationAndDestroy()
    {
        yield return new WaitForSeconds(animator.GetCurrentAnimatorStateInfo(0).length);
        Destroy(gameObject);
    }
}
