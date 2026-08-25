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
        [SerializeField] private LevelShapeAsset _levelShape;

        private BoardState _board;
        private List<TileSlot> _shape;
        private Dictionary<string, TileSlot> _slotsById;
        private string _selectedSlotId;
        private readonly ComboScorer _comboScorer = new ComboScorer();

        public event Action<int, int> ScoreChanged;

        private void Start()
        {
            LoadLevel();
        }

        private void LoadLevel()
        {
            _shape = LayeredRowShapeBuilder.Build(_levelShape.RowLengthsByLayer);
            _slotsById = _shape.ToDictionary(s => s.Id);

            var level = new LevelDefinition
            {
                LevelId = _levelShape.LevelId,
                Shape = _shape,
                TileSetId = _levelShape.TileSetId
            };

            _board = BoardGenerator.Generate(level, new System.Random());
            _boardView.Build(_board, _slotsById);
            ScoreChanged?.Invoke(_board.Score, _board.ComboCount);
        }

        public void OnTileTapped(string slotId)
        {
            if (_selectedSlotId == null)
            {
                _selectedSlotId = slotId;
                _boardView.GetTileView(slotId)?.Highlight();
                return;
            }

            if (_selectedSlotId == slotId)
            {
                _selectedSlotId = null;
                _boardView.RefreshFreeStates(_board);
                return;
            }

            string firstSelected = _selectedSlotId;
            _selectedSlotId = null;

            bool matched = MatchValidator.TryMatch(_board, _slotsById, firstSelected, slotId);
            if (matched)
            {
                _comboScorer.RegisterMatch(_board, DateTime.UtcNow);
                _boardView.RemoveTiles(new[] { firstSelected, slotId });
                ScoreChanged?.Invoke(_board.Score, _board.ComboCount);
            }

            _boardView.RefreshFreeStates(_board);
        }

        public void OnHintRequested()
        {
            var hint = HintFinder.FindFreePair(_board, _slotsById);
            if (!hint.HasValue) return;

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
