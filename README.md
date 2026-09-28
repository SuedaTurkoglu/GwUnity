# Unity Multiplayer Card Game (Fan Project)

> **Disclaimer**  
> This is a non-commercial, unofficial educational fan project built solely for learning and portfolio purposes. It is inspired by the iconic card game mechanics from a world-famous fantasy universe and video game created by CD Projekt Red. All card artworks and trademarked concepts belong to their respective copyright owners.


## Tech Stack & Architecture

* **Game Engine:** Unity (C#)
* **Backend & Networking:** Firebase Firestore (Real-time DB & State Listener)
* **Animation Engine:** DOTween (Demigiant)
* **Development Partner:** Local Ollama Instance (LLM-assisted modeling & architecture optimization)
* **UI Framework:** Unity UI & TextMeshPro

---

## Project Structure

```text
Assets/
 ├── Scripts/
 │    ├── Core/
 │    │    ├── AbilityManager.cs      # Card abilities and score calculation logic
 │    │    ├── CardManager.cs         # Card data loading and registry
 │    │    └── GameManager.cs         # Local session and player state
 │    ├── Networking/
 │    │    └── FirestoreGameManager.cs# Firebase listener, matchmaking and moves
 │    ├── UI/
 │    │    ├── CardAnimationManager.cs# DOTween flight & graveyard animations
 │    │    ├── CardView.cs            # Dynamic UI binding for individual cards
 │    │    ├── DeckBuilderUI.cs       # Custom deck creation & faction constraints
 │    │    ├── LobbyManager.cs        # Lobby UI, room lists and navigation
 │    │    └── UIManager.cs           # Game screen perspective mapping & board updates
 │    └── Models/
 │         ├── CardData.cs            # JSON card definitions
 │         └── GameState.cs           # Synchronized match state model
 └── Resources/
      ├── data/                       # Cards JSON database
      └── images/                     # Badges, row icons, and card sprites
