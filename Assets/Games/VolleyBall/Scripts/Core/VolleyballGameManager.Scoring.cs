using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Rehab.Volleyball.Data;
using Rehab.Volleyball.Mechanics;

namespace Rehab.Volleyball.Core
{
    public partial class VolleyballGameManager
    {
        // ═══════════════════════════════════════════════════════
        // NET CROSSING (tracking only — no scoring)
        // ═══════════════════════════════════════════════════════

        private void HandleUnderNetFault(bool crossedToAI)
        {
            if (state != GameState.RallyActive) return;
            state = GameState.PointScored;
            
            if (crossedToAI)
            {
                Debug.Log("[GameManager] PLAYER FAULT! Ball went under the net.");
                AwardPointToAI();
            }
            else
            {
                Debug.Log("[GameManager] AI FAULT! Ball went under the net.");
                AwardPointToPlayer();
            }
            
            OnScoreUpdated?.Invoke();
            CheckForMatchWinner();

            if (!isMatchOver)
            {
                opponentAI.ResetPosition();
                TransitionToServe();
            }
        }

        private void HandleBallCrossedToAI()
        {
            playerConsecutiveHits = 0;
            if (activeBall.LastHitter == BallHitter.Player)
            {
                Debug.Log("[GameManager] Ball crossed to AI side!");
                CurrentRallyCount++;
                if (CurrentRallyCount > BestRallyCount) BestRallyCount = CurrentRallyCount;
                
                consecutiveDrops = 0;
                OnScoreUpdated?.Invoke();

            }
        }

        private void HandleBallCrossedToPlayer()
        {
            if (activeBall.LastHitter == BallHitter.AI)
            {
                Debug.Log("[GameManager] Ball crossed to Player side!");
                CurrentRallyCount++;
                if (CurrentRallyCount > BestRallyCount) BestRallyCount = CurrentRallyCount;
                
                consecutiveDrops = 0;
                OnScoreUpdated?.Invoke();

            }
        }

        // ═══════════════════════════════════════════════════════
        // SCORING — Only triggered by floor collision via OnCollisionEnter
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Called when the ball hits the floor. This is the ONLY place points are scored.
        /// Triple-guarded: state check prevents double-fire from bounce collisions.
        /// </summary>
        public void HandleBallDropped(VolleyballBall ball)
        {
            // GUARD 1: Match over
            if (isMatchOver) return;

            // GUARD 2: Only score during rally or player serving (serve dropped)
            if (state != GameState.RallyActive && state != GameState.PlayerServing) return;

            // GUARD 3: Instantly lock state to prevent any further scoring calls
            state = GameState.PointScored;

            Debug.Log($"[GameManager] Ball dropped! LastHitter={ball.LastHitter}, Pos={ball.transform.position}");

            // Determine where the ball landed
            bool isOut = false;
            bool landedOnAISide = false;

            if (aiCourtBounds != null && playerCourtBounds != null)
            {
                bool inAI = IsPointInCourtBounds(aiCourtBounds, ball.transform.position);
                bool inPlayer = IsPointInCourtBounds(playerCourtBounds, ball.transform.position);

                if (!inAI && !inPlayer)
                {
                    isOut = true;
                    landedOnAISide = (ball.LastHitter == BallHitter.Player);
                }
                else
                {
                    landedOnAISide = inAI;
                }
            }
            else
            {
                float netZ = netTransform != null ? netTransform.position.z : 5.0f;
                landedOnAISide = ball.transform.position.z > netZ;
            }

            // ── Award the point ──
            if (isOut)
            {
                if (ball.LastHitter == BallHitter.Player)
                {
                    Debug.Log("[GameManager] OUT! Player hit it out.");
                    AwardPointToAI();
                }
                else if (ball.LastHitter == BallHitter.AI)
                {
                    Debug.Log("[GameManager] OUT! AI hit it out.");
                    AwardPointToPlayer();
                }
                else
                {
                    // Nobody hit it (serve fell off without being struck)
                    Debug.Log("[GameManager] Ball fell without being hit. Server loses point.");
                    if (isPlayerServe) AwardPointToAI();
                    else AwardPointToPlayer();
                }
            }
            else if (landedOnAISide)
            {
                Debug.Log("[GameManager] POINT PLAYER! Ball landed on AI court.");
                AwardPointToPlayer();
            }
            else
            {
                Debug.Log("[GameManager] POINT AI! Ball landed on player court.");
                AwardPointToAI();
            }

            // Rally ends when ball drops
            CurrentRallyCount = 0;

            OnScoreUpdated?.Invoke();
            CheckForMatchWinner();

            if (!isMatchOver)
            {
                opponentAI.ResetPosition();
                TransitionToServe();
            }
        }

