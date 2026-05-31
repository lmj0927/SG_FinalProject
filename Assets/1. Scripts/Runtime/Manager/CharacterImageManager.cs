using System.Collections;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using UnityEngine;

public class CharacterImageManager : Singleton<CharacterImageManager>
{
    [SerializeField] private SerializedDictionary<string, SerializedDictionary<EmotionType, Sprite>> characterImages;

    public Sprite GetCharacterImage(string characterId, EmotionType motionType)
    {
        if (TryGetCharacterImage(characterId, motionType, out Sprite sprite))
        {
            return sprite;
        }

        return null;
    }

    public bool TryGetCharacterImage(string characterId, EmotionType motionType, out Sprite sprite)
    {
        sprite = null;

        if (characterImages == null || string.IsNullOrWhiteSpace(characterId))
        {
            return false;
        }

        if (!characterImages.TryGetValue(characterId.Trim(), out SerializedDictionary<EmotionType, Sprite> emotions)
            || emotions == null)
        {
            return false;
        }

        return emotions.TryGetValue(motionType, out sprite) && sprite != null;
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