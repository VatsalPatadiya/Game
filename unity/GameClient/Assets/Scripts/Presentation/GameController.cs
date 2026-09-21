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

        [Header("Audio")]
        private AudioSource _audioSource;
        private AudioClip _tilesSettledClip;
        private AudioClip _matchCelebrationClip;
        private AudioClip _invalidTapClip;

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
        // Fired once per level load with the score a full board clear will
        // reach (see LoadLevel) - lets the progress bar's fill be calibrated
        // to the actual level instead of a guessed fixed constant.
        public event Action<int> MaxScoreChanged;
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

        // Set by the pause menu's sound toggle so it takes effect immediately,
        // without waiting for the next scene load to re-read SettingsData.
        public void SetSoundEnabled(bool enabled) => _audioSource.mute = !enabled;

        private void Awake()
        {
            // Ensure an AudioListener exists (required for any audio output).
            if (Camera.main != null && Camera.main.GetComponent<AudioListener>() == null)
                Camera.main.gameObject.AddComponent<AudioListener>();

            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.volume = 1.0f;
            _audioSource.spatialBlend = 0f; // Force 2D – no 3D distance rolloff
            _audioSource.mute = !SaveSystem.LoadSettings().SoundOn;

            _tilesSettledClip = Resources.Load<AudioClip>("SFX/TilesSettled");
            if (_tilesSettledClip != null)
                _tilesSettledClip.LoadAudioData();

            _matchCelebrationClip = Resources.Load<AudioClip>("SFX/MatchCelebration");
            if (_matchCelebrationClip != null)
                _matchCelebrationClip.LoadAudioData();

            _invalidTapClip = Resources.Load<AudioClip>("SFX/InvalidTap");
            if (_invalidTapClip != null)
                _invalidTapClip.LoadAudioData();

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
                // The big showcase pyramid, capped at the project MAX of 54 tiles so
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
            
            // Play the level-start sound once.
            if (_tilesSettledClip != null && _audioSource != null)
                _audioSource.PlayOneShot(_tilesSettledClip);
            
            _boardView.Build(_board, _slotsById, animateDealIn: true, onDealInComplete: () => {
                IsInputLocked = false;
            });

            // Every match is worth a flat 100 points (TrayManager.TryPushToTray) -
            // ComboScorer's streak multiplier is dead code, never invoked - so a
            // full clear's total score is always exactly (tile count / MatchSize)
            // * 100. Telling the progress bar this per level (instead of a fixed
            // guessed constant) is what makes the fill reach exactly 100% on the
            // last match regardless of the level's size.
            int maxScore = (_board.Cells.Count / TrayManager.MatchSize) * 100;
            MaxScoreChanged?.Invoke(maxScore);
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
#if UNITY_ANDROID || UNITY_IOS
                if (SaveSystem.LoadSettings().VibrationOn) Handheld.Vibrate();
#endif
                if (_invalidTapClip != null && _audioSource != null)
                    _audioSource.PlayOneShot(_invalidTapClip);
                return;
            }



            var newTray = new List<string>(_board.TrayTileIds);
            StartCoroutine(AnimateTapToTray(slotId, oldTray, newTray));
        }

        private IEnumerator AnimateTapToTray(string slotId, List<string> oldTray, List<string> newTray)
        {
            IsInputLocked = true;

            var value = _board.Cells[slotId].Value;
            var tileSprite = TileVisual.IconFor(_boardView.TileSet, value);

            var tileView = _boardView.GetTileView(slotId);
            Vector3 startPos = tileView != null
                ? tileView.transform.position
                : _trayView.GetSlotWorldPosition(0);

            // Give the tapped tile a beat of feedback (flash + shrink) before it
            // leaves the board, instead of vanishing with no acknowledgement.
            if (tileView != null)
            {
                bool tapAwayDone = false;
                tileView.PlayTapAway(() => tapAwayDone = true);
                yield return new WaitUntil(() => tapAwayDone);
            }

            // The tile now lives in the tray (domain-side), so take it off the board.
            _boardView.RemoveTileInstant(slotId);

            // Fly a card from the board up to the slot it landed in, on the same
            // arced/smoothstep curve as the undo flight (CardAnimator.
            // MoveTransformSmooth) instead of a dead-straight lerp - board tiles
            // can be far from the tray, and a straight line covered in ~220ms
            // reads as a teleport/pop rather than a flight. The tray's arrival
            // pop-in starts before the flight lands (see CardAnimator.
            // TrayArrivalOverlapFraction) so the two read as one continuous motion.
            int landingIndex = oldTray.Count;
            var flight = _trayView.SpawnFlightCard(tileSprite, startPos);
            Vector3 slotPos = _trayView.GetSlotWorldPosition(landingIndex);
            var flightRoutine = StartCoroutine(CardAnimator.MoveTransformSmooth(
                flight.transform, startPos, slotPos, Quaternion.identity, CardAnimator.TrayFlightDuration));
            yield return new WaitForSeconds(CardAnimator.TrayFlightDuration * CardAnimator.TrayArrivalOverlapFraction);
            _trayView.PlayArrivalPopIn(landingIndex, tileSprite);
            yield return flightRoutine;
            _trayView.ReleaseFlightCard(flight);

            // A pair cleared if the tray ended up shorter than "old + this one".
            bool matched = newTray.Count < oldTray.Count + 1;
            if (matched)
            {
                var now = DateTime.UtcNow;
                bool isCombo = _lastMatchTime.HasValue && (now - _lastMatchTime.Value).TotalSeconds <= ComboWindowSeconds;
                _comboCount = isCombo ? _comboCount + 1 : 1;
                _lastMatchTime = now;
                
                if (_matchCelebrationClip != null && _audioSource != null)
                    _audioSource.PlayOneShot(_matchCelebrationClip);
                    
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
                // Daily challenge has no par/aids-based rating, so it shows no
                // stars (negative = hide the row) rather than a misleading count.
                int stars;
                if (_isDaily) { RecordDailyWin(); stars = -1; }
                else stars = RecordWin();
                _gameOverPopup?.ShowWin(this, _board.Score, stars);
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
        // board with a smooth reverse-flight animation, consuming an undo charge.
        public void OnUndoRequested()
        {
            if (IsInputLocked || _board.IsGameOver) return;
            if (_board.UndosRemaining <= 0 || _board.TrayTileIds.Count == 0) return;

            StartCoroutine(AnimateUndo());
        }

        private IEnumerator AnimateUndo()
        {
            IsInputLocked = true;

            int trayIndex = _board.TrayTileIds.Count - 1;
            string popped = TrayUndo.TryUndo(_board);
            if (popped == null)
            {
                IsInputLocked = false;
                yield break;
            }

            _aidsUsed++;
            NotifyUsesChanged();

            Vector3 trayPos = _trayView != null && trayIndex >= 0
                ? _trayView.GetSlotWorldPosition(trayIndex)
                : Vector3.zero;

            // Immediately update the tray slots so the undone tile cleanly lifts off from its slot
            if (_trayView != null)
            {
                _trayView.RenderTray(_board.TrayTileIds, _board);
            }

            // Restore the tile on the board, initially deactivated until the flight lands
            var tileView = _boardView.RestoreTile(popped, _board);
            if (tileView != null)
            {
                tileView.gameObject.SetActive(false);
            }

            Vector3 targetBoardPos = tileView != null
                ? tileView.transform.position
                : trayPos;
            Quaternion targetBoardRot = tileView != null
                ? tileView.transform.rotation
                : Quaternion.identity;

            var value = _board.Cells[popped].Value;
            var tileSprite = TileVisual.IconFor(_boardView.TileSet, value);

            if (_trayView != null)
            {
                var flight = _trayView.SpawnFlightCard(tileSprite, trayPos);
                yield return CardAnimator.MoveTransformSmooth(
                    flight.transform, trayPos, targetBoardPos, targetBoardRot, CardAnimator.UndoFlightDuration);
                _trayView.ReleaseFlightCard(flight);
            }

            if (tileView != null)
            {
                tileView.gameObject.SetActive(true);
                tileView.PlayPopSettle();
            }

            _boardView.RefreshFreeStates(_board);
            IsInputLocked = false;
        }

        // Shuffle: reshuffle the on-board tile values, consuming a shuffle charge.
        public void OnShuffleRequested()
{
    if (IsInputLocked || _board.IsGameOver) return;

    // Attempt shuffle; if no shuffles left, abort.
    bool shuffled = ShuffleService.Shuffle(_board, _shape, _random);
    if (!shuffled) return;

    // Record shuffle usage as an aid.
    _aidsUsed++;

    // Commit current progress.
    SaveSystem.Save(_progress);

    // Lock input and play the level‑start sound.
    IsInputLocked = true;
    if (_tilesSettledClip != null && _audioSource != null)
        _audioSource.PlayOneShot(_tilesSettledClip);

    // Re‑build the board with the standard deal‑in animation.
    _boardView.Build(_board, _slotsById, animateDealIn: true, onDealInComplete: () =>
    {
        IsInputLocked = false;
    });

    NotifyUsesChanged();
}
        

        // On a win: score the attempt (stars), record it (best stars + unlock the
        // next level), persist, and advance the current level for the next play.
        private int RecordWin()
        {
            if (_progress == null) _progress = new GameProgress();
            var levelData = LevelCatalog.Get(_currentLevelId) ?? LevelCatalog.Levels[0];
            int stars = StarRating.Evaluate(levelData, _aidsUsed, won: true);
            int next = LevelCatalog.NextLevelId(_currentLevelId);
            _progress.RecordResult(_currentLevelId, stars, next);
            SaveSystem.Save(_progress);
            _currentLevelId = next;
            return stars;
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