        private void AwardPointToPlayer()
        {
            PlayerScore++;
            isPlayerServe = true;
            consecutiveDrops = 0;
            
            // Win streak tracking
            CurrentWinStreak++;
            if (CurrentWinStreak > BestWinStreak) BestWinStreak = CurrentWinStreak;

            
            if (VolleyballEffectsManager.Instance != null && netTransform != null)
                VolleyballEffectsManager.Instance.PlayPlayerScored(netTransform.position);
        }

        private void AwardPointToAI()
        {
            AIScore++;
            isPlayerServe = false;
            CurrentWinStreak = 0;
            consecutiveDrops++;
            
            if (VolleyballEffectsManager.Instance != null)
                VolleyballEffectsManager.Instance.PlayAIScored();

        }

        // ═══════════════════════════════════════════════════════
        // STRIKE HANDLERS
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Called by VolleyballHand BEFORE it launches the ball.
        /// Returns true if the strike is accepted (game is in a hittable state).
        /// Returns false if rejected (point already scored, waiting for serve, etc.)
        /// This prevents ghost hits, double sounds, and wrong scoring.
        /// </summary>
        public bool TryPlayerStrike()
        {
            // Only accept strikes during active rally or during player serve
            if (state != GameState.RallyActive && state != GameState.PlayerServing) return false;

            bool isServe = (state == GameState.PlayerServing);

            // Transition from PlayerServing → RallyActive on first hit
            state = GameState.RallyActive;
            lastStrikeTime = Time.time;

            if (!allowDoubleHits)
            {
                playerConsecutiveHits++;
                if (playerConsecutiveHits > 1)
                {
                    Debug.LogWarning("[GameManager] DOUBLE HIT FAULT!");
                    HandleBallDropped(activeBall);
                    return false; // Rejected — fault was triggered
                }
            }

            opponentAI.OnPlayerHitBall(isServe);
            return true; // Accepted — hand can now launch the ball
        }

        /// <summary>
        /// Called when the AI successfully hits the ball.
        /// </summary>
        public void HandleOpponentStrike()
        {
            if (state != GameState.RallyActive) return;
            lastStrikeTime = Time.time;
            playerConsecutiveHits = 0;
            Debug.Log("[GameManager] AI struck the ball!");
        }

        // ═══════════════════════════════════════════════════════
        // MATCH MANAGEMENT
        // ═══════════════════════════════════════════════════════

        private void CheckForMatchWinner()
        {
            if (VolleyballLevelDirector.Instance != null && VolleyballLevelDirector.Instance.IsLevelRunning)
            {
                // Level Director handles end of rounds and end of match
                return;
            }

            if (PlayerScore >= pointsToWin && (PlayerScore - AIScore) >= 2)
                HandleMatchOver("PLAYER WINS!");
            else if (AIScore >= pointsToWin && (AIScore - PlayerScore) >= 2)
                HandleMatchOver("OPPONENT WINS!");
        }

