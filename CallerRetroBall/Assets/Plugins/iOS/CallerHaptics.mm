// Caller Retro Ball — tiny iOS haptics bridge (original code).
// Called from C# via [DllImport("__Internal")]. Uses the public UIKit feedback generators.
#import <UIKit/UIKit.h>

extern "C" {

void CallerHaptics_Impact(int style)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        UIImpactFeedbackStyle s = UIImpactFeedbackStyleLight;
        if (style == 1) s = UIImpactFeedbackStyleMedium;
        else if (style == 2) s = UIImpactFeedbackStyleHeavy;
        UIImpactFeedbackGenerator *g = [[UIImpactFeedbackGenerator alloc] initWithStyle:s];
        [g prepare];
        [g impactOccurred];
    });
}

void CallerHaptics_Success(void)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        UINotificationFeedbackGenerator *g = [[UINotificationFeedbackGenerator alloc] init];
        [g prepare];
        [g notificationOccurred:UINotificationFeedbackTypeSuccess];
    });
}

}
