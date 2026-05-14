using System;
using System.Collections.Generic;
using Valari.Collections;
using UnityEngine;
using UnityEngine.UI;
using Valari.Managers;

public class MainMenuHandler : MonoBehaviour
{
    public enum MenuState
    {
        Locked, Unlocked
    }

    [Serializable]
    public class MenuButton
    {
        public TrainingID ID;
        public Button Button;
        public CanvasGroup Holder;
        public GameObject LockedIcon;
        public MenuState State;
    }

    [SerializeField] private List<MenuButton> _mainMenuButtonList = new List<MenuButton>();
    private int _unlockedIndex = 0;

    private AchievementDataCollection _achievementDataCollection;

    private void Start()
    {
        // Initialize();
    }

    private void Initialize()
    {
        _achievementDataCollection = GameDataManager.Services.GetAchievementDataCollection;
        LoadMainMenu();
    }

    public void LoadMainMenu()
    {
        for (var i = 0; i < _mainMenuButtonList.Count; i++)
        {
            var menuButton = _mainMenuButtonList[i];
            if (_achievementDataCollection.IsAchievementComplete(menuButton.ID))
                _unlockedIndex++;

            menuButton.State = _unlockedIndex >= i ? MenuState.Unlocked : MenuState.Locked;
            menuButton.Button.interactable = _unlockedIndex >= i;
            menuButton.Holder.alpha = _unlockedIndex >= i ? 1 : .4f;
            menuButton.LockedIcon.SetActive(_unlockedIndex < i);
        }
    }
}

