using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleUI : MonoBehaviour
{
    [SerializeField] private Image logoImage;
    [SerializeField] private List<Image> characterImages;
    [SerializeField] private Button startButton;
    [SerializeField] private Image fadeImage;

    [Header("Animation")]
    [SerializeField] private float logoDropDuration = 0.8f;
    [SerializeField] private float logoDropOffset = 600f;
    [SerializeField] private float characterRiseDuration = 0.5f;
    [SerializeField] private float characterRiseOffset = 400f;
    [SerializeField] private float characterInterval = 0.15f;
    [SerializeField] private float startButtonScaleDuration = 0.5f;
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "GameScene";

    private Sequence introSequence;
    private Tween fadeTween;
    private Vector2 logoFinalPosition;
    private readonly List<Vector2> characterFinalPositions = new List<Vector2>();
    private bool isLoadingScene;

    private void Awake()
    {
        EnsureFadeOverlay();
        CacheFinalPositions();
        SetInitialState();
    }

    private void Start()
    {
        PlayIntroAnimation();

        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartButtonClicked);
        }
    }

    private void OnDestroy()
    {
        introSequence?.Kill();
        fadeTween?.Kill();

        if (startButton != null)
        {
            startButton.onClick.RemoveListener(OnStartButtonClicked);
        }
    }

    private void CacheFinalPositions()
    {
        characterFinalPositions.Clear();

        if (logoImage != null)
        {
            logoFinalPosition = logoImage.rectTransform.anchoredPosition;
        }

        if (characterImages == null)
        {
            return;
        }

        for (int i = 0; i < characterImages.Count; i++)
        {
            Image characterImage = characterImages[i];
            if (characterImage == null)
            {
                characterFinalPositions.Add(Vector2.zero);
                continue;
            }

            characterFinalPositions.Add(characterImage.rectTransform.anchoredPosition);
        }
    }

    private void SetInitialState()
    {
        if (logoImage != null)
        {
            RectTransform logoRect = logoImage.rectTransform;
            logoRect.anchoredPosition = logoFinalPosition + Vector2.up * logoDropOffset;
        }

        if (characterImages != null)
        {
            for (int i = 0; i < characterImages.Count; i++)
            {
                Image characterImage = characterImages[i];
                if (characterImage == null || i >= characterFinalPositions.Count)
                {
                    continue;
                }

                characterImage.rectTransform.anchoredPosition =
                    characterFinalPositions[i] + Vector2.down * characterRiseOffset;
            }
        }

        if (startButton != null)
        {
            startButton.transform.localScale = Vector3.zero;
            startButton.interactable = false;
        }
    }

    private void PlayIntroAnimation()
    {
        introSequence?.Kill();
        introSequence = DOTween.Sequence();

        if (logoImage != null)
        {
            introSequence.Append(
                logoImage.rectTransform
                    .DOAnchorPos(logoFinalPosition, logoDropDuration)
                    .SetEase(Ease.OutBounce));
        }

        if (characterImages != null)
        {
            bool isFirstCharacter = true;
            int characterOrder = 0;

            for (int i = 0; i < characterImages.Count; i++)
            {
                Image characterImage = characterImages[i];
                if (characterImage == null || i >= characterFinalPositions.Count)
                {
                    continue;
                }

                Tween riseTween = characterImage.rectTransform
                    .DOAnchorPos(characterFinalPositions[i], characterRiseDuration)
                    .SetEase(Ease.OutCubic)
                    .SetDelay(characterOrder * characterInterval);

                if (isFirstCharacter)
                {
                    introSequence.Append(riseTween);
                    isFirstCharacter = false;
                }
                else
                {
                    introSequence.Join(riseTween);
                }

                characterOrder++;
            }
        }

        if (startButton != null)
        {
            introSequence.Append(
                startButton.transform
                    .DOScale(Vector3.one, startButtonScaleDuration)
                    .SetEase(Ease.OutBack)
                    .OnComplete(() => startButton.interactable = true));
        }
    }

    private void EnsureFadeOverlay()
    {
        if (fadeImage != null)
        {
            Color color = fadeImage.color;
            color.a = 0f;
            fadeImage.color = color;
            fadeImage.raycastTarget = false;
            return;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return;
        }

        GameObject fadeObject = new GameObject("FadeOverlay");
        RectTransform fadeRect = fadeObject.AddComponent<RectTransform>();
        fadeRect.SetParent(canvas.transform, false);
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;
        fadeRect.SetAsLastSibling();

        fadeImage = fadeObject.AddComponent<Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        fadeImage.raycastTarget = false;
    }

    private void OnStartButtonClicked()
    {
        if (isLoadingScene)
        {
            return;
        }

        isLoadingScene = true;
        startButton.interactable = false;

        if (fadeImage == null)
        {
            SceneManager.LoadScene(gameSceneName);
            return;
        }

        fadeImage.raycastTarget = true;
        fadeTween?.Kill();
        fadeTween = fadeImage
            .DOFade(1f, fadeDuration)
            .SetEase(Ease.InQuad)
            .OnComplete(() => SceneManager.LoadScene(gameSceneName));
    }
}
