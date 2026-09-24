using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Gwent.Models;
using System;

namespace Gwent.UI
{
    public class LobbyMatchItem : MonoBehaviour
    {
        public TextMeshProUGUI matchInfoText; // Örn: "Kod: A8K3P9 | Irk: Northern"
        public Button joinButton;

        public void Setup(GameState matchState, Action<string> onJoinClicked)
        {
            if (matchInfoText != null)
            {
                matchInfoText.text = $"[ {matchState.matchId} ] - Irk: {matchState.player1Faction}";
            }

            if (joinButton != null)
            {
                joinButton.onClick.RemoveAllListeners();
                joinButton.onClick.AddListener(() => onJoinClicked?.Invoke(matchState.matchId));
            }
        }
    }
}