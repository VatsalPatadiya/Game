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
using UnityEngine;

namespace GameClient.Presentation
{
    public sealed class GameController : MonoBehaviour
    {
        [SerializeField] private BoardView3D _boardView;
        [SerializeField] private TrayView3D _trayView;
        [SerializeField] private GameOverPopup3D _gameOverPopup;
        [SerializeField] private MatchCelebrationController _matchCelebration;

        // Classic on-board mahjong: tap a free tile to select it, tap a second
        // matching free tile to clear the pair. ComboScorer owns scoring now
        // (the old tray path is gone, so there's no double-count to avoid).
        private string _selectedSlotId; // null = nothing selected
        private ComboScorer _comboScorer;
        private readonly System.Random _random = new System.Random();

        private BoardState _board;
        private List<TileSlot> _shape;
        private Dictionary<string, TileSlot> _slotsById;
        public event Action<int, int> ScoreChanged;
        public event Action<int, int, int> UsesChanged;

        // True while the deal-in animation or a tap's tap-to-tray sequence
        // is still playing, so a second tap (or a hint/undo/shuffle press)
        // can't land mid-animation and desync the board from what's visible.
        public bool IsInputLocked { get; private set; }

        private void Start()
        {
            // vSyncCount must be 0 for targetFrameRate to take effect at all -
            // otherwise Unity ignores it and locks to (display refresh /
            // vSyncCount). Requesting 120 only actually renders at 120 on a
            // device whose display supports it (paired with
            // PlayerSettings.Android.optimizedFramePacing in AndroidBuilder,
            // which asks Android for the higher display mode); on a 60Hz-only
            // panel this same code just runs at that panel's 60Hz ceiling.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 120;

            // The board no longer deals in on scene load - the level-start
            // screen (LevelStartScreen3D) is shown first and calls BeginLevel()
            // when the player taps Play. If no level-start screen is present in
            // the scene, BeginLevel is never auto-called, so the board stays
            // empty; wire one in GameSceneBuilder3D or call BeginLevel directly.
        }

        // Entry point from the level-start screen's Play button (see
        // LevelStartScreen3D). Deals the board in for the first time.
        public void BeginLevel() => LoadLevel();

        public void RestartLevel()
        {
            if (_gameOverPopup != null)
                _gameOverPopup.Hide();
            LoadLevel();
        }

        private void LoadLevel()
        {
            // Turtle silhouette (wide top, hollow twin-pillar middle, wide
            // bottom) - see docs/superpowers/specs/2026-08-26-pyramid-shape-
            // and-tray-correction.md for why this replaced the flat
            // rectangle-plus-bump PyramidShapeBuilder previously used here.
            _shape = TurtleShapeBuilder.Build();
            _slotsById = _shape.ToDictionary(s => s.Id);

            var level = new LevelDefinition
            {
                LevelId = 999, // Use an int ID for the randomized level
                Shape = _shape,
                TileSetId = "default",
                // Placeholder moves budget so the moves-exhausted lose path is
                // exercised before real per-level data exists (sub-project #7).
                MovesBudget = 60
            };

            // On-board pair match: each value appears exactly twice.
            _board = BoardGenerator.Generate(level, _random);
            _comboScorer = new ComboScorer();
            _selectedSlotId = null;

            IsInputLocked = true;
            _boardView.Build(_board, _slotsById, animateDealIn: true, onDealInComplete: () => IsInputLocked = false);

            ScoreChanged?.Invoke(_board.Score, _board.ComboCount);
            NotifyUsesChanged();
        }

