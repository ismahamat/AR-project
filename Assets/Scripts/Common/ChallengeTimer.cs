using System;
using UnityEngine;

public class ChallengeTimer : MonoBehaviour
{
    public enum Mode { Normal, Handicap }

    [Tooltip("Identifiant unique du défi (ex. \"glaucome\"). Sert de clé PlayerPrefs.")]
    public string challengeKey = "default";

    [Tooltip("Mode courant : Normal sauvegarde le score sans handicap, Handicap avec.")]
    public Mode mode = Mode.Handicap;

    public bool Running { get; private set; }
    public float Elapsed { get; private set; }
    public bool HasResult { get; private set; }

    public event Action<float> OnEnded;

    float _startTime;

    public void Begin()
    {
        Elapsed = 0f;
        HasResult = false;
        Running = true;
        _startTime = Time.unscaledTime;
    }

    public void End()
    {
        if (!Running) return;
        Running = false;
        Elapsed = Time.unscaledTime - _startTime;
        HasResult = true;
        Save(Elapsed);
        OnEnded?.Invoke(Elapsed);
    }

    public void Reset()
    {
        Running = false;
        Elapsed = 0f;
        HasResult = false;
    }

    void Update()
    {
        if (Running) Elapsed = Time.unscaledTime - _startTime;
    }

    void Save(float seconds)
    {
        PlayerPrefs.SetFloat(KeyFor(challengeKey, mode), seconds);
        PlayerPrefs.Save();
    }

    public static string KeyFor(string challenge, Mode m) =>
        $"sim.{challenge}.{(m == Mode.Normal ? "normal" : "handicap")}";

    public static bool TryGet(string challenge, Mode m, out float seconds)
    {
        string k = KeyFor(challenge, m);
        if (PlayerPrefs.HasKey(k)) { seconds = PlayerPrefs.GetFloat(k); return true; }
        seconds = 0f; return false;
    }

    public static void Clear(string challenge)
    {
        PlayerPrefs.DeleteKey(KeyFor(challenge, Mode.Normal));
        PlayerPrefs.DeleteKey(KeyFor(challenge, Mode.Handicap));
        PlayerPrefs.Save();
    }

    public static string Format(float seconds)
    {
        if (seconds < 0f) return "—";
        int m = (int)(seconds / 60f);
        float s = seconds - m * 60f;
        return m > 0 ? $"{m}:{s:00.00}" : $"{s:0.00}s";
    }
}
