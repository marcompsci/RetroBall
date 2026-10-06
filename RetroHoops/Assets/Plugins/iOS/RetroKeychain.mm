// Retro Hoops — the save file's sealing key (original code). Called from C# (Core/SaveKey.cs).
// 32 random bytes kept in the Keychain as a generic password: only this app can read it, it's
// available after the first unlock, and it never leaves this device (no iCloud Keychain sync). Links Security.framework (IosPostProcess).
#import <Foundation/Foundation.h>
#import <Security/Security.h>
#include <stdlib.h>
#include <string.h>

static NSString* const kRetroKeyService = @"com.phoronomicstudios.retrohoops.savekey";
static NSString* const kRetroKeyAccount = @"career";

static char* RetroKeyCopy(const char* s)
{
    if (s == NULL) s = "";
    char* out = (char*)malloc(strlen(s) + 1);
    strcpy(out, s);
    return out; // freed by the C# marshaller
}

static NSString* RetroKeyHex(NSData* d)
{
    const unsigned char* b = (const unsigned char*)d.bytes;
    NSMutableString* s = [NSMutableString stringWithCapacity:d.length * 2];
    for (NSUInteger i = 0; i < d.length; i++) [s appendFormat:@"%02x", b[i]];
    return s;
}

extern "C" char* RetroKeychain_SaveKey(int* created)
{
    if (created != NULL) *created = 0;
    NSDictionary* query = @{
        (__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
        (__bridge id)kSecAttrService: kRetroKeyService,
        (__bridge id)kSecAttrAccount: kRetroKeyAccount,
        (__bridge id)kSecReturnData: @YES,
        (__bridge id)kSecMatchLimit: (__bridge id)kSecMatchLimitOne,
    };
    CFTypeRef found = NULL;
    OSStatus st = SecItemCopyMatching((__bridge CFDictionaryRef)query, &found);
    if (st == errSecSuccess && found != NULL)
    {
        NSData* d = (__bridge NSData*)found;
        char* out = d.length == 32 ? RetroKeyCopy([RetroKeyHex(d) UTF8String]) : NULL;
        CFRelease(found);
        if (out != NULL) return out;
    }
    else if (st != errSecItemNotFound)
    {
        // The Keychain can't be read right now (e.g. launched in the background before the first unlock):
        // don't make a new key, or the existing one would be replaced. C# saves unsealed for this launch.
        return RetroKeyCopy("");
    }

    unsigned char bytes[32];
    if (SecRandomCopyBytes(kSecRandomDefault, sizeof(bytes), bytes) != errSecSuccess) return RetroKeyCopy("");
    NSData* key = [NSData dataWithBytes:bytes length:sizeof(bytes)];
    NSDictionary* match = @{
        (__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
        (__bridge id)kSecAttrService: kRetroKeyService,
        (__bridge id)kSecAttrAccount: kRetroKeyAccount,
    };
    SecItemDelete((__bridge CFDictionaryRef)match); // a wrong-sized leftover, if any
    NSDictionary* add = @{
        (__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
        (__bridge id)kSecAttrService: kRetroKeyService,
        (__bridge id)kSecAttrAccount: kRetroKeyAccount,
        (__bridge id)kSecAttrAccessible: (__bridge id)kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly,
        (__bridge id)kSecValueData: key,
    };
    st = SecItemAdd((__bridge CFDictionaryRef)add, NULL);
    if (st != errSecSuccess) return RetroKeyCopy(""); // C# falls back to a session key
    if (created != NULL) *created = 1;
    return RetroKeyCopy([RetroKeyHex(key) UTF8String]);
}
