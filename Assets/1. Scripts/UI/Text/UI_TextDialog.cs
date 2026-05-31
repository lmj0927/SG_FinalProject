using System.Collections;
using UnityEngine;
using TMPro;
using DG.Tweening;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 말풍선 UI 컴포넌트
/// 등장/사라짐 애니메이션과 텍스트 타이핑 효과를 제공합니다.
/// </summary>
public class UI_TextDialog : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform balloonRectTransform;  // 말풍선 RectTransform
    [SerializeField] private TextMeshProUGUI textComponent;  // 텍스트 컴포넌트
    [SerializeField] private CanvasGroup canvasGroup;  // 페이드 효과용 CanvasGroup
    [SerializeField] private GameObject balloonTail;  // 말꼬리 이미지 오브젝트
    
    
    
    [Header("Show Animation")]
    [SerializeField] private bool showUseScale;
    [SerializeField] private bool showUseFade = true;
    [SerializeField] private float appearDuration = 0.3f;
    [SerializeField] private Ease appearEase = Ease.OutCubic;
    
    [Header("Hide Animation")]
    [SerializeField] private bool hideUseScale = true;
    [SerializeField] private bool hideUseFade = true;
    [SerializeField] private float disappearDuration = 0.3f;
    [SerializeField] private Ease disappearEase = Ease.InBack;
    
    [Header("Text Typing")]
    [SerializeField] private float textTypingSpeed = 0.05f;
    
    [Header("Inspector Preview (Play Mode)")]
    [SerializeField] private string inspectorPreviewText = "Hello, How Are You";
    
    [Header("Button")]
    [SerializeField] private UnityEngine.UI.Button button;  // 버튼 컴포넌트
    
    [Header("Behavior")]
    [SerializeField] private bool autoHideOnClick = true;  // 클릭 시 자동으로 Hide까지 할지 여부
    
    // Runtime Values
    private Tween currentTween;  // 현재 실행 중인 트윈
    private Tween textTween;  // 텍스트 타이핑 트윈
    private bool isVisible;
    private string currentText = "";
    
    // 이벤트
    public event Action<string> OnTextBalloonClicked;  // 텍스트 말풍선 클릭 시 (표시 중이던 텍스트)
    public event Action OnHideCompleted;  // Hide 연출 완료 시
    
    private void Awake()
    {
        // 컴포넌트 자동 찾기
        if (balloonRectTransform == null)
        {
            balloonRectTransform = GetComponent<RectTransform>();
        }
        
        if (textComponent == null)
        {
            textComponent = GetComponentInChildren<TextMeshProUGUI>();
        }
        
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
        
        // 초기 상태: 숨김
        if (balloonRectTransform != null)
        {
            balloonRectTransform.localScale = showUseScale ? Vector3.zero : Vector3.one;
        }
        
        if (canvasGroup != null)
        {
            canvasGroup.alpha = showUseFade ? 0f : 1f;
            canvasGroup.blocksRaycasts = false;
        }
        
        if (textComponent != null)
        {
            textComponent.text = "";
        }
        
        // 버튼 초기화
        if (button == null)
        {
            button = GetComponentInChildren<UnityEngine.UI.Button>();
        }
        
        // 버튼 클릭 이벤트 등록
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnButtonClicked);
        }
    }
    
    /// <summary>
    /// 버튼 클릭 이벤트 처리
    /// </summary>
    private void OnButtonClicked()
    {
        if (!string.IsNullOrEmpty(currentText))
        {
            // 텍스트 타이핑이 진행 중이면 즉시 최종 텍스트로 완료
            if (textTween != null && textTween.IsActive())
            {
                // 텍스트 트윈 즉시 완료
                textTween.Complete();
                return;
            }
            
            // 텍스트 타이핑이 완료된 경우에만 클릭 처리
            // textTween이 null이거나 IsActive()가 false면 타이핑 완료 상태
            if (textTween == null || !textTween.IsActive())
            {
                // 이벤트 발생 (현재 키값 전달)
                OnTextBalloonClicked?.Invoke(currentText);
                
                // 자동으로 말풍선을 닫도록 설정된 경우에만 Hide
                if (autoHideOnClick)
                {
                    Hide();
                }
            }
        }
    }
    
    /// <summary>
    /// 클릭 시 자동 Hide 여부 설정 (시퀀스 대화 등에서 외부에서 제어하고 싶을 때 사용).
    /// </summary>
    public void SetAutoHideOnClick(bool value)
    {
        autoHideOnClick = value;
    }
    
    /// <summary>
    /// 말풍선을 표시하고 전달한 문자열을 애니메이션으로 표시합니다.
    /// (로컬라이제이션 미구현 상태: 문자열을 그대로 사용)
    /// </summary>
    /// <param name="text">표시할 텍스트</param>
    public void ShowText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            Debug.LogWarning("[UI_TextDialog] 표시할 텍스트가 비어있습니다.");
            return;
        }
        
        if (isVisible && currentText == text)
        {
            return;
        }
        
        if (isVisible && currentText != text)
        {
            ChangeText(text);
            return;
        }
        
        ShowTextInternal(text);
    }
    
    private void ShowTextInternal(string text)
    {
        currentText = text;
        KillTweens();
        
        if (textComponent != null)
        {
            textComponent.text = "";
        }
        
        isVisible = true;
        gameObject.SetActive(true);
        ApplyShowStartState();
        SetDialogInteractable(true);
        
        if (!TryPlayShowAnimation(text))
        {
            StartTextAnimation(text);
        }
    }
    
    private void ApplyShowStartState()
    {
        if (balloonRectTransform != null)
        {
            balloonRectTransform.localScale = showUseScale ? Vector3.zero : Vector3.one;
        }
        
        if (canvasGroup != null)
        {
            canvasGroup.alpha = showUseFade ? 0f : 1f;
        }
    }

    private void SetDialogInteractable(bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = interactable;
        }
    }
    
    private bool TryPlayShowAnimation(string text)
    {
        bool hasTween = false;
        Sequence appearSequence = DOTween.Sequence().SetUpdate(true);
        
        if (showUseScale && balloonRectTransform != null)
        {
            appearSequence.Join(
                balloonRectTransform.DOScale(Vector3.one, appearDuration)
                    .SetEase(appearEase)
                    .SetUpdate(true));
            hasTween = true;
        }
        
        if (showUseFade && canvasGroup != null)
        {
            appearSequence.Join(canvasGroup.DOFade(1f, appearDuration).SetUpdate(true));
            hasTween = true;
        }
        
        if (!hasTween)
        {
            return false;
        }
        
        appearSequence.OnComplete(() => StartTextAnimation(text));
        currentTween = appearSequence;
        return true;
    }
    
    /// <summary>
    /// 텍스트 타이핑 애니메이션 시작 (전달된 문자열 그대로 출력)
    /// </summary>
    private void StartTextAnimation(string text)
    {
        if (textComponent == null)
        {
            return;
        }

        KillTextTween();
        textComponent.text = "";
        textTween = CreateTypingTween(text, text.Length * textTypingSpeed);
    }
    
    /// <summary>
    /// 텍스트만 변경 (말풍선은 유지)
    /// </summary>
    private void ChangeText(string text)
    {
        currentText = text;
        KillTextTween();
        
        if (textComponent != null)
        {
            textComponent.text = "";
        }
        
        SetDialogInteractable(true);
        StartTextAnimation(text);
    }

    private void KillTextTween()
    {
        if (textTween != null && textTween.IsActive())
        {
            textTween.Kill();
        }
    }

    private Tween CreateTypingTween(string text, float duration)
    {
        if (textComponent == null)
        {
            return null;
        }

        return DOTween.To(() => 0, value =>
        {
            if (textComponent == null)
            {
                return;
            }

            int safeLength = Mathf.Clamp(value, 0, text.Length);
            string visibleText = text.Substring(0, safeLength);
            if (visibleText.Length > 0 && visibleText[visibleText.Length - 1] == '\\')
            {
                visibleText = visibleText.Substring(0, visibleText.Length - 1);
            }

            textComponent.text = visibleText;
        }, text.Length, duration)
        .SetEase(Ease.Linear)
        .SetUpdate(true);
    }
    
    /// <summary>
    /// 텍스트를 정리하고 다음 텍스트를 표시할 준비를 합니다.
    /// </summary>
    /// <param name="onComplete">정리 완료 후 호출할 콜백</param>
    public void CleanText(Action onComplete = null)
    {
        if (!isVisible)
        {
            onComplete?.Invoke();
            return;
        }
        
        // 텍스트 트윈 정리
        if (textTween != null && textTween.IsActive())
        {
            textTween.Kill();
        }
        
        // 텍스트 정리 애니메이션 (페이드 아웃, unscaled time 사용)
        if (textComponent != null && canvasGroup != null)
        {
            Sequence cleanSequence = DOTween.Sequence().SetUpdate(true);
            cleanSequence.Append(canvasGroup.DOFade(0f, 0.2f).SetUpdate(true));
            cleanSequence.OnComplete(() =>
            {
                if (textComponent != null)
                {
                    textComponent.text = "";
                }
                
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f;
                }
                
                onComplete?.Invoke();
            });
            
            currentTween = cleanSequence;
        }
        else
        {
            if (textComponent != null)
            {
                textComponent.text = "";
            }
            onComplete?.Invoke();
        }
    }
    
    /// <summary>
    /// 말풍선을 숨깁니다.
    /// </summary>
    public void Hide()
    {
        if (!isVisible) return;
        
        // 기존 트윈 정리
        KillTweens();
        
        isVisible = false;
        currentText = "";
        
        if (!TryPlayHideAnimation())
        {
            CompleteHide();
        }
    }
    
    private bool TryPlayHideAnimation()
    {
        bool hasTween = false;
        Sequence disappearSequence = DOTween.Sequence().SetUpdate(true);
        
        if (hideUseScale && balloonRectTransform != null)
        {
            disappearSequence.Join(
                balloonRectTransform.DOScale(Vector3.zero, disappearDuration)
                    .SetEase(disappearEase)
                    .SetUpdate(true));
            hasTween = true;
        }
        
        if (hideUseFade && canvasGroup != null)
        {
            disappearSequence.Join(canvasGroup.DOFade(0f, disappearDuration).SetUpdate(true));
            hasTween = true;
        }
        
        if (!hasTween)
        {
            return false;
        }
        
        disappearSequence.OnComplete(CompleteHide);
        currentTween = disappearSequence;
        return true;
    }
    
    private void CompleteHide()
    {
        if (textComponent != null)
        {
            textComponent.text = "";
        }
        
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
        }
        
        if (balloonRectTransform != null)
        {
            balloonRectTransform.localScale = Vector3.zero;
        }
        
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
        
        gameObject.SetActive(false);
        OnHideCompleted?.Invoke();
    }
    
    /// <summary>
    /// 인스펙터 미리보기: 등장 연출부터 다시 표시합니다. (Play 모드 전용)
    /// </summary>
    public void InspectorPreviewShow()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[UI_TextDialog] Inspector Preview Show는 Play 모드에서만 사용할 수 있습니다.");
            return;
        }
        
        KillTweens();
        isVisible = false;
        currentText = "";
        ShowTextInternal(inspectorPreviewText);
    }
    
    /// <summary>
    /// 인스펙터 미리보기: Hide 연출로 닫습니다. (Play 모드 전용)
    /// </summary>
    public void InspectorPreviewHide()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[UI_TextDialog] Inspector Preview Hide는 Play 모드에서만 사용할 수 있습니다.");
            return;
        }
        
        Hide();
    }
    
    private void KillTweens()
    {
        if (currentTween != null && currentTween.IsActive())
        {
            currentTween.Kill();
        }
        
        if (textTween != null && textTween.IsActive())
        {
            textTween.Kill();
        }
    }
    
    private void OnDestroy()
    {
        KillTweens();
        
        // 버튼 이벤트 해제
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
        }
        
        OnTextBalloonClicked = null;
        OnHideCompleted = null;
    }
    
    #region Balloon Tail Control
    
    /// <summary>
    /// 말꼬리 활성화
    /// </summary>
    public void ShowBalloonTail()
    {
        if (balloonTail != null)
        {
            balloonTail.SetActive(true);
        }
    }
    
    /// <summary>
    /// 말꼬리 비활성화
    /// </summary>
    public void HideBalloonTail()
    {
        if (balloonTail != null)
        {
            balloonTail.SetActive(false);
        }
    }
    
    #endregion
}