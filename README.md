# VRC-Liars-Dice
VRC Liars Dice is a prefab for playing the classic game Liar's Dice, the game is also known as Doubting Dice, Perudo, Mexacali, Call My Bluff, Pirates Dice, and Deception Dice. The instaltion and the rules are outlined below. 

[![Discord Invite](/image%20files/2111370.png)](https://discord.gg/u4SNU3eRrd) akalink's discord server.

## Dependencies
- UdonSharp 1.0 (download from the [VRC Creator Companion](https://vcc.docs.vrchat.com/))


## Install Instructions
1. Download the most recent release (download link is to the right)
2. Make sure your project is updated to use UdonSharp 1.x (1.0 or newer). Update it from the VRC Creator Companion
3. Import the Unity Package into your World Unity Project
4. Import the Liars Dice Prefab into your scene **OR** generate using Window -> Liar's Dice Table -> Generate Only Table

   ![Example of the prefab](/image%20files/prefab%20example.png)
   ![Example of the Window Option](/image%20files/editor%20script%20example.png)
5. The logger exists to help debug the table during development. It is suggested you use it if you plan to edit the table for any purpose.

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


### Future plans
- Codesmell - reduce cognitive complexity of various methods
- Implement reset button in UI for playing players
- Implement easy language switching system
- Remove benign errors in console.
- large dice table prefab
- selector script does not fire if tracking ball exits from bottom.
