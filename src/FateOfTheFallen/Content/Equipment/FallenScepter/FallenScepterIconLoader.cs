using System;
using UnityEngine;

namespace FateOfTheFallen
{
    internal static class FallenScepterIconLoader
    {
        private const int ExpectedIconSize =
            64;

        // The 64x64 Fallen Scepter PNG is compiled directly into this
        // source file as Base64, so no loose PNG and no EmbeddedResource
        // entry is required in FateOfTheFallen.csproj.
        private const string EmbeddedPngBase64 =
            "iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAYAAACqaXHeAAAKiElEQVR4nO2ZeXRU1R3Hv/dts703b/aZLDNZCA5GMCBIQdCIFqqy" +
            "ynHcacvmvlFtT1s9TdGeLtatdWkrauuGNSlFXFoEeirSqlQBUYGwHBoIhJCNTGZ985bbP/JyECGoEM0k5XPOnDln3u/N/X1/9/d+" +
            "v/vuBU5xilOcYuBBzG/bnaG7f3KFcN33AZBYZUwAwJrX2X7z7iuGUFACGa5lla+9s6Wggca4hdXHsRcncpMnVKHa9XU5+FVCalDD" +
            "AcBC101zjAClj+D5ZQAQkkouLgiUPma3uy8CYAMwfRouW71SWtX8B8dL2zzwOPvT8ZOGghIAqEENA4CdgxtGLcajzwPwAEDAXfzE" +
            "6eWjaEkkqhXaS3e/wCyj8UKVGkFK7yEPPNyfvvcFPc+85bMXquUqFyuzF8KBJyAxSmFJuRouitKnQ89qtIgqa1FvlLHR6V+zv32H" +
            "OeO42rHwm3eJNc8BsLmkwAyfq/BXos+1SpCl5lJxGL1EnE0f5p/S77Xer0uFPg1BXrvGuYDO5W5rBeDrXxUnDgGA062jStaNWN/0" +
            "nv+T91kWM6NlVdTl89Nq+0W0TlpBd4cOaJ2irlJQ/VHHM7pPLtSn2KfpQsChSV6PIdq8s3obgPnapJwAMcQYAHCrwTNGNo0taE20" +
            "xXUdugLFKFLCyivMX9XLymboouYjKU1jsn5gpe1vqLZOwm3K7dA7czleYRVCck4cfoyOIK8DUItaAwAkXd66vP2NzOvZ1T8bWu6c" +
            "SQyDSXnTzPd8d7Brtr9H7J0cmFIFjblD2MZtRSRbru1nWhidUZcISXbY0+mnXwRAjzXGMaOSRxAA1Cf4TpNzZd4zztvth+5cvnZT" +
            "lpa5S8g+2z7i1Jx0bGYMru28Hj7FjYnh0Xig7UktqXUJ91hvXoQEHgEQEFFgJHGg7bMD5HUGwPRPtagT9lp3zfEKzqu2H2hjnIrL" +
            "ePLACxjWUgnDoFjheA33y4vxjOt3YCmHoO4hH7MbAR1dU4pjFUuKX3zzZuGOub0OkMdQAMhpqS12i+3G1hQTS2qKHmHC7C66DfGu" +
            "FBUbZYzsHIlGx16scL+KIhrGUjyPNfzrYAxGvypx+bwF2atHZnLZgVcD0B0AJpPRNmdz2bU+B8u6RcawqRIaSRPaSAu8NIQfHLob" +
            "/i4vOMpBJzrecqyiBtFh8IbeEU+kf922ZPVvUfPMsQbI9xoAdL/U6BxnPW/a2d6VadUQ1n2QxjRmFgnQIlTxw/F752PYqL6DgBqG" +
            "zufA8KwGSriCroKfO9XgQ//CGwkAWn8LOVF6Jkkad5rzwNTRQTp+uEcDoMfY7+jFngodMvR5wq36t3G7RogtJ4pew+8rpGH3EFps" +
            "L33CXEYfM9vzPgNqAGYxYL1iUnktjNzUfa0JzWW3MoksIe/ub6YRuxeT9s8Hw2aNvwjPccEgkM2SdEuc7gGj16eUzC+haR+g+3E6" +
            "qhXmewAYAIbVipJ7rh27++MdTaQjlTXiXTlqFzhaFHAQI8eSrnY72+XaA5IS0j7JsVFJ01ve+HD/5i8yAPcVCzgZSCwGUlcHNuQJ" +
            "PgQAlGpqOq2zibTGpbMaUjkd8YwORVc73AnH9f9tS30QVw41ACA/9N83pz6+o+yV3NZfABvU3gbJxy5AYjGwAJi6OuiCID3u87hm" +
            "b99zUNvbqgiKarCKqq/rSOZu3NqYeaqhlaTbE07PtjZaFVeUhti4cba64KubZ2HmcxbNtgvYoPa8UOU7TG0sdsQ2lsMm3VdcNJQG" +
            "fAVp0SFSluW3s2Cn+O0I9djYef4su01cKjlcOYZn5s8WvnvJEtuf192CH80GDu8jDCQsoyvEG52i+EBxUQUNBsIZh+imHGv9BED5" +
            "p+w4HLnv5+fAjTsTkx09P3wR8flQA1gAerFHuPScqsKpw8Ly8PfrO76xow0w9JyeTiet2Ux6va4rlwI4gG6fdRzu6z3p3apBa/0I" +
            "qwEAtahlCYj+eYPnQ3owAIyAw3JhRcS+5tyRRbBYnWrd2ibsaGwnhqYspzR3HYDOHtte/oeYn96u9zp4f2PU1IBpSSn/ONiRWr1z" +
            "bzvd2dBojCklzNAgm6yM8FsBdNXUHFc80N3jv5R4IC8CUM0tXgxDln0LFBKacCihavUNHZZ9bQlj4pkeJ0NYCQC2bs2LbO1rqjkA" +
            "EEXP/JJIlAYDJZrdLtGygKWpstiunzvcQy1AmWmcB5PVp3SLlyTPvJJw1AgGw4okuqkgWP4EQC5wWr5VEbT+3e2GbN4wmDLgsPhI" +
            "cdQIBrrF87zw+DGMB/nMB8KKJLooz1vuNw16qvlgPNvrFi9LnrmR4qjeM/Msa7nXNOg52ByMfCrtw1E9GAirouiiHCf82DQY/OJl" +
            "0TffLHiq6HBRjhMWmQaDMd17ONzqSsNRPRiMaKLoMjjOcpNpMPjFy919Xg8GwprocGk8b5lnGuTDu0ifY77Pj+YBU3w4aoQCYV0U" +
            "5RzPW68x7Qal+CNwy76F5swbokPO8Dx/uXlpUIo3+7dXmjG+8ErRLt5dWDDECIUi1G53pgRWmGXaDUrxqK7uFjYibFt00/QhdP7U" +
            "SupwuHW73XmIZYVpplneie+rvssC0EtkS6nD7lgfLuS8Q4qc6paGLmbdJy1zDQNLAfAAet2c7C9Oep0dM8UD8DntzhVBlz/QdFA3" +
            "3t7UxPHEEKJhyQqAqe7leLq/OdkM6BFfNKIoVOsWpXM6EnGFGtSSyKZyKlHfTBvM7fG40mDa510QTviZrAa4tYBW7nZPEEXby25R" +
            "KspkM4qiqpaMorQnFD0WV9R/9qWz+QQHADxw1piScOvkEZV0ZHFBJhrw0qDDugFAFOg+1kKer++/tHOjAX4DoArAsGhRwUqHzVKS" +
            "SmcyLCG2znS6reFQ1xgAe9AdpLw/kf3cIhiLgTULHYkB7AZALZSs408vDK32u+QSVdWUtJK1tSeSSjqbXYABJB44gQzw24TLyoOh" +
            "hx12a3FbvCujaKotnkpvbE5mFgDYhONvXecdvRZBSkEIAZ04InBVNpNL7t6vbLpwjGeB3crXbN6uQE8ZOVDD1pXK7GhOZi4BcBCH" +
            "u8KA4ZgBqI3FWELq9NgFQ24ZWiA+urF+PzheT3SldSnotupej6bv3pvkKGXqkzltBrrFD5i0Py7mAQSmjisbe9cVw9Xxw+SdEb+U" +
            "rIgUUa8nqJ011K1dUOXWRpVLFMAF5m15t8Q9YSgFkWXZfcP06N7zz3TXW6y+ZWWlI7RQsExzuUKaU3RpFSGrFpKFRZSCVA9w8Ud1" +
            "AUJAp4z0zmw80Cm8ux3ry8LhWYqSQC6XIjlVZQMy2CGFYrY5nltKCOjaAVTwjsVRAagGuJ37WyrX7Wh7SfY7L44nOqiiZFhVM0g6" +
            "FW8ee5or45asdo5DSX843NccFYDzq2sQ31360fLg27PPU8/1tySa2URS3SUgs+rKSQUZq0Bs9Xs7my0WpM1b8m59f7JIr0fXfEgj" +
            "lL5DdtACMbR68tjAWwsuHpKbNMKj+p3sbwBETNu8XuZ+EY4qYHeG7rluTOrsqn/v+7j1ITz4rBzoIpzh5N/8z76XG9uVtwFsMU0J" +
            "BuHsY77j1j/eRx78KSAOM3/iP309NrgPME5xilP8n/E/mBkIE8Hz4AsAAAAASUVORK5CYII=";