        private void HandleMatchOver(string winMessage)
        {
            isMatchOver = true;
            state = GameState.PointScored;
            Debug.Log($"[GameManager] MATCH OVER: {winMessage}");

            // Handle UI Canvases
            if (scoreBoardContent != null) scoreBoardContent.SetActive(false);
            if (endCanvas != null) 
            {
                endCanvas.SetActive(true);
                
                // Teleport the player directly in front of the board (or to a custom point) so they can click it!
                if (playerTransform != null)
                {
                    // CRITICAL FIX: VR Rigs often use a CharacterController. You MUST disable it to teleport!
                    CharacterController cc = playerTransform.GetComponentInChildren<CharacterController>();
                    if (cc != null) cc.enabled = false;

                    if (endGameTeleportPoint != null)
                    {
                        // Use the exact custom transform assigned in the inspector
                        playerTransform.position = endGameTeleportPoint.position;
                        playerTransform.rotation = endGameTeleportPoint.rotation;
                    }
                    else
                    {
                        // Fallback: Calculate a position 6 meters directly in front of the board
                        Vector3 boardFrontPos = endCanvas.transform.position - (endCanvas.transform.forward * 6.0f);
                        boardFrontPos.y = playerCourtPosition.y; // Keep the player on the floor!
                        
                        playerTransform.position = boardFrontPos;
                        
                        // Make the player face the board
                        Vector3 lookDir = endCanvas.transform.position - playerTransform.position;
                        lookDir.y = 0; // Don't tilt the VR player!
                        if (lookDir.sqrMagnitude > 0.01f)
                        {
                            playerTransform.rotation = Quaternion.LookRotation(lookDir);
                        }
                    }
                    
                    if (cc != null) cc.enabled = true;
                }
            }

            if (VolleyballEffectsManager.Instance != null)
            {
                if (PlayerScore > AIScore) VolleyballEffectsManager.Instance.PlayMatchWin();
                else VolleyballEffectsManager.Instance.PlayMatchLose();
            }

            OnMatchOver?.Invoke(winMessage);

            // Cancel any pending serve to prevent stale coroutines
            if (activeServeCoroutine != null)
            {
                StopCoroutine(activeServeCoroutine);
                activeServeCoroutine = null;
            }
        }

        public void ResetMatchScores()
        {
            PlayerScore = 0;
            AIScore = 0;
            CurrentWinStreak = 0;
            consecutiveDrops = 0;
            isPlayerServe = false;
            playerConsecutiveHits = 0;
            OnScoreUpdated?.Invoke();
            
            TransitionToServe(isFirstServe: true);
        }

        public void ForceMatchOver()
        {
            HandleMatchOver("LEVEL COMPLETE");
        }

        public void RestartMatch()
        {
            PlayerScore = 0;
            AIScore = 0;
            CurrentRallyCount = 0;
            CurrentWinStreak = 0;
            consecutiveDrops = 0;
            isMatchOver = false;
            playerConsecutiveHits = 0;
            
            // Fix: Ensure Dog gets the first serve on rematch too!
            isPlayerServe = false;

            // Handle UI Canvases
            if (endCanvas != null) endCanvas.SetActive(false);
            if (scoreBoardContent != null) scoreBoardContent.SetActive(true);

            // Teleport player back to the court!
            if (playerTransform != null)
            {
                CharacterController cc = playerTransform.GetComponentInChildren<CharacterController>();
                if (cc != null) cc.enabled = false;
                
                playerTransform.position = playerCourtPosition;
                playerTransform.rotation = playerCourtRotation;
                
                if (cc != null) cc.enabled = true;
            }

            OnScoreUpdated?.Invoke();
            opponentAI.ResetPosition();
            
            // Start the first serve immediately
            TransitionToServe(isFirstServe: true);
        }

        public void BumpDifficulty()
        {
            if (difficultyMode < DifficultyMode.Hard)
            {
                difficultyMode++;
                
                if (VolleyballEffectsManager.Instance != null)
                {
                    VolleyballEffectsManager.Instance.PlayLevelUp();
                }
                
                Debug.Log($"[GameManager] Difficulty bumped to {difficultyMode}");
            }
        }
    }
}