        // Classic on-board mahjong: first tap selects a free tile, second tap on a
        // matching free tile clears the pair. Tapping a blocked tile shakes it;
        // tapping the selected tile again deselects; tapping a different free tile
        // whose value differs carries the selection forward (not an error).
        public void OnTileTapped(string slotId)
        {
            if (IsInputLocked) return;
            if (_board.IsGameOver) return;
            if (_board.Cells.Values.All(c => c.Cleared)) return;

            var remaining = new HashSet<string>(
                _board.Cells.Where(kv => !kv.Value.Cleared).Select(kv => kv.Key));
            bool isFree = FreedomRuleCalculator.IsFree(_slotsById[slotId], remaining);
            if (!isFree)
            {
                _boardView.GetTileView(slotId)?.PlayShake();
                return;
            }

            if (_selectedSlotId == null)
            {
                Select(slotId);
                return;
            }

            if (_selectedSlotId == slotId)
            {
                Deselect();
                return;
            }

            string a = _selectedSlotId;
            if (MatchValidator.TryMatch(_board, _slotsById, a, slotId))
            {
                Deselect();
                // Consume a move only on a successful match; unlimited budgets
                // (negative MovesRemaining) are left untouched.
                _board.MovesRemaining = _board.MovesRemaining < 0
                    ? _board.MovesRemaining
                    : _board.MovesRemaining - 1;

                _comboScorer.RegisterMatch(_board, DateTime.UtcNow);
                var pos = _boardView.GetTileView(slotId)?.transform.position ?? Vector3.zero;
                _matchCelebration?.PlayMatchCelebration(pos, _board.ComboCount > 1);

                _boardView.RemoveTiles(new[] { a, slotId });
                _boardView.RefreshFreeStates(_board);
                ScoreChanged?.Invoke(_board.Score, _board.ComboCount);
                EvaluateEndState();
            }
            else
            {
                // Different free tile with a different value: carry selection.
                Deselect();
                Select(slotId);
            }
        }

        private void Select(string slotId)
        {
            _selectedSlotId = slotId;
            _boardView.GetTileView(slotId)?.SetSelected(true);
        }

        private void Deselect()
        {
            if (_selectedSlotId != null)
                _boardView.GetTileView(_selectedSlotId)?.SetSelected(false);
            _selectedSlotId = null;
        }

        private void EvaluateEndState()
        {
            switch (EndStateEvaluator.Evaluate(_board, _slotsById))
            {
                case EndState.Won:
                    _gameOverPopup?.ShowWin(this, _board.Score);
                    break;
                case EndState.Lost:
                    _board.IsGameOver = true;
                    _gameOverPopup?.ShowLose(this);
                    break;
            }
        }

        // Hint: highlight one valid free pair, consuming a hint charge. No-op (and
        // no charge spent) when stuck or out of charges.
        public void OnHintRequested()
        {
            if (IsInputLocked || _board.IsGameOver) return;
            if (_board.HintsRemaining <= 0) return;
            var hint = HintFinder.FindFreePair(_board, _slotsById);
            if (hint == null) return;
            _board.HintsRemaining -= 1;
            _boardView.GetTileView(hint.Value.slotIdA)?.Highlight();
            _boardView.GetTileView(hint.Value.slotIdB)?.Highlight();
            NotifyUsesChanged();
        }

        // Undo: restore the last cleared pair (does NOT refund the spent move),
        // consuming an undo charge.
        public void OnUndoRequested()
        {
            if (IsInputLocked || _board.IsGameOver) return;
            if (_board.UndosRemaining <= 0 || _board.MoveHistory.Count == 0) return;
            var last = _board.MoveHistory[_board.MoveHistory.Count - 1];
            if (!UndoStack.TryUndo(_board)) return;
            Deselect();
            _boardView.RestoreTiles(new[] { last.SlotIdA, last.SlotIdB }, _board);
            ScoreChanged?.Invoke(_board.Score, _board.ComboCount);
            NotifyUsesChanged();
        }

        // Shuffle: reshuffle remaining tiles into a new still-solvable layout,
        // consuming a shuffle charge.
        public void OnShuffleRequested()
        {
            if (IsInputLocked || _board.IsGameOver) return;
            if (_board.ShufflesRemaining <= 0) return;
            var remainingIds = _board.Cells.Where(kv => !kv.Value.Cleared).Select(kv => kv.Key).ToList();
            if (!ShuffleService.Shuffle(_board, _shape, _random)) return;
            Deselect();
            _boardView.RefreshTileValues(remainingIds, _board);
            NotifyUsesChanged();
        }

        private void NotifyUsesChanged()
        {
            UsesChanged?.Invoke(_board.HintsRemaining, _board.UndosRemaining, _board.ShufflesRemaining);
        }
    }
}
