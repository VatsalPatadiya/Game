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

        // Presentation-only streak timer for picking which praise-text tier
        // to show (see MatchCelebrationController) - deliberately separate
        // from the domain-layer ComboScorer/BoardState.ComboCount, since
        // TrayManager already scores each match with its own flat award and
        // wiring ComboScorer in here too would double-count points. This
        // timer never touches _board.Score.
        private const double ComboWindowSeconds = 3.0;
        private DateTime? _lastMatchTime;

        private BoardState _board;
        private List<TileSlot> _shape;
        private Dictionary<string, TileSlot> _slotsById;
        private readonly ComboScorer _comboScorer = new ComboScorer();

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
                TileSetId = "default"
            };

            _board = BoardGenerator.Generate(level, new System.Random());
            _lastMatchTime = null;

            if (_trayView != null)
                _trayView.Initialize(4); // 4 slots

            IsInputLocked = true;
            _boardView.Build(_board, _slotsById, animateDealIn: true, onDealInComplete: () => IsInputLocked = false);

            ScoreChanged?.Invoke(_board.Score, _board.ComboCount);
            NotifyUsesChanged();
        }

        public void OnTileTapped(string slotId)
        {
            if (_board.IsGameOver) return;
            if (IsInputLocked) return;

            // Compute remaining tiles (not cleared and not in tray)
            var remaining = new HashSet<string>(
                _board.Cells.Where(kv => !kv.Value.Cleared && !_board.TrayTileIds.Contains(kv.Key)).Select(kv => kv.Key));

            if (!FreedomRuleCalculator.IsFree(_slotsById[slotId], remaining))
            {
                _boardView.GetTileView(slotId)?.PlayShake();
                return;
            }

            StartCoroutine(TapToTrayRoutine(slotId));
        }

        // Tap-confirm flash + quick scale-down-fade on the board tile (at its
        // own position - see TileView.PlayTapAway) while the tray slot pops
        // in concurrently - replaces the round-2 cross-screen flying-proxy,
        // which footage showed doesn't match how fast the real transition
        // reads (well under 150ms, no catchable in-transit frame). The
        // domain push still only runs *after* that settles, so
        // TrayManager.TryPushToTray is called exactly once, just later than
        // an instant tap would.
        private IEnumerator TapToTrayRoutine(string slotId)
        {
            IsInputLocked = true;

            var tileView = _boardView.GetTileView(slotId);
            string value = _board.Cells[slotId].Value;
            var icon = TileVisual.IconFor(_boardView.TileSet, value);
            var accentColor = TileVisual.AccentColorFor(_boardView.TileSet, value);
            var oldTrayIds = new List<string>(_board.TrayTileIds);
            int targetIndex = oldTrayIds.Count;

            bool tileAwayDone = false;
            tileView?.PlayTapAway(() => tileAwayDone = true);
            _trayView.PlayArrivalPopIn(targetIndex, icon, accentColor);

            yield return new WaitUntil(() => tileAwayDone || tileView == null);

            bool pushed = TrayManager.TryPushToTray(_board, _slotsById, slotId);
            if (!pushed)
            {
                // Shouldn't happen given the pre-check and the input lock
                // (nothing else can mutate the board mid-flight), but recover
                // gracefully rather than leaving the tile permanently invisible.
                tileView?.PlayFadeInOnly();
                IsInputLocked = false;
                yield break;
            }

            _boardView.RemoveTileInstant(slotId);

            bool matched = _board.TrayTileIds.Count < oldTrayIds.Count + 1;
            if (matched && _matchCelebration != null)
            {
                var now = DateTime.UtcNow;
                bool isCombo = _lastMatchTime.HasValue && (now - _lastMatchTime.Value).TotalSeconds <= ComboWindowSeconds;
                _lastMatchTime = now;
                _matchCelebration.PlayMatchCelebration(_trayView.GetSlotWorldPosition(targetIndex), isCombo);
            }

            yield return _trayView.ResolveAfterPush(oldTrayIds, slotId, _board.TrayTileIds, _board);

            _boardView.RefreshFreeStates(_board);
            ScoreChanged?.Invoke(_board.Score, _board.ComboCount);

            CheckEndOfLevel();

            IsInputLocked = false;
        }

        private void CheckEndOfLevel()
        {
            if (_board.Cells.Values.All(c => c.Cleared))
            {
                if (_gameOverPopup != null)
                    _gameOverPopup.ShowWin(this, _board.Score);
            }
            else if (_board.IsGameOver)
            {
                if (_gameOverPopup != null)
                    _gameOverPopup.ShowStuck(this);
            }
        }

        public void OnHintRequested()
        {
            if (IsInputLocked) return;
            if (_board.HintsRemaining <= 0) return;

            var hint = HintFinder.FindFreePair(_board, _slotsById);
            if (!hint.HasValue) return;

            _board.HintsRemaining -= 1;

            // Hint shouldn't use highlight anymore if tiles move to tray directly,
            // or maybe it highlights a free tile on the board that matches something in the tray?
            // For now, let's just highlight a free pair on the board.
            _boardView.GetTileView(hint.Value.slotIdA)?.Highlight();
            _boardView.GetTileView(hint.Value.slotIdB)?.Highlight();
            NotifyUsesChanged();
        }

        public void OnUndoRequested()
        {
            if (IsInputLocked) return;

            if (UndoStack.TryUndo(_board))
            {
                _boardView.Build(_board, _slotsById, animateDealIn: false);
                ScoreChanged?.Invoke(_board.Score, _board.ComboCount);
                NotifyUsesChanged();
            }
        }

        public void OnShuffleRequested()
        {
            if (IsInputLocked) return;

            try
            {
                if (ShuffleService.Shuffle(_board, _shape, new System.Random()))
                {
                    _boardView.Build(_board, _slotsById, animateDealIn: false);
                    NotifyUsesChanged();
                }
            }
            catch (BoardGenerationException ex)
            {
                Debug.LogWarning("Shuffle could not find a solvable arrangement, board left unchanged: " + ex.Message);
            }
        }

        private void NotifyUsesChanged()
        {
            UsesChanged?.Invoke(_board.HintsRemaining, _board.UndosRemaining, _board.ShufflesRemaining);
        }
    }
}
