using System.Collections;
using UnityEngine;

namespace FateOfTheFallen
{
    internal static class CoroutineHost
    {
        private sealed class Runner : MonoBehaviour
        {
        }

        private static Runner _runner;

        internal static Coroutine Start(
            IEnumerator routine)
        {
            if (routine == null)
            {
                return null;
            }

            EnsureRunner();

            return _runner.StartCoroutine(routine);
        }

        internal static void Stop(
            Coroutine coroutine)
        {
            if (_runner == null ||
                coroutine == null)
            {
                return;
            }

            _runner.StopCoroutine(coroutine);
        }

        internal static void Shutdown()
        {
            if (_runner == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(
                _runner.gameObject);

            _runner = null;
        }

        private static void EnsureRunner()
        {
            if (_runner != null)
            {
                return;
            }

            GameObject host =
                new GameObject(
                    "FateOfTheFallen_CoroutineHost");

            UnityEngine.Object.DontDestroyOnLoad(host);

            _runner =
                host.AddComponent<Runner>();
        }
    }
}
