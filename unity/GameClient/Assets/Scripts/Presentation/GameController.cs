using System;
using System.Collections.Generic;
using System.Linq;
using GameClient.Data;
using GameClient.Presentation.Board;
using GameDomain.Gameplay;
using GameDomain.Generation;
using GameDomain.Model;
using UnityEngine;

namespace GameClient.Presentation
{
    public sealed class GameController : MonoBehaviour
    {
        [SerializeField] private BoardView _boardView;
        [SerializeField] private TrayView _trayView;
        [SerializeField] private GameOverPopup _gameOverPopup;

        private BoardState _board;
        private List<TileSlot> _shape;
        private Dictionary<string, TileSlot> _slotsById;
        private readonly ComboScorer _comboScorer = new ComboScorer();

        public event Action<int, int> ScoreChanged;

        private void Start()
        {
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
            // Build a random pyramid with 20 tiles
            _shape = PyramidShapeBuilder.BuildRandom(20, new System.Random());
            _slotsById = _shape.ToDictionary(s => s.Id);

            var level = new LevelDefinition
            {
                LevelId = 999, // Use an int ID for the randomized level
                Shape = _shape,
                TileSetId = "default"
            };

            _board = BoardGenerator.Generate(level, new System.Random());
            
            _boardView.Build(_board, _slotsById);
            if (_trayView != null)
                _trayView.Initialize(4); // 4 slots
                
            ScoreChanged?.Invoke(_board.Score, _board.ComboCount);
        }

        public void OnTileTapped(string slotId)
        {
            if (_board.IsGameOver) return;

            // Compute remaining tiles (not cleared and not in tray)
            var remaining = new HashSet<string>(
                _board.Cells.Where(kv => !kv.Value.Cleared && !_board.TrayTileIds.Contains(kv.Key)).Select(kv => kv.Key));

            if (!FreedomRuleCalculator.IsFree(_slotsById[slotId], remaining))
            {
                _boardView.GetTileView(slotId)?.PlayShake();
                return;
            }

            if (TrayManager.TryPushToTray(_board, _slotsById, slotId))
            {
                // Remove tile from BoardView entirely, or just hide it
                // We'll update BoardView to hide it, and update TrayView to show it
                _boardView.GetTileView(slotId)?.gameObject.SetActive(false);
                
                if (_trayView != null)
                    _trayView.UpdateTray(_board, _slotsById);
                    
                _boardView.RefreshFreeStates(_board);
                ScoreChanged?.Invoke(_board.Score, _board.ComboCount);

                if (_board.IsGameOver)
                {
                    if (_gameOverPopup != null)
                        _gameOverPopup.Show(this);
                }
            }
        }

        public void OnHintRequested()
        {
            var hint = HintFinder.FindFreePair(_board, _slotsById);
            if (!hint.HasValue) return;

            // Hint shouldn't use highlight anymore if tiles move to tray directly,
            // or maybe it highlights a free tile on the board that matches something in the tray?
            // For now, let's just highlight a free pair on the board.
            _boardView.GetTileView(hint.Value.slotIdA)?.Highlight();
            _boardView.GetTileView(hint.Value.slotIdB)?.Highlight();
        }

        public void OnUndoRequested()
        {
            if (UndoStack.TryUndo(_board))
            {
                _boardView.Build(_board, _slotsById);
                ScoreChanged?.Invoke(_board.Score, _board.ComboCount);
            }
        }

        public void OnShuffleRequested()
        {
            try
            {
                ShuffleService.Shuffle(_board, _shape, new System.Random());
                _boardView.Build(_board, _slotsById);
            }
            catch (BoardGenerationException ex)
            {
                Debug.LogWarning("Shuffle could not find a solvable arrangement, board left unchanged: " + ex.Message);
            }
        }
    }
}
