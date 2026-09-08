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

        // Presentation-only streak timer: picks the praise-text tier and drives
        // the combo meter (via ComboChanged). Deliberately separate from any
        // domain scoring - TrayManager awards points itself, so this never
        // touches _board.Score.
        private const double ComboWindowSeconds = 3.0;
        private DateTime? _lastMatchTime;
        private int _comboCount;
        private readonly System.Random _random = new System.Random();

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
            // when the player taps Play.
        }

        // Entry point from the level-start screen's Play button.
        public void BeginLevel() => LoadLevel();

        public void RestartLevel()
        {
            if (_gameOverPopup != null)
                _gameOverPopup.Hide();
            LoadLevel();
        }

        private void LoadLevel()
        {
            _shape = TurtleShapeBuilder.Build();
            _slotsById = _shape.ToDictionary(s => s.Id);

            var level = new LevelDefinition
            {
                LevelId = 999,
                Shape = _shape,
                TileSetId = "default"
            };

            // Pair-match tray: values come in pairs so two identical tiles
            // collected in the tray clear together.
            _board = BoardGenerator.Generate(level, _random);
            _lastMatchTime = null;
            _comboCount = 0;

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
            _boardView.RefreshTileValues(ids, _board);
            NotifyUsesChanged();
        }

        private void NotifyUsesChanged()
        {
            UsesChanged?.Invoke(_board.HintsRemaining, _board.UndosRemaining, _board.ShufflesRemaining);
        }
    }
}
