using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameClient.Data;
using GameClient.Presentation.Board;
using GameClient.Presentation.Board3D;
using GameClient.Presentation.HUD;
using GameClient.Presentation.HUD3D;
using GameDomain.Gameplay;
using GameDomain.Generation;
using GameDomain.Model;
using GameDomain.Progression;
using UnityEngine;

namespace GameClient.Presentation
{
    public sealed class GameController : MonoBehaviour
    {
        [SerializeField] private BoardView3D _boardView;
        [SerializeField] private TrayView3D _trayView;
        [SerializeField] private GameOverPopup3D _gameOverPopup;
        [SerializeField] private MatchCelebrationController _matchCelebration;

        // Presentation-only streak timer: picks the praise-text tier and drives
        // the combo meter (via ComboChanged). Deliberately separate from any
        // domain scoring - TrayManager awards points itself, so this never
        // touches _board.Score.
        private const double ComboWindowSeconds = 3.0;
        private DateTime? _lastMatchTime;
        private int _comboCount;
        private readonly System.Random _random = new System.Random();

        // Progression (sub-project #4): loaded/saved progress, the level being
        // played, and how many aids were spent this attempt (for star scoring).
        private GameProgress _progress;
        private int _currentLevelId = 1;
        private int _aidsUsed;
        private bool _isDaily; // true while the daily challenge is being played
        public int CurrentLevelId => _currentLevelId;
        public GameProgress Progress => _progress;

        private BoardState _board;
        private List<TileSlot> _shape;
        private Dictionary<string, TileSlot> _slotsById;
        public event Action<int, int> ScoreChanged;
        public event Action<int, int, int> UsesChanged;
        // Fired on every tray match with the current combo streak count so the
        // combo meter can fill/pop and start its drain timer.
        public event Action<int> ComboChanged;

        // True while the deal-in animation or a tap's tap-to-tray sequence
        // is still playing, so a second tap (or a hint/undo/shuffle press)
        // can't land mid-animation and desync the board from what's visible.
        public bool IsInputLocked { get; private set; }

        // Set by the pause menu so board taps are ignored while the overlay is up.
        private bool _paused;
        public void SetPaused(bool paused) => _paused = paused;

        private void Awake()
        {
            // Load progress in Awake so it's ready before other components'
            // OnEnable (the level-select screen reads it there to show lock/stars).
            _progress = SaveSystem.Load();
            _currentLevelId = Mathf.Clamp(_progress.HighestUnlockedLevelId, 1,
                LevelCatalog.Levels[LevelCatalog.Levels.Count - 1].LevelId);
        }

        private void Start()
        {
            // vSyncCount must be 0 for targetFrameRate to take effect at all -
            // otherwise Unity ignores it and locks to (display refresh /
            // vSyncCount). Requesting 120 only actually renders at 120 on a
            // device whose display supports it (paired with
            // PlayerSettings.Android.optimizedFramePacing in AndroidBuilder).
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 120;

            // The board no longer deals in on scene load - the level-select then
            // level-start screens are shown first; BeginLevel() deals it in on Play.
        }

        // Chosen from the level-select screen. Only unlocked levels are accepted.
        public void SelectLevel(int levelId)
        {
            if (_progress == null) _progress = SaveSystem.Load();
            if (_progress.IsUnlocked(levelId))
                _currentLevelId = levelId;
        }

        // Tear down the current board's rendered tiles when leaving gameplay (e.g.
        // pressing Back to the level-select screen), so they don't linger on screen
        // and show through the next screen. No-op if nothing is dealt.
        public void ClearBoard()
        {
            if (_boardView != null) _boardView.Clear();
        }

        // Entry point from the level-start screen's Play button.
        public void BeginLevel()
        {
            _isDaily = false;
            LoadLevel();
        }

        // Entry point from the level-select screen's Daily button.
        public void BeginDaily()
        {
            _isDaily = true;
            LoadLevel();
        }

        public void RestartLevel()
        {
            if (_gameOverPopup != null)
                _gameOverPopup.Hide();
            LoadLevel();
        }

