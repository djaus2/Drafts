# AI Player Implementation

## Overview

The Draughts game now includes an AI opponent feature, allowing players to practice and play solo games against computer-controlled opponents of varying difficulty levels. This document outlines the current implementation, available AI modes, and future enhancements.

## Current Implementation (V8.1.0)

### Available AI Difficulty Modes

The AI player system currently supports four difficulty levels, with two fully implemented.
These were simple enough to be defined by the developer.

#### 1. **Easy Mode** ✅ *Implemented*
- **Strategy**: Defensive play with basic lookahead
- **Behavior**: 
  - Makes random moves from available options after consideration of:
    - Can it take a piece next turn?
    - Actively avoids moves that would leave pieces vulnerable to capture on the next turn
      - Analyzes opponent's next (only) potential capture opportunities before committing to a move
    - Falls back to any available move if all positions are vulnerable
- **Best For**: New players learning the game, casual practice sessions

#### 2. **Random Mode** ✅ *Implemented*
- **Strategy**: Pure randomization with no strategic logic
- **Behavior**:
  - Selects completely random moves from all legal options
  - No defensive checks or offensive planning
  - No lookahead or position evaluation
  - Unpredictable and chaotic gameplay
- **Best For**: Testing game mechanics, experiencing varied game states, fun casual play

#### 3. **Moderate-3-ply Mode** ✅ *Implemented*
- **Strategy**: 3-ply lookahead with a simple weighted evaluation (non-minimax)
- **Behavior**:
  - Generates all legal AI moves (including mandatory captures and forced multi-jumps)
  - For each candidate move, simulates a 3-ply sequence:
    - AI move → Opponent reply → AI move
  - Scores each resulting leaf state using a custom weighting function (`GameMe`) and chooses the highest-scoring initial move
- **Weighting (GameMe)**:
  - `+1` for each opponent piece taken
  - `-1` for each AI piece taken
  - `+2` for creating an AI king
  - `+2` for taking an opponent king
  - `-2` for losing an AI king
  - `-100` if AI has no pieces or no legal moves (concede/lose)
  - `+100` if opponent has no pieces
- **Target Audience**: Intermediate players seeking a step up from Easy

#### 4. **Hard Mode** 🚧 *Planned*
- **Strategy**: Advanced tactical play with deep analysis
- **Planned Behavior**:
  - Minimax algorithm with alpha-beta pruning
  - Position scoring based on material, king count, and board control
  - Extended lookahead depth (4-6 moves)
  - Opening book and endgame tables
  - Trap detection and forced capture sequences
- **Target Audience**: Advanced players, competitive practice

## How AI Mode Works

### Game Setup
1. Player selects "Play against AI" on the Player page
2. Chooses desired difficulty level from dropdown
3. Clicks "Start New Game"
4. Player always plays as Player 1 (moving first)
5. AI controls Player 2

### Game Flow
1. **Player's Turn**: Player makes moves via the standard game interface (click/drag pieces)
2. **AI's Turn**: 
   - System automatically triggers AI move after a 1-second delay (for natural feel)
   - AI analyzes available moves based on selected difficulty
   - AI executes chosen move
   - Game board updates and control returns to player
3. **Game Completion**: Standard win/loss/draw conditions apply

### AI Move Selection Process

**Easy Mode Algorithm:**
```
1. Get all legal moves for AI pieces
2. For each move:
   a. Simulate the move on a virtual board
   b. Check if opponent could capture the piece at new position
   c. Mark move as "safe" or "vulnerable"
3. If safe moves exist, randomly select from safe moves
4. Otherwise, randomly select from all moves
5. Execute the selected move
```

**Random Mode Algorithm:**
```
1. Get all legal moves for AI pieces
2. Randomly select one move
3. Execute the selected move
```

**Moderate-3-ply Mode Algorithm:**
```
1. Snapshot the current game state
2. Get all legal moves for AI (respect mandatory capture and forced multi-jumps)
3. For each AI move (ply 1):
   a. Simulate the move
   b. Enumerate opponent legal replies (ply 2)
   c. For each opponent reply, enumerate AI legal moves again (ply 3)
   d. Score the leaf state with GameMe
4. Choose the initial AI move with the best resulting score
5. Execute the selected move
```

