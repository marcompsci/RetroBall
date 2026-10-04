using UnityEngine;

namespace CallerRetroBall.Core
{
    /// <summary>
    /// The app is portrait everywhere except Full Court 5-on-5, which turns the screen to landscape.
    /// The project allows portrait and both landscapes (ReleaseTools.EnsureOrientations) and this locks
    /// the one wanted at the time, so the phone's rotation lock and tilting don't flip menus around.
    /// </summary>
    public static class Orientation
    {
        public static bool IsLandscape => Screen.width > Screen.height;

        public static void Portrait()
        {
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToPortrait = true;
            if (Screen.orientation != ScreenOrientation.Portrait) Screen.orientation = ScreenOrientation.Portrait;
        }

        /// <summary>Landscape for Full Court.</summary>
        public static void Landscape()
        {
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            if (Screen.orientation != ScreenOrientation.LandscapeLeft && Screen.orientation != ScreenOrientation.LandscapeRight)
                Screen.orientation = ScreenOrientation.LandscapeLeft;
        }
    }
}
