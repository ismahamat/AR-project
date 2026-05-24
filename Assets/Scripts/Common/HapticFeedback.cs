using System.Collections;
using UnityEngine;

public static class HapticFeedback
{
    static HapticRunner _runner;

    static HapticRunner Runner
    {
        get
        {
            if (_runner != null) return _runner;
            var go = new GameObject("[HapticRunner]");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            _runner = go.AddComponent<HapticRunner>();
            return _runner;
        }
    }

    public static void Pulse(OVRInput.Controller controller, float frequency = 0.5f, float amplitude = 0.6f, float duration = 0.10f)
    {
        if (duration <= 0f) return;
        Runner.Run(controller, Mathf.Clamp01(frequency), Mathf.Clamp01(amplitude), duration);
    }

    public static void Tap(OVRInput.Controller controller)        => Pulse(controller, 0.7f, 0.7f, 0.05f);
    public static void Bump(OVRInput.Controller controller)       => Pulse(controller, 0.8f, 0.9f, 0.12f);
    public static void Buzz(OVRInput.Controller controller)       => Pulse(controller, 0.4f, 0.5f, 0.30f);

    class HapticRunner : MonoBehaviour
    {
        readonly System.Collections.Generic.Dictionary<OVRInput.Controller, Coroutine> _active = new System.Collections.Generic.Dictionary<OVRInput.Controller, Coroutine>();

        public void Run(OVRInput.Controller c, float freq, float amp, float dur)
        {
            if (_active.TryGetValue(c, out var existing) && existing != null) StopCoroutine(existing);
            _active[c] = StartCoroutine(PulseCoroutine(c, freq, amp, dur));
        }

        IEnumerator PulseCoroutine(OVRInput.Controller c, float freq, float amp, float dur)
        {
            try { OVRInput.SetControllerVibration(freq, amp, c); }
            catch { yield break; }
            yield return new WaitForSecondsRealtime(dur);
            try { OVRInput.SetControllerVibration(0f, 0f, c); }
            catch { }
            _active.Remove(c);
        }

        void OnDisable()
        {
            try
            {
                OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
                OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
            }
            catch { }
        }
    }
}
