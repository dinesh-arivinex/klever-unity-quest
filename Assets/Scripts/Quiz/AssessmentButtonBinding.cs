using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Oculus.Interaction;

// Rebind only listeners owned by the assessment code, preserving authored events.
public sealed class AssessmentButtonBinding : MonoBehaviour
{
    private Button button;
    private InteractableUnityEventWrapper[] interactables = Array.Empty<InteractableUnityEventWrapper>();
    private UnityAction click;
    private UnityAction select;

    public static void Bind(Button target, int answerIndex, Action<int> onAnswer)
    {
        if (target == null) return;
        var binding = target.GetComponent<AssessmentButtonBinding>();
        if (binding == null) binding = target.gameObject.AddComponent<AssessmentButtonBinding>();
        binding.Unbind();
        binding.button = target;
        binding.click = () => onAnswer(answerIndex);
        binding.select = () => onAnswer(answerIndex);
        target.onClick.AddListener(binding.click);
        binding.interactables = target.GetComponents<InteractableUnityEventWrapper>();
        foreach (var interactable in binding.interactables)
            if (interactable.WhenSelect != null) interactable.WhenSelect.AddListener(binding.select);
    }

    private void Unbind()
    {
        if (button != null && click != null) button.onClick.RemoveListener(click);
        foreach (var interactable in interactables)
            if (interactable != null && interactable.WhenSelect != null && select != null) interactable.WhenSelect.RemoveListener(select);
        button = null;
        click = null;
        select = null;
        interactables = Array.Empty<InteractableUnityEventWrapper>();
    }

    private void OnDestroy() { Unbind(); }
}