## User Interface Features

### AI Mode Indicators
- **Game Setup**: Clear labeling of AI mode and selected difficulty
- **In-Game**: Persistent display showing "Playing against AI" with difficulty level
- **Move Feedback**: AI moves are logged with difficulty level for debugging

### Difficulty Availability
- **Implemented Modes**: "Easy", "Moderate-3-ply", and "Random" are enabled
- **Unimplemented Modes**: "Hard" shows warning message and disables game start
- **Visual Feedback**: Disabled buttons with tooltips explaining unavailability

### Chat and Voice Features
- **Disabled in AI Mode**: Chat and voice communication features are hidden
- **Rationale**: AI opponent doesn't communicate, simplifies UI for solo play

## Technical Architecture

### Service Layer
- **AiService**: Handles all AI move logic and difficulty implementations
- **DraughtsService**: Manages game state, move validation, and rules enforcement
- **Integration**: AI service calls DraughtsService for move validation and execution

### Move Validation
- AI moves go through the same validation as human moves
- Ensures AI follows all game rules (forced captures, legal moves, king movement)
- Prevents any AI cheating or invalid moves

### Performance
- **Move Calculation**: Sub-second for Easy and Random modes
- **Artificial Delay**: 1-second delay added for natural gameplay feel
- **Asynchronous Processing**: AI calculations don't block UI thread

## Game State Management

### AI Game Lifecycle
1. **Creation**: AI games are created with `isAiMode=true` flag
2. **Persistence**: AI difficulty level stored in game state
3. **Disposal**: Proper cleanup when game ends or is cancelled
4. **New Game**: "New Game" button starts fresh AI game with same difficulty

### State Persistence
- AI games properly disposed on exit (cancel/concede/timeout/win/loss)
- No state leakage between games
- Fresh board state for each new game

## Future Enhancements

### Moderate Mode Implementation
Moderate is now implemented as **Moderate-3-ply** (see above). Future refinements will likely include:
- Improved positional evaluation beyond material/king events
- Better move ordering and pruning (still non-minimax)
- Tactical pattern detection and trap avoidance

### Hard Mode Implementation
**Planned Features:**
- Minimax algorithm with alpha-beta pruning for optimal move selection
- Advanced position evaluation considering:
  - Material count (pieces vs kings)
  - Board control and mobility
  - Piece advancement and promotion potential
  - King safety and activity
- Opening book for strong early game
- Endgame tablebase for perfect endgame play
- Adjustable search depth based on game phase
- Move ordering for better pruning efficiency

**Estimated Complexity**: High - requires sophisticated algorithms and extensive testing

### Additional Future Features
- **AI Personality Modes**: Aggressive, defensive, balanced playing styles
- **Difficulty Customization**: Fine-tune AI parameters for custom difficulty
- **AI Analysis Mode**: Show AI's evaluation and suggested moves for learning
- **Training Mode**: Hints and move suggestions from AI
- **Statistics Tracking**: Win/loss records against each difficulty level
- **AI vs AI**: Watch two AI opponents play each other

## Testing and Quality Assurance

### Current Testing
- Manual testing of Easy and Random modes
- Verification of legal move generation
- Game completion scenarios (win/loss/draw)
- State cleanup and disposal

### Future Testing Needs
- Automated AI move validation tests
- Performance benchmarks for harder difficulties
- Edge case handling (forced captures, multiple jumps)
- Regression testing for game rule compliance

## Conclusion

The AI player implementation provides a solid foundation for solo play, with two working difficulty modes suitable for beginners and casual players. The architecture is designed to support more sophisticated AI implementations in future releases, with clear separation of concerns and extensible difficulty system.

The current implementation focuses on correctness and user experience, ensuring that AI opponents play fair, follow all game rules, and provide an enjoyable practice environment for players of all skill levels.

---

**Version**: 8.1.0  
**Last Updated**: April 2026  
**Status**: Easy, Moderate-3-ply, and Random modes implemented; Hard mode planned