        private void LoadLevel()
        {
            // The daily challenge is a fixed-difficulty board seeded by the date
            // (same for everyone that day); normal levels scale by difficulty and
            // use the running RNG (sub-projects #5, #7).
            int difficulty;
            System.Random rng;
            if (_isDaily)
            {
                difficulty = DailyChallenge.Difficulty;
                rng = new System.Random(DailyChallenge.SeedFor(DateTime.Now));
            }
            else
            {
                var levelData = LevelCatalog.Get(_currentLevelId) ?? LevelCatalog.Levels[0];
                difficulty = levelData.Difficulty;
                rng = _random;
            }
            if (!_isDaily && _currentLevelId == 5)
            {
                // The big showcase pyramid, capped at the project MAX of 80 tiles so
                // it fits the play area cleanly at full tile size (a 7x5 turtle) with
                // no overlap into the tray or the bottom buttons.
                _shape = TurtleShapeBuilder.BuildWithTileCount(TurtleShapeBuilder.MaxTiles);
            }
            else
            {
                _shape = TurtleShapeBuilder.BuildForDifficulty(difficulty);
            }
            _slotsById = _shape.ToDictionary(s => s.Id);

            var level = new LevelDefinition
            {
                LevelId = _isDaily ? -1 : _currentLevelId,
                Shape = _shape,
                TileSetId = "default"
            };

            // Pair-match tray: values come in pairs so two identical tiles
            // collected in the tray clear together. The mode/profile pick how the
            // board is shaped (opening branching, look-alike confusability); daily
            // challenges are always pair-mode regardless of level config.
            var mode = _isDaily ? MatchMode.Pair
                                : (LevelCatalog.Get(_currentLevelId)?.Mode ?? MatchMode.Pair);
            var profile = DifficultyProfile.For(difficulty, mode);

            var tileSet = _boardView != null ? _boardView.TileSet : null;
            int[] clusters = tileSet != null ? tileSet.SimilarityClusterId : null;
            int modelCount = (tileSet != null && tileSet.FoodModels != null && tileSet.FoodModels.Length > 0)
                ? tileSet.FoodModels.Length : 26;

            _board = BoardGenerator.GenerateShaped(level, rng, profile, clusters, modelCount);
            _lastMatchTime = null;
            _comboCount = 0;
            _aidsUsed = 0;

            // The tray holds tapped tiles until 2 identical ones collect and
            // clear; slot count matches the board's MaxTraySize (4).
            if (_trayView != null)
                _trayView.Initialize(_board.MaxTraySize);

            IsInputLocked = true;
            _boardView.Build(_board, _slotsById, animateDealIn: true, onDealInComplete: () => IsInputLocked = false);

            ScoreChanged?.Invoke(_board.Score, _board.ComboCount);
            NotifyUsesChanged();
        }

        // Tray-collection mechanic: tap a FREE tile to send it flying up into the
        // tray. Two identical tiles in the tray (any slots) auto-clear together.
        // If the tray fills (4 different tiles) with no match, it's game over.
        public void OnTileTapped(string slotId)
        {
            if (_paused) return;
            if (IsInputLocked) return;
            if (_board.IsGameOver) return;
            if (_board.Cells.Values.All(c => c.Cleared)) return;

            var oldTray = new List<string>(_board.TrayTileIds);

            // TrayManager runs the freedom check itself (excluding tray tiles) and
            // rejects covered tiles, a full tray, or an already-collected tile.
            if (!TrayManager.TryPushToTray(_board, _slotsById, slotId))
            {
                _boardView.GetTileView(slotId)?.PlayShake();
                return;
            }

            var newTray = new List<string>(_board.TrayTileIds);
            StartCoroutine(AnimateTapToTray(slotId, oldTray, newTray));
        }

