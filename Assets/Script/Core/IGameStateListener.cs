namespace WhackAMole
{
    /// <summary>
    /// All possible states a rehabilitation session can be in.
    /// Transition order: Calibration -> Ready -> Playing -> Finished (or Paused mid-session).
    /// </summary>
    public enum GameState
    {
        Calibration,
        Ready,
        Playing,
        Paused,
        Finished
    }

    /// <summary>
    /// Observer interface for any system that needs to react to session state changes.
    /// Decouples the GameManager from downstream consumers like UI, audio, and spawning.
    /// </summary>
    public interface IGameStateListener
    {
        void OnGameStateChanged(GameState newState);
    }
}
