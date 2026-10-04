// RetroBall — iCloud key-value storage bridge (original code). Called from C# via [DllImport("__Internal")].
// Uses Apple's public NSUbiquitousKeyValueStore: the career is one string value in the player's own iCloud.
// The build post-processor adds the iCloud (key-value storage) capability.
#import <Foundation/Foundation.h>
#include <string.h>
#include <stdlib.h>

static BOOL gRetroCloudChanged = NO;
static id gRetroCloudObserver = nil;

static char *RetroCloudCopy(NSString *s)
{
    if (s == nil) return NULL;
    const char *utf8 = [s UTF8String];
    if (utf8 == NULL) return NULL;
    // C# marshals the returned string and frees this copy.
    char *copy = (char *)malloc(strlen(utf8) + 1);
    strcpy(copy, utf8);
    return copy;
}

extern "C" {

void RetroCloud_Start(void)
{
    if (gRetroCloudObserver == nil)
    {
        gRetroCloudObserver = [[NSNotificationCenter defaultCenter]
            addObserverForName:NSUbiquitousKeyValueStoreDidChangeExternallyNotification
                        object:[NSUbiquitousKeyValueStore defaultStore]
                         queue:[NSOperationQueue mainQueue]
                    usingBlock:^(NSNotification *note) { gRetroCloudChanged = YES; }];
    }
    [[NSUbiquitousKeyValueStore defaultStore] synchronize];
}

bool RetroCloud_IsSignedIn(void)
{
    return [[NSFileManager defaultManager] ubiquityIdentityToken] != nil;
}

char *RetroCloud_Get(const char *key)
{
    if (key == NULL) return NULL;
    NSString *value = [[NSUbiquitousKeyValueStore defaultStore] stringForKey:[NSString stringWithUTF8String:key]];
    return RetroCloudCopy(value);
}

void RetroCloud_Set(const char *key, const char *value)
{
    if (key == NULL || value == NULL) return;
    NSUbiquitousKeyValueStore *store = [NSUbiquitousKeyValueStore defaultStore];
    [store setString:[NSString stringWithUTF8String:value] forKey:[NSString stringWithUTF8String:key]];
    [store synchronize];
}

void RetroCloud_Remove(const char *key)
{
    if (key == NULL) return;
    NSUbiquitousKeyValueStore *store = [NSUbiquitousKeyValueStore defaultStore];
    [store removeObjectForKey:[NSString stringWithUTF8String:key]];
    [store synchronize];
}

void RetroCloud_Synchronize(void)
{
    [[NSUbiquitousKeyValueStore defaultStore] synchronize];
}

/// True once after another device changed the value (then resets).
bool RetroCloud_TakeChanged(void)
{
    BOOL changed = gRetroCloudChanged;
    gRetroCloudChanged = NO;
    return changed;
}

}
