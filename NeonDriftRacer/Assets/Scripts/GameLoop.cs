using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Game state machine: Ready → Countdown → Running → Won/Lost.
/// Supports both Input System package and legacy Input Manager.
/// </summary>
public class GameLoop : MonoBehaviour
{
    public enum RunState { Ready, Countdown, Running, Won, Lost, Paused }

    public float countdownDuration = 3f;
    public float maxRaceTime = 300f; // 5 minutes

    [Header("References")]
    public DriftCarController car;
    public LapManager lapManager;

    [Header("Events")]
    public UnityEvent onCountdownStart;
    public UnityEvent<int> onCountdownTick;
    public UnityEvent onRaceStart;
    public UnityEvent onWin;
    public UnityEvent onLose;
    public UnityEvent<RunState> onStateChange;

    public RunState State { get; private set; } = RunState.Ready;
    public float CountdownTimer { get; private set; }

    RunState stateBeforePause;

    void Start()
    {
        if (car == null) car = FindAnyObjectByType<DriftCarController>();
        if (lapManager == null) lapManager = FindAnyObjectByType<LapManager>();

        if (lapManager != null)
            lapManager.onRaceFinish.AddListener(OnRaceFinished);

        SetState(RunState.Ready);
    }

    void Update()
    {
        bool escapePressed = false;
        bool enterPressed = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.escapeKey.wasPressedThisFrame) escapePressed = true;
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) enterPressed = true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (!escapePressed && Input.GetKeyDown(KeyCode.Escape)) escapePressed = true;
        if (!enterPressed && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))) enterPressed = true;
#endif

        if (escapePressed)
        {
            if (State == RunState.Paused)
                Resume();
            else if (State == RunState.Running)
                Pause();
        }

        if (State == RunState.Ready && enterPressed)
            StartCountdown();

        if (State == RunState.Countdown)
        {
            CountdownTimer -= Time.deltaTime;
            int tick = Mathf.CeilToInt(CountdownTimer);
            onCountdownTick?.Invoke(tick);
            if (CountdownTimer <= 0)
                BeginRace();
        }

        if (State == RunState.Running)
        {
            if (lapManager != null && lapManager.TotalTime > maxRaceTime)
            {
                SetState(RunState.Lost);
                onLose?.Invoke();
            }
        }

        if ((State == RunState.Won || State == RunState.Lost) && enterPressed)
            Restart();
    }

    public void StartCountdown()
    {
        CountdownTimer = countdownDuration;
        SetState(RunState.Countdown);
        onCountdownStart?.Invoke();
    }

    void BeginRace()
    {
        SetState(RunState.Running);
        lapManager?.StartRace();
        onRaceStart?.Invoke();
    }

    void OnRaceFinished()
    {
        SetState(RunState.Won);
        onWin?.Invoke();
    }

    public void Pause()
    {
        stateBeforePause = State;
        SetState(RunState.Paused);
        Time.timeScale = 0;
    }

    public void Resume()
    {
        Time.timeScale = 1;
        SetState(stateBeforePause);
    }

    public void Restart()
    {
        Time.timeScale = 1;
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    void SetState(RunState s)
    {
        State = s;
        onStateChange?.Invoke(s);
    }
}