        private static Sprite _cachedSprite;


        internal static Sprite Load()
        {
            if (_cachedSprite != null)
            {
                return _cachedSprite;
            }

            try
            {
                byte[] pngBytes =
                    Convert.FromBase64String(
                        EmbeddedPngBase64);

                Texture2D texture =
                    new Texture2D(
                        2,
                        2,
                        TextureFormat.RGBA32,
                        false);

                texture.name =
                    "FOTF_FallenScepter_Icon_Texture";

                texture.filterMode =
                    FilterMode.Bilinear;

                texture.wrapMode =
                    TextureWrapMode.Clamp;

                bool loaded =
                    ImageConversion.LoadImage(
                        texture,
                        pngBytes,
                        false);

                if (!loaded)
                {
                    UnityEngine.Object.Destroy(
                        texture);

                    Plugin.NativeLog.LogError(
                        "Fallen Scepter icon: Unity failed to decode the icon embedded in the DLL.");

                    return null;
                }

                if (texture.width !=
                        ExpectedIconSize ||
                    texture.height !=
                        ExpectedIconSize)
                {
                    Plugin.NativeLog.LogWarning(
                        "Fallen Scepter icon: expected [" +
                        ExpectedIconSize +
                        "x" +
                        ExpectedIconSize +
                        "] but embedded icon loaded as [" +
                        texture.width +
                        "x" +
                        texture.height +
                        "]. The icon will still be used.");
                }

                _cachedSprite =
                    Sprite.Create(
                        texture,
                        new Rect(
                            0f,
                            0f,
                            texture.width,
                            texture.height),
                        new Vector2(
                            0.5f,
                            0.5f),
                        ExpectedIconSize);

                _cachedSprite.name =
                    "FOTF_FallenScepter_Icon";


                return _cachedSprite;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Fallen Scepter icon: failed to load DLL-embedded icon: " +
                    exception);

                return null;
            }
        }
    }
}
