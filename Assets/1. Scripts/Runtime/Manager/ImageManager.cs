using System.Collections;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using UnityEngine;
using UnityEngine.UI;

public class ImageManager : Singleton<ImageManager>
{
    [SerializeField] private SerializedDictionary<CharacterType, SerializedDictionary<EmotionType, Sprite>> characterImages;
    [SerializeField] private SerializedDictionary<BackgroundType, Sprite> backgroundImages;
    public Sprite GetCharacterImage(CharacterType characterType, EmotionType motionType)
    {
        if (TryGetCharacterImage(characterType, motionType, out Sprite sprite))
        {
            return sprite;
        }

        return null;
    }

    public bool TryGetCharacterImage(CharacterType characterType, EmotionType motionType, out Sprite sprite)
    {
        sprite = null;

        if (!ShouldShowPortrait(characterType) || characterImages == null)
        {
            return false;
        }

        if (!characterImages.TryGetValue(characterType, out SerializedDictionary<EmotionType, Sprite> emotions)
            || emotions == null)
        {
            return false;
        }

        return emotions.TryGetValue(motionType, out sprite) && sprite != null;
    }

    public bool TryGetBackgroundImage(BackgroundType backgroundType, out Sprite sprite)
    {
        sprite = null;

        if (backgroundType == BackgroundType.None || backgroundImages == null)
        {
            return false;
        }

        return backgroundImages.TryGetValue(backgroundType, out sprite) && sprite != null;
    }

    public bool TryApplyBackground(Image image, BackgroundType backgroundType)
    {
        if (image == null || backgroundType == BackgroundType.None)
        {
            return false;
        }

        if (!TryGetBackgroundImage(backgroundType, out Sprite sprite))
        {
            return false;
        }

        image.gameObject.SetActive(true);
        image.sprite = sprite;
        image.enabled = true;
        return true;
    }

    public static bool ShouldShowPortrait(CharacterType characterType)
    {
        return characterType != CharacterType.None
            && characterType != CharacterType.Player
            && characterType != CharacterType.Story;
    }
}


public enum EmotionType
{
    Default,
    Happy,
    Sad,
    Angry,
    Surprised,

}