        private IEnumerator AnimateTapToTray(string slotId, List<string> oldTray, List<string> newTray)
        {
            IsInputLocked = true;

            var value = _board.Cells[slotId].Value;
            var foodModel = TileVisual.FoodModelFor(_boardView.TileSet, value);

            var tileView = _boardView.GetTileView(slotId);
            Vector3 startPos = tileView != null
                ? tileView.transform.position
                : _trayView.GetSlotWorldPosition(0);

            // The tile now lives in the tray (domain-side), so take it off the board.
            _boardView.RemoveTileInstant(slotId);

            // Fly a card from the board up to the slot it landed in.
            int landingIndex = oldTray.Count;
            var flight = _trayView.SpawnFlightCard(foodModel, startPos);
            Vector3 slotPos = _trayView.GetSlotWorldPosition(landingIndex);
            yield return CardAnimator.MoveTransform(flight.transform, startPos, slotPos, 0.22f);
            _trayView.ReleaseFlightCard(flight);
            _trayView.PlayArrivalPopIn(landingIndex, foodModel);

            // A pair cleared if the tray ended up shorter than "old + this one".
            bool matched = newTray.Count < oldTray.Count + 1;
            if (matched)
            {
                var now = DateTime.UtcNow;
                bool isCombo = _lastMatchTime.HasValue && (now - _lastMatchTime.Value).TotalSeconds <= ComboWindowSeconds;
                _comboCount = isCombo ? _comboCount + 1 : 1;
                _lastMatchTime = now;
                _matchCelebration?.PlayMatchCelebration(slotPos, isCombo);
                ComboChanged?.Invoke(_comboCount);
                yield return _trayView.ResolveAfterPush(oldTray, slotId, newTray, _board);
            }

            _boardView.RefreshFreeStates(_board); // newly-uncovered tiles brighten
            ScoreChanged?.Invoke(_board.Score, _board.ComboCount);

            EvaluateEndState();

            IsInputLocked = false;
        }

        private void EvaluateEndState()
        {
            if (_board.Cells.Values.All(c => c.Cleared))
            {
                if (_isDaily) RecordDailyWin();
                else RecordWin();
                _gameOverPopup?.ShowWin(this, _board.Score);
                return;
            }

            // Lose when the tray is full (4 different, no match), or when every
            // remaining tile is already in the tray (stranded - nothing left on
            // the board to complete a pair).
            bool anyOnBoard = _board.Cells.Any(kv => !kv.Value.Cleared && !_board.TrayTileIds.Contains(kv.Key));
            if (_board.IsGameOver || !anyOnBoard)
                _gameOverPopup?.ShowLose(this);
        }

        // Hint: highlight a free board tile that completes a tray pair (or a free
        // same-value pair on the board), consuming a hint charge.
        public void OnHintRequested()
        {
            if (IsInputLocked || _board.IsGameOver) return;
            if (_board.HintsRemaining <= 0) return;
            var (a, b) = TrayHintFinder.FindHint(_board, _slotsById);
            if (a == null) return;
            _board.HintsRemaining -= 1;
            _aidsUsed++;
            _boardView.GetTileView(a)?.Highlight();
            if (b != null) _boardView.GetTileView(b)?.Highlight();
            NotifyUsesChanged();
        }

        // Undo: return the most-recently-collected tile from the tray to the
        // board, consuming an undo charge.
        public void OnUndoRequested()
        {
            if (IsInputLocked || _board.IsGameOver) return;
            var popped = TrayUndo.TryUndo(_board);
            if (popped == null) return;
            _aidsUsed++;
            _boardView.RestoreTiles(new[] { popped }, _board);
            if (_trayView != null) _trayView.RenderTray(_board.TrayTileIds, _board);
            _boardView.RefreshFreeStates(_board);
            NotifyUsesChanged();
        }

        // Shuffle: reshuffle the on-board tile values, consuming a shuffle charge.
        public void OnShuffleRequested()
        {
            if (IsInputLocked || _board.IsGameOver) return;
            var ids = TrayShuffle.Shuffle(_board, _random);
            if (ids == null) return;
            _aidsUsed++;
            _boardView.RefreshTileValues(ids, _board);
            NotifyUsesChanged();
        }

        // On a win: score the attempt (stars), record it (best stars + unlock the
        // next level), persist, and advance the current level for the next play.
        private void RecordWin()
        {
            if (_progress == null) _progress = new GameProgress();
            var levelData = LevelCatalog.Get(_currentLevelId) ?? LevelCatalog.Levels[0];
            int stars = StarRating.Evaluate(levelData, _aidsUsed, won: true);
            int next = LevelCatalog.NextLevelId(_currentLevelId);
            _progress.RecordResult(_currentLevelId, stars, next);
            SaveSystem.Save(_progress);
            _currentLevelId = next;
        }

        // Daily win: mark today's daily complete and persist; does not touch the
        // main level progression.
        private void RecordDailyWin()
        {
            if (_progress == null) _progress = new GameProgress();
            _progress.MarkDailyDone(DailyChallenge.DateKey(DateTime.Now));
            SaveSystem.Save(_progress);
        }

        private void NotifyUsesChanged()
        {
            UsesChanged?.Invoke(_board.HintsRemaining, _board.UndosRemaining, _board.ShufflesRemaining);
        }
    }
}
