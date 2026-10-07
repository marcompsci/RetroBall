using CallerRetroBall.Core;
using CallerRetroBall.Logic;

namespace CallerRetroBall.UI
{
    /// <summary>Phase 37: opens the screen an App Store screenshot launch asked for (see <see cref="StoreShots"/>).</summary>
    public sealed partial class MainMenuController
    {
        /// <summary>True the first time the menu builds on a store-screenshot launch: it opened the shot's screen and skips the first-launch dialogs.</summary>
        private bool RunStoreShot()
        {
            if (!StoreShots.Active) return false;
            if (StoreShots.Shown) return false; // back on the menu later: a normal menu
            StoreShots.Shown = true;
            switch (StoreShots.Current.Plan)
            {
                case StoreShotPlan.PlayMenu: ShowPlayMenu(); break;
                case StoreShotPlan.Clutch: ShowClutch(); break;
                case StoreShotPlan.Park: ShowPark(); break;
                case StoreShotPlan.Locker: SceneFlow.GoTo(SceneNames.LockerRoom); break;
                case StoreShotPlan.Game:
                case StoreShotPlan.FullCourt:
                    var request = StoreShots.GameRequest(App.Catalog, StoreShots.Current.Plan == StoreShotPlan.FullCourt);
                    if (request == null) break;
                    App.PendingMatch = request;
                    SceneFlow.GoTo(SceneNames.Game);
                    break;
            }
            return true;
        }
    }
}
