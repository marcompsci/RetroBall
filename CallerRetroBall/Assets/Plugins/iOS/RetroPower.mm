// RetroBall — power and heat bridge (original code). Called from C# via [DllImport("__Internal")].
#import <Foundation/Foundation.h>

extern "C" {

/// 1 when the iPhone is in Low Power Mode.
int RetroPower_IsLowPower(void)
{
    return [[NSProcessInfo processInfo] isLowPowerModeEnabled] ? 1 : 0;
}

/// 0 nominal, 1 fair, 2 serious, 3 critical (NSProcessInfoThermalState).
int RetroPower_ThermalState(void)
{
    return (int)[[NSProcessInfo processInfo] thermalState];
}

}
