// RetroBall — Game Center bridge (original code). Called from C# via [DllImport("__Internal")].
// Uses Apple's public GameKit API: sign-in, leaderboard scores, achievements, and the dashboard.
// Leaderboard and achievement ids must also be created in App Store Connect (see docs/GAME_CENTER.md).
#import <GameKit/GameKit.h>
#import <UIKit/UIKit.h>

extern UIViewController* UnityGetGLViewController(void);
extern "C" int RetroLive_ListenForInvites(void); // RetroLive.mm: friends' Live invites (Phase 34: right after sign-in)

@interface CallerGCDelegate : NSObject <GKGameCenterControllerDelegate>
@end

@implementation CallerGCDelegate
- (void)gameCenterViewControllerDidFinish:(GKGameCenterViewController *)viewController
{
    [viewController dismissViewControllerAnimated:YES completion:nil];
}
@end

static CallerGCDelegate *gCallerGCDelegate = nil;

extern "C" {

void CallerGC_Authenticate(void)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        GKLocalPlayer *player = [GKLocalPlayer localPlayer];
        player.authenticateHandler = ^(UIViewController *viewController, NSError *error) {
            if (viewController != nil)
                [UnityGetGLViewController() presentViewController:viewController animated:YES completion:nil];
            else if ([GKLocalPlayer localPlayer].isAuthenticated)
                RetroLive_ListenForInvites(); // Apple delivers an invite accepted at launch once a listener is registered
        };
    });
}

bool CallerGC_IsAuthenticated(void)
{
    return [GKLocalPlayer localPlayer].isAuthenticated;
}

void CallerGC_ReportScore(const char *leaderboardId, long long score)
{
    if (leaderboardId == NULL || ![GKLocalPlayer localPlayer].isAuthenticated) return;
    NSString *board = [NSString stringWithUTF8String:leaderboardId];
    [GKLeaderboard submitScore:(NSInteger)score
                       context:0
                        player:[GKLocalPlayer localPlayer]
                leaderboardIDs:@[board]
             completionHandler:^(NSError *error) {}];
}

void CallerGC_ReportAchievement(const char *achievementId, double percent)
{
    if (achievementId == NULL || ![GKLocalPlayer localPlayer].isAuthenticated) return;
    GKAchievement *a = [[GKAchievement alloc] initWithIdentifier:[NSString stringWithUTF8String:achievementId]];
    a.percentComplete = percent;
    a.showsCompletionBanner = YES;
    [GKAchievement reportAchievements:@[a] withCompletionHandler:^(NSError *error) {}];
}

// Phase 36: one leaderboard (friends only, or everyone), e.g. the Skills Gauntlet or the Live month.
void CallerGC_ShowLeaderboard(const char *leaderboardId, bool friendsOnly)
{
    if (leaderboardId == NULL) return;
    NSString *board = [NSString stringWithUTF8String:leaderboardId];
    dispatch_async(dispatch_get_main_queue(), ^{
        if (![GKLocalPlayer localPlayer].isAuthenticated) return;
        if (gCallerGCDelegate == nil) gCallerGCDelegate = [[CallerGCDelegate alloc] init];
        GKGameCenterViewController *vc = [[GKGameCenterViewController alloc] initWithLeaderboardID:board
                                                                                       playerScope:(friendsOnly ? GKLeaderboardPlayerScopeFriendsOnly : GKLeaderboardPlayerScopeGlobal)
                                                                                         timeScope:GKLeaderboardTimeScopeAllTime];
        vc.gameCenterDelegate = gCallerGCDelegate;
        [UnityGetGLViewController() presentViewController:vc animated:YES completion:nil];
    });
}

void CallerGC_ShowDashboard(void)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (![GKLocalPlayer localPlayer].isAuthenticated) return;
        if (gCallerGCDelegate == nil) gCallerGCDelegate = [[CallerGCDelegate alloc] init];
        GKGameCenterViewController *vc = [[GKGameCenterViewController alloc] initWithState:GKGameCenterViewControllerStateDefault];
        vc.gameCenterDelegate = gCallerGCDelegate;
        [UnityGetGLViewController() presentViewController:vc animated:YES completion:nil];
    });
}

}
