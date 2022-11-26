# VRC-Liars-Dice

### Dependencies
- Udon 1.0 (download from the VRC Creator Companion)

### Instal Instructions
1. Download the most recent release (download link is to the right)
2. Import the Unity Package into your World Unity Project
3. Import the Liars Dice Prefab into your scene **OR** generate using Window -> Liar's Dice Table -> Generate Only Table

### How to play
To begin each round, all players roll their dice simultaneously. Each player looks at their own dice after they roll, keeping them hidden from the other players.

The first player then states a bid consisting of a face ("1's", "5's", etc.) and a quantity. The quantity represents the player's guess as to how many of each face have been rolled by all the players at the table, including themselves. For example, a player might bid "five 2's."

Each subsequent player can either then make a higher bid of the same face (e.g., "six 2's"), or they can challenge the previous bid.

If the player challenges the previous bid, all players reveal their dice. If the bid is matched or exceeded, the bidder wins. Otherwise the challenger wins.

If the bidder loses, they remove one of their dice from the game.

The loser of the previous round begins the next round.

The winner of the game is the last player to have any dice remaining.


### Credits
- [🦎](https://github.com/akalink) akalink: Primary Liar's Dice Developer
- [⚗️](https://github.com/CyanLaser) Cyanlaser: CyanPlayerObjectPool (ObjectPool Prefab used in prototyping)
- [🧝‍](https://github.com/orels1) orels1: TMP Billboard Shader
- [🧙](https://github.com/MerlinVR/UdonSharp) Merlin: UdonSharp
- [😼](https://github.com/Centauri2442) Centauri: General Help/Feedback/Testing
