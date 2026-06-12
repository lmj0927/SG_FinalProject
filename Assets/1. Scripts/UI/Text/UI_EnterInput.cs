using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Enter 트리거용 입력 UI. 입력 후 Enter로 제출하며, 제출된 텍스트는 시나리오에서 {input} 치환에 사용됩니다.
/// </summary>
public class UI_EnterInput : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputField;

    public event Action<string> OnSubmitted;

    private void Awake()
    {
        if (inputField == null)
        {
            inputField = GetComponentInChildren<TMP_InputField>(true);
        }

        if (inputField != null)
        {
            inputField.onSubmit.AddListener(HandleSubmit);
        }

        HideImmediate();
    }

    public void Show()
    {
        gameObject.SetActive(true);

        if (inputField == null)
        {
            return;
        }

        inputField.text = string.Empty;
        inputField.interactable = true;
        inputField.ActivateInputField();
        inputField.Select();
    }

    public void Hide()
    {
        if (inputField != null)
        {
            inputField.text = string.Empty;
            inputField.DeactivateInputField();
        }

        gameObject.SetActive(false);
    }

    private void HideImmediate()
    {
        if (inputField != null)
        {
            inputField.text = string.Empty;
        }

        gameObject.SetActive(false);
    }

    private void HandleSubmit(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        OnSubmitted?.Invoke(text.Trim());
    }

    public void SetInteractable(bool interactable)
    {
        if (inputField != null)
        {
            inputField.interactable = interactable;
        }
    }

    private void OnDestroy()
    {
        if (inputField != null)
        {
            inputField.onSubmit.RemoveListener(HandleSubmit);
        }

        OnSubmitted = null;
    }
}